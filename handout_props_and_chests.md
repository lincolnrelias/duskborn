# Guia de Implementação: Integração de Recursos, Baús e POIs ao Terreno Procedural (Duskborn)

> **Destinatário:** Agente de Implementação / Desenvolvedor Unity  
> **Objetivo:** Integrar a geração procedural e determinística de nós de recursos (árvores, pedras, ferro, fibra), baús escalonados por distância e pontos de interesse (clareira de spawn, bancada, santuários) à malha de terreno *low-poly* em *chunks*, garantindo a melhor experiência *roguelike* cooperativa (1–5 jogadores).  
> **Base de Design:** GDD §5 (Loot & Crafting), §7 (World & Map) e `terrain_generation_handout.md`.

---

## 1. Visão Geral e Pilares de Game Design Roguelike

Em *Duskborn*, a exploração diurna é uma corrida contra o tempo antes que a noite caia. A distribuição de recursos e itens no mapa deve criar decisões estratégicas significativas de **Risco vs. Recompensa**:

1. **Clareira Central Segura (Spawn Hub):**
   - O centro do mapa $(0,0)$ é garantidamente plano e seguro, livre de densidade excessiva de árvores ou barreiras de rocha.
   - Contém o ponto de spawn dos jogadores, a bancada inicial de *crafting* (*Workbench*) e recursos básicos suficientes para fabricar os primeiros itens primitivos (*Tier 1*).
   - Contém 1–2 baús básicos de baixo custo inicial ($25\text{--}50\text{g}$) para dar objetivo imediato.

2. **Gradiente de Distância (Risk / Reward Radius):**
   - **Centro (Segurança):** Recursos abundantes de Madeira e Pedra; baús baratos com itens Comuns.
   - **Zona Média (Florestas e Planícies):** Densidade maior de recursos, arbustos de Fibra, baús médios ($75\text{--}100\text{g}$) com chance de itens Incomuns/Raros.
   - **Bordas e Picos Montanhosos (Alto Risco):** Nós de Minério de Ferro (*Iron Ore*), desfiladeiros perigosos, baús dourados/lendários ($150\text{--}250\text{g}$) com itens Raros, Lendários ou Amaldiçoados (*Cursed*). Estar longe do centro ao anoitecer força o time a lutar em terreno acidentado ou correr de volta.

3. **Geração Determinística via Semente (Multiplayer Sync):**
   - Todos os clientes calculam as posições de árvores, pedras e baús através da mesma semente determinística (`SeededRNG` / `customSeed`), garantindo sincronia sem tráfego de rede para coordenadas de vegetação.
   - Entidades interativas e destruíveis (`ResourceNode`, `Chest`) são gerenciadas com autoridade do Host via *FishNet* (`[ServerRpc]`, `SyncVar`, `LootManager`).

---

## 2. Arquitetura Técnica do Sistema

```text
ChunkGridManager (Terreno)
  │
  ├── 1. Gera as malhas de terreno de cada Chunk (TerrainChunk + MeshCollider)
  │
  └── 2. WorldPropsPlacer (Novo Gerenciador de Props & Spawns)
        ├── Inicializa SeededRNG com a semente da partida
        ├── Raycast vertical contra os MeshColliders para posicionamento exato no relevo
        │
        ├── [A] Clareira Central (Centro do Mapa)
        │     ├── Limpa raio central (Clear radius)
        │     ├── Define PlayerSpawnPoints
        │     └── Instancia Workbench Site
        │
        ├── [B] Distribuição de Recursos (Resource Nodes)
        │     ├── Florestas / Grama baixa: Árvores (Wood) + Fibras
        │     ├── Zonas de Rocha / Altitude: Rochas (Stone) + Ferro (Iron Ore)
        │     └── Encostas Íngremes: Boulders decorativos / Bloqueadores de passagem
        │
        ├── [C] Distribuição de Baús (Chests)
        │     ├── Grid Jitter / Poisson-Disc sampling (evita acúmulo)
        │     └── Escala de Custo e Tabela de Loot por distância ao centro
        │
        └── 3. RebuildNavMesh() (Recalcula o NavMesh englobando árvores, rochas e baús)
```

---

## 3. Mapeamento de Biomas e Regras de Posicionamento

Cada tipo de objeto possui regras de filtragem por **Altitude ($Y$)**, **Inclinação (Slope)** e **Distância do Centro ($R$)**:

| Tipo de Prop | Prefab / Componente | Condição de Altitude / Terreno | Inclinação Máxima | Regra de Distância / Densidade |
|---|---|---|---|---|
| **Árvore (Madeira)** | `ResourceNode` (`TargetType.Tree`) | $Y > \text{waterLevel} + 0.5\text{m}$ e $Y < \text{snowLevel}$ | $\le 25^\circ$ (terreno caminhável) | Alta densidade em áreas de grama; fora do raio central de spawn |
| **Rocha (Pedra)** | `ResourceNode` (`TargetType.MiningNode`) | Qualquer $Y > \text{waterLevel} + 0.3\text{m}$ | $\le 45^\circ$ | Média densidade; mais comum em zonas rochosas |
| **Minério de Ferro** | `ResourceNode` (`TargetType.MiningNode`) | $Y \ge \text{heightMultiplier} \times 0.45$ ou Encostas | $\le 40^\circ$ | Raro; apenas em zonas de altitude ou bordas distantes |
| **Arbusto (Fibra)** | `ResourceNode` / Pickup de Fibra | Zonas de planície / grama | $\le 20^\circ$ | Disperso em planícies abertas |
| **Baú Comum** | `Chest.prefab` (Loot Comum/Incomum) | Solo seco ($Y > \text{waterLevel} + 0.5\text{m}$) | $\le 15^\circ$ | Raio central a intermediário ($R \le 40\text{m}$); Custo: $35\text{--}50\text{g}$ |
| **Baú Avançado/Ouro**| `Chest.prefab` (Loot Raro/Lendário) | Solo seco, topos de colina ou bordas | $\le 20^\circ$ | Raio externo ($R > 40\text{m}$); Custo: $80\text{--}150\text{g}$ |
| **Bancada (Workbench)**| `Workbench` prefab | Clareira central | $\le 5^\circ$ (plano) | 1 instância próxima ao spawn dos jogadores |

---

## 4. Estrutura de Arquivos Proposta

```text
Assets/_Duskborn/
├── Gameplay/
│   ├── World/
│   │   ├── Props/
│   │   │   ├── PropDefinition.cs       // SO: Prefab, densidade, regras de altura/slope, raio de exclusão
│   │   │   ├── WorldPropsConfig.cs     // SO: Lista de props, baús por tier, raio de clareira
│   │   │   └── WorldPropsPlacer.cs     // Componente que executa a amostragem e spawn dos props
│   │   ├── LowPolyTerrainConfig.cs     // (Já implementado)
│   │   ├── TerrainChunk.cs             // (Já implementado)
│   │   └── ChunkGridManager.cs         // (Atualizado para chamar WorldPropsPlacer antes do NavMesh)
│   ├── Loot/
│   │   ├── Chest.cs                    // (Já implementado com SyncVar e TargetRpc)
│   │   ├── ResourceNode.cs             // (Já implementado com TargetType e LootDropper)
│   │   └── LootTable.cs / DropLootTable.cs // (Já implementados)
│   └── Crafting/
│       └── Workbench.cs                // Componente simples de interação da bancada
└── ScriptableObjects/
    └── World/
        ├── PropsConfig_Default.asset   // Configuração padrão de árvores, rochas, ferro e baús
        └── ...
```

---

## 5. Especificação Técnica dos Novos Scripts

### 5.1 `PropDefinition.cs` (ScriptableObject)
Define as regras ecológicas de cada elemento decorativo ou interativo:

```csharp
using UnityEngine;

namespace Duskborn.Gameplay.World
{
    [CreateAssetMenu(fileName = "Prop_Name", menuName = "Duskborn/World/Prop Definition")]
    public class PropDefinition : ScriptableObject
    {
        public string propName = "Tree";
        public GameObject prefab;

        [Header("Densidade por Chunk")]
        [Range(0, 50)] public int minPerChunk = 2;
        [Range(0, 50)] public int maxPerChunk = 6;

        [Header("Condições de Terreno")]
        public float minHeight = 2.5f;
        public float maxHeight = 20.0f;
        [Range(0f, 60f)] public float maxSlopeAngle = 25f;

        [Header("Variação de Escala e Rotação")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.25f);
        public bool randomYRotation = true;
        public bool alignToNormal = false;

        [Header("Espaçamento")]
        [Tooltip("Raio mínimo de distância de outros props")]
        public float exclusionRadius = 2.0f;
    }
}
```

