---
name: duskborn-performance-optimizer
description: >-
  Use esta skill para auditoria, prevenção de vazamentos de memória (memory leaks) e otimização de CPU, GPU e memória gerenciada (GC) no Duskborn (Unity 6 URP, FishNet, roguelite co-op com hordas de inimigos).
---

# Otimizador de Desempenho e Memória — Duskborn

Este guia estabelece os procedimentos e padrões técnicos para eliminar vazamentos de memória, minimizar alocações de Heap (GC Alloc) e maximizar o desempenho de CPU e GPU no **Duskborn** (Unity 6.000.4 LTS, Universal Render Pipeline 17.4, FishNet P2P).

---

## 1. Diretrizes Rápidas de Código (Regras de Ouro)

1. **Zero GC no Game Loop (`Update`, `FixedUpdate`, `LateUpdate`)**:
   - Nunca use `new` para coleções (`List`, `HashSet`, `Dictionary`), arrays ou classes em métodos de ciclo contínuo.
   - Substitua APIs físicas alocativas (`Physics.OverlapSphere`, `Physics.RaycastAll`) pelas variantes seguras com buffer reutilizável (`Physics.OverlapSphereNonAlloc`, `Physics.RaycastNonAlloc`).
   - Evite LINQ (`.Where()`, `.Select()`, `.ToList()`, etc.) e concatenação dinâmica de strings em código de execução frequente.

2. **Prevenção Rígida de Memory Leaks**:
   - Todo delegate ou evento assinado (`+=`) deve ter seu cancelamento explícito (`-=`) no método `OnDisable()`, `OnDestroy()` ou na reciclagem de pool.
   - Variáveis sincronizadas do FishNet (`SyncVar<T>.OnChange`) e callbacks de rede devem ser desinscritos quando o objeto for desativado/destruído.
   - Nunca chame `renderer.material` em tempo de execução sem destruir a instância criada ao final; use `renderer.sharedMaterial` ou `MaterialPropertyBlock` para manter compatibilidade com o **SRP Batcher**.

3. **Otimização de Hordas Noturnas (Night Waves)**:
   - Nunca calcule busca de alvos (`PlayerRegistry.FindNearest`) ou caminhos de navegação (`Agent.SetDestination`) para centenas de inimigos em todo frame.
   - Aplique taxas de atualização escalonadas (staggered ticks / intervalo com jitter) e desative sombras/animações secundárias em inimigos pequenos (Swarmer).
   - Reutilize entidades através de Object Pooling (`EnemyPool`, `DamageNumberPool`) em vez de `Instantiate` e `Destroy` contínuos durante as noites.

---

## 2. Fluxo de Auditoria e Otimização Passo a Passo

Sempre que inspecionar, refatorar ou criar componentes no projeto, siga estas etapas:

### Passo 1: Inspeção de Ciclo de Vida e Assinaturas de Eventos
- Verifique se classes que herdam de `MonoBehaviour` ou `NetworkBehaviour` registram listeners em:
  - Eventos estáticos (`PlayerRegistry`, `GameStateManager`, etc.).
  - Eventos de instâncias locais (`OnDied`, `OnHealthChanged`).
  - Eventos do FishNet (`SyncVar.OnChange`, `ServerManager`, `TimeManager`).
- **Ação**: Garanta que exista `OnDisable()` ou `OnDestroy()` removendo rigorosamente cada listener.

### Passo 2: Verificação de Alocações Ocultas (GC Alloc)
- Procure por:
  - `new WaitForSeconds(t)` dentro de corrotinas frequentes (armazene instâncias em cache ou use temporizadores manuais via `Time.deltaTime`).
  - Boxing de enums ou chamadas de log como `DuskLog.Log($"...")` executadas sem verificação condicional prévia.
  - Alocação de arrays de retorno em queries de colisão ou chamadas como `GetComponentsInChildren<T>()` em loops.
- **Consulte o guia**: [Prevenção de Vazamentos e Gestão de Memória](./references/memory-leaks-prevention.md).

### Passo 3: Otimização de CPU em Sistemas de Inimigos e IA
- Se o script afeta inimigos (`EnemyBase`, `WaveManager`, `EnemyRagdoll`):
  - Certifique-se de que a detecção de jogadores utilize distâncias ao quadrado (`sqrMagnitude`) ou ticks espaçados (ex: a cada 0.2s - 0.5s).
  - Garanta que inimigos fora da tela ou a longa distância não atualizem ragdolls ou cálculos desnecessários de animação.
- **Consulte o guia**: [Otimização de CPU e Hordas](./references/cpu-wave-optimization.md).

### Passo 4: Otimização de GPU e Render Pipeline (URP 17)
- Verifique o impacto do contorno (*Linework Lite* / `RenderingLayerMask`) e materiais:
  - O shader base deve permanecer compatível com SRP Batcher (`SRP Batcher: compatible`).
  - Desative projeção de sombras (`Cast Shadows = Off`) em mobs de alta densidade (Swarmer/Runner).
  - Separe `Canvas` de interface estática da interface dinâmica (números de dano flutuantes e barras de vida) para evitar re-criação da malha gráfica da UI (*canvas dirtying*).
- **Consulte o guia**: [Otimização de GPU e URP](./references/gpu-urp-rendering.md).

### Passo 5: Validação Sem Comprometer o Unity Editor
- Antes de commitar ou validar alterações, certifique-se de não bloquear o Unity Editor:
  - Nunca execute comandos da CLI do Unity com a Engine aberta.
  - Compile offline através das ferramentas internas (`dotnet build` ou scripts de validação em `Tools/unity.ps1` se o Editor estiver fechado).

---

## 3. Documentação Detalhada de Referência

- [Prevenção de Vazamentos de Memória e GC Alloc](./references/memory-leaks-prevention.md)
- [Otimização de CPU para Hordas e IA Noturna](./references/cpu-wave-optimization.md)
- [Otimização de GPU, Shaders URP e Linework Lite](./references/gpu-urp-rendering.md)
