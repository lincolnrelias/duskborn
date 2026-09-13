# Handoff: Sistema Unificado de Distribuição Lógica de Recursos e Folhagem

> **Contexto do Jogo**: *Duskborn* é um jogo cooperativo de sobrevivência/extração baseado em partidas com duração média de **1 a 2 horas**.
> **Objetivo Deste Passo**: Implementar uma arquitetura de distribuição espacial coesa que gerencie tanto **Nós de Recursos Interativos** (árvores, pedras, ferro, fibra) quanto **Folhagem Visual Pura** (grama, arbustos decorativos), com controle granular de densidade, agrupamento (*clustering*), espaçamento interno (*Poisson/Clearance*) e prevenção absoluta de sobreposições, mantendo o ritmo acelerado e o fluxo de combate limpo.

---

## 1. Avaliação Crítica & Separação de Responsabilidades

### ⚠️ Distinção Arquitetural Mandatória
1. **Nós de Recursos Interativos (Harvestable Resource Nodes)**:
   - Implementam `ResourceNode.cs`, `NetworkObject` (FishNet), `IDamageable`, possuem `Collider` físico, HP sincronizado via `SyncVar`, flutuação de dano e soltam itens ao esgotar.
   - **NÃO** podem ser mesclados no batch de malha estática da folhagem (`ChunkFoliagePlacer`). Precisam permanecer como instâncias de rede autoritativas pelo servidor gerenciadas pelo `WorldPropsPlacer`.
2. **Folhagem Visual Pura (Decorative Foliage)**:
   - Grama e arbustos puramente estéticos renderizados via shader `Duskborn/StylizedFoliage`, mesclando vértices ao terreno.
   - Gerados no cliente, sem colisão e combinados em 1 draw call por chunk para máxima performance.
3. **A Solução Unificada**:
   - Uma camada de coordenação espacial compartilhada (**Spatial Occupancy & Clustering Coordinator**). O servidor/gerador posiciona primeiro os recursos interativos em bolsões orgânicos e publica um mapa/máscara de ocupação. Em seguida, a folhagem visual decorativa é posicionada contornando os troncos e pedras (sem atravessar modelos) e adensando-se naturalmente sob as copas das árvores.

---

## 2. Visão de Game Design (Sessões de 1 a 2 Horas)

Para uma partida de média duração (60–120 min), a distribuição uniforme tradicional é frustrante e prejudica o ritmo:
- **Ritmo de Coleta em Rajadas (*Burst Gathering*)**: Em vez de caminhar 20 metros para cada árvore, o jogador encontra um bosque (*grove*) com 4 a 8 árvores agrupadas ou um veio (*vein*) com 3 a 5 nós de ferro. O jogador coleta rápido, enche o inventário e avança para a ação/crafting.
- **Clareiras de Combate Preservadas (*Combat Arenas*)**: O agrupamento liberta 30% a 40% do mapa para áreas abertas limpas onde jogadores podem esquivar, correr (*kite*) e enfrentar hordas sem colidir com obstáculos visuais ou ter a câmera obstruída.
- **Zoneamento Radial de Risco/Recompensa**:
  - **Zona 0 (Santuário Central, 0–12m)**: Área limpa, bancada inicial, zero bloqueio.
  - **Zona 1 (Periferia Próxima, 12–40m)**: Recursos essenciais de início rápido (madeira comum, pedra básica, fibra). Agrupamentos médios (3–5 nós), garantindo equipamentos iniciais nos primeiros 5 minutos de partida.
  - **Zona 2 (Terras Centrais, 40–80m)**: Bosques densos e veios mistos de ferro e pedra. Pontos naturais de encontro e disputa.
  - **Zona 3 (Platôs e Bordas Altas, 80m+)**: Concentração primária de Minério de Ferro e baús raros, cercados por desníveis de relevo e patrulhas de inimigos.

---

## 3. Especificação Técnica dos Componentes a Desenvolver