---

### 5.2 `WorldPropsConfig.cs` (ScriptableObject)
Agrupa as definições de todos os recursos, baús e regras da clareira:

```csharp
using UnityEngine;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.World
{
    [CreateAssetMenu(fileName = "WorldPropsConfig", menuName = "Duskborn/World/World Props Config")]
    public class WorldPropsConfig : ScriptableObject
    {
        [Header("Clareira Central (Safe Spawn Zone)")]
        [Tooltip("Raio em torno de (0,0) onde não serão geradas árvores ou rochas densas")]
        public float centerClearingRadius = 10f;
        public GameObject workbenchPrefab;

        [Header("Recursos Naturais (Resource Nodes)")]
        public PropDefinition treeProp;
        public PropDefinition stoneProp;
        public PropDefinition ironProp;
        public PropDefinition fiberProp;

        [Header("Configuração de Baús")]
        public GameObject chestPrefab;
        [Range(1, 20)] public int totalChests = 8;
        public LootTable basicLootTable;
        public LootTable rareLootTable;
        public int minChestCost = 35;
        public int maxChestCost = 150;
    }
}
```

---

### 5.3 `WorldPropsPlacer.cs` (Monobehaviour)
Executa a geração procedural com **Raycasts** contra a malha recém-gerada:

1. **Amostragem em Grid com Jitter:** Divide cada *chunk* em sub-células e aplica jitter determinístico com `SeededRNG`.
2. **Validação de Terreno:** Dispara um `Physics.Raycast` de cima para baixo ($Y = 100 \rightarrow -10$).
   - Obtém `hit.point` (altura exata da face) e `hit.normal` (inclinação).
   - Valida se `hit.point.y` e `slopeAngle` satisfazem `PropDefinition`.
   - Rejeita se estiver dentro de `centerClearingRadius` (exceto itens da clareira).
3. **Escalonamento de Baús:**
   - Calcula a distância $d = \text{Vector3.Distance}(pos, \text{Vector3.zero})$.
   - Normaliza $t = \text{Clamp01}(d / \text{mapRadius})$.
   - Custo do baú: $\text{Lerp}(minCost, maxCost, t)$.
   - Atribui `basicLootTable` se $t < 0.5$ ou `rareLootTable` se $t \ge 0.5$.
4. **Instanciação:** Em *Host/Singleplayer*, se o objeto contiver `NetworkObject`, spawna via `InstanceFinder.ServerManager.Spawn(go)`.

---

## 6. Integração com `ChunkGridManager` e Ordem de Execução

O ciclo completo de geração passa a ser:

```csharp
public void GenerateGrid()
{
    // 1. Limpa terreno e props antigos
    ClearGrid();
    ClearProps();

    // 2. Gera os Chunks e malhas com MeshColliders
    GenerateTerrainChunks();

    // 3. Spawna os nós de recursos, baús e clareira
    if (propsPlacer != null)
    {
        propsPlacer.PlaceWorldProps(config, propsConfig, activeSeed);
    }

    // 4. Baka o NavMesh englobando a malha do terreno e os colliders dos props
    RebuildNavMesh();
}
```

---

## 7. Instruções Passo a Passo para o Agente Executor

1. **Criação dos Scripts:**
   - Criar `PropDefinition.cs`, `WorldPropsConfig.cs` e `WorldPropsPlacer.cs` em `Assets/_Duskborn/Gameplay/World/Props/`.
   - Atualizar `ChunkGridManager.cs` e `ChunkGridManagerEditor.cs` para suportar o novo passo de *spawning*.
2. **Criação dos ScriptableObjects:**
   - Criar `Prop_Tree.asset`, `Prop_Stone.asset`, `Prop_Iron.asset`, `Prop_Fiber.asset`.
   - Criar `WorldPropsConfig_Default.asset` vinculando os prefabs de `ResourceNode` e `Chest`.
3. **Testes de Validação:**
   - Clicar em **"Gerar Terreno"** no Inspector.
   - Verificar se as árvores e pedras surgem cravadas no relevo sem flutuar nem afundar.
   - Verificar se o centro $(0,0)$ permanece limpo e com a bancada.
   - Verificar se o *NavMesh* foi assado contornando os troncos e baús corretamente.