### A) Modelo de Dados: `ResourceClusterConfig` (ou extensão em `PropDefinition`)
Adicionar controles claros de agrupamento à definição de props:
```csharp
[System.Serializable]
public class ResourceClusterSettings
{
    [Tooltip("Se ativado, este recurso surge em bolsões agrupados.")]
    public bool enableClustering = true;

    [Tooltip("Quantidade de aglomerados/bolsões deste recurso por chunk.")]
    [Range(0, 8)] public int clustersPerChunk = 2;

    [Tooltip("Quantidade de nós gerados dentro de um mesmo aglomerado.")]
    public Vector2Int nodesPerCluster = new Vector2Int(3, 6);

    [Tooltip("Raio de espalhamento do aglomerado em torno do seu centro.")]
    [Range(2f, 15f)] public float clusterRadius = 6.0f;

    [Tooltip("Distância mínima entre nós do mesmo aglomerado (evita sobreposição física).")]
    [Range(1.2f, 5f)] public float intraClusterSpacing = 2.4f;

    [Tooltip("Distância mínima de isolamento em relação a outros tipos de recursos.")]
    [Range(2f, 10f)] public float interClusterSpacing = 5.0f;
}
```

### B) Coordenador Espacial: `WorldPlacementGrid` / `SpatialOccupancyMap`
Criar uma estrutura espacial bidimensional leve (baseada em grade de células de 1m a 2m) ou lista indexada de círculos de ocupação:
- **Tipos de Ocupação**:
  - `Resource_Solid`: Raio de bloqueio físico (ex: tronco de árvore r = 1.0m; rocha r = 1.4m; baú r = 1.2m).
  - `Resource_Canopy`: Raio de influência da copa (ex: r = 4.0m). Usado pela folhagem para gerar tufos de grama sombreada e cogumelos ao redor da árvore.
  - `Combat_Clearing`: Área reservada para clareiras de combate sem árvores ou pedras.
  - `Player_Sanctuary`: Santuário central e bancada.

### C) Refatoração do Pipeline no `WorldPropsPlacer.cs`
1. **Passo 1 — Geração de Centros de Aglomerados**:
   - O gerador calcula centros de clusters usando ruído de Poisson com restrições de bioma e inclinação.
2. **Passo 2 — Distribuição Intra-Cluster**:
   - Para cada centro de cluster, gera N nós respeitando `intraClusterSpacing` via raycast de terreno.
   - Registra a posição e o raio no mapa de ocupação espacial.
3. **Passo 3 — Exportação de Ocupação**:
   - Expõe o mapa de ocupação ou a lista de pontos ocupados para a camada de folhagem do chunk.

### D) Atualização do `ChunkFoliagePlacer.cs` (Folhagem Visual)
1. **Verificação de Não-Sobreposição**:
   - Ao sortear a coordenada de um tufo de grama ou arbusto estético, consulta o mapa de ocupação:
     - Se estiver dentro do `Resource_Solid` (raio do tronco ou rocha), o tufo é **rejeitado**.
     - Se estiver no anel `Resource_Canopy` (periferia da árvore), a probabilidade de spawn de grama/arbusto pode ser **aumentada** em 50% para simular vegetação exuberante debaixo da sombra.
2. **Desempenho Preservado**:
   - Continua combinando toda a folhagem visual do chunk em 1 único mesh batch (`UniversalForward`, GPU Instanced, 1 draw call).

---

## 4. Plano de Implementação Sugerido para o Próximo Agente

1. **Passo 1 — Enriquecer `PropDefinition.cs` / `WorldPropsConfig.cs`**:
   - Incluir os parâmetros de aglomerado (`clustersPerChunk`, `nodesPerCluster`, `clusterRadius`, `intraClusterSpacing`).
   - Configurar os ScriptableObjects existentes (`Prop_Tree.asset`, `Prop_Stone.asset`, `Prop_Iron.asset`, `Prop_Fiber.asset`).

2. **Passo 2 — Implementar Algoritmo de Agrupamento (*Cluster Spawner*) em `WorldPropsPlacer.cs`**:
   - Substituir a amostragem aleatória uniforme pura por amostragem baseada em bolsões (Cluster Centers + Local Poisson Disc Sampling).
   - Manter compatibilidade total com FishNet (`NetworkObject.Spawn`).

3. **Passo 3 — Interligar `WorldPropsPlacer` e `ChunkFoliagePlacer`**:
   - Passar a lista de obstáculos ocupados para o `ChunkFoliagePlacer` no momento da geração dos chunks.
   - Garantir que grama e arbustos nunca atravessem troncos de árvores ou veios de pedra.

4. **Passo 4 — Validação & Ajustes**:
   - Executar `dotnet build Mugg.sln`.
   - Testar no Editor/Play Mode: inspecionar clareiras de combate, densidade de bosques e ausência de sobreposições de malha.
