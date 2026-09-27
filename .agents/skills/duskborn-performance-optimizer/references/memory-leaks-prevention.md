# Prevenção de Vazamentos de Memória e Controle de GC Alloc

No Unity com C#, vazamentos de memória geralmente ocorrem por:
1. Referências mantidas em **Delegates e Eventos C#**.
2. Instanciação não controlada de materiais (`renderer.material`).
3. Objetos de rede do FishNet desativados ou destruídos sem desacoplar callbacks.
4. Acúmulo de instâncias na memória nativa que o coletor de lixo (GC) não consegue liberar.

---

## 1. Padrão Seguro para Eventos e Delegates

Toda vez que uma classe registrar um método em um evento estático ou de instância, deve cancelá-lo no ciclo de vida apropriado:

```csharp
// ❌ INCORRETO: Causa retenção de memória após destruição do GameObject
private void Awake()
{
    _currentHP.OnChange += OnHPChanged;
    PlayerRegistry.OnPlayerSpawned += HandlePlayerSpawned;
}

// ✔️ CORRETO: Cancelamento explícito em OnDestroy ou OnDisable
private void Awake()
{
    _currentHP.OnChange += OnHPChanged;
}

private void OnEnable()
{
    PlayerRegistry.OnPlayerSpawned += HandlePlayerSpawned;
}

private void OnDisable()
{
    PlayerRegistry.OnPlayerSpawned -= HandlePlayerSpawned;
}

private void OnDestroy()
{
    _currentHP.OnChange -= OnHPChanged;
}
```

---

## 2. Eliminação de GC Alloc em Queries de Física

Inimigos e armas frequentemente realizam checagens de proximidade e áreas de ataque (ex: Cleave do Guerreiro, ataques de inimigos).

```csharp
// ❌ INCORRETO: Aloca um array novo a cada chamada no Heap
Collider[] hits = Physics.OverlapSphere(transform.position, radius, mask);
foreach (var hit in hits) { ... }

// ✔️ CORRETO: Buffer pré-alocado estático ou reutilizável sem custo de GC
private static readonly Collider[] HitBuffer = new Collider[32];

public void ExecuteMeleeCleave(float radius, LayerMask mask)
{
    int count = Physics.OverlapSphereNonAlloc(transform.position, radius, HitBuffer, mask);
    for (int i = 0; i < count; i++)
    {
        var col = HitBuffer[i];
        // Processa o impacto
        HitBuffer[i] = null; // Libera referência
    }
}
```

---

## 3. Gestão de Materiais e SRP Batcher

Acessar `renderer.material` clona a propriedade do material na memória de vídeo (VRAM) e quebra a compatibilidade com o SRP Batcher:

- Para ler propriedades: use `renderer.sharedMaterial`.
- Para alterar cores ou efeitos temporários (ex: flash de dano / `HitFlash`):
  - Utilize `MaterialPropertyBlock` configurado uma única vez por objeto ou compartilhado.
  - Para contornos no Duskborn, utilize a modificação do canal de camada de renderização (`renderingLayerMask`) sem instanciar materiais.

```csharp
// ✔️ Exemplo correto com MaterialPropertyBlock
private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
private MaterialPropertyBlock _propBlock;

private void Awake()
{
    _propBlock = new MaterialPropertyBlock();
}

public void SetFlash(Color color)
{
    _renderer.GetPropertyBlock(_propBlock);
    _propBlock.SetColor(ColorProperty, color);
    _renderer.SetPropertyBlock(_propBlock);
}
```

---

## 4. Otimização de Corrotinas e Yields

Evite instanciar objetos de espera repetidamente:

```csharp
// ❌ INCORRETO: 1 alocação a cada execução
IEnumerator DespawnTimer(float delay)
{
    yield return new WaitForSeconds(delay);
    Despawn();
}

// ✔️ CORRETO: Cache de intervalos comuns ou uso de contadores manuais
private static readonly WaitForSeconds WaitHalfSecond = new WaitForSeconds(0.5f);
private static readonly WaitForSeconds WaitOneSecond = new WaitForSeconds(1.0f);
```

---

## 5. Reciclagem e Pooling com FishNet

No Duskborn, inimigos em ondas noturnas escalam para grandes quantidades. Em vez de `Instantiate` e `Destroy` via `DespawnType.Destroy`:
- Utilize coleções pré-alocadas de instâncias (`EnemyPool`).
- Ao desativar uma entidade:
  1. Desative componentes pesados (`NavMeshAgent.enabled = false`, `Collider.enabled = false`).
  2. Limpe alvos e referências (`CurrentTarget = null`).
  3. Zere os timers de habilidades e efeitos (`ResetEnemy()`).
  4. Recicle o `NetworkObject` via FishNet Pooling (`Despawn(..., DespawnType.Pool)`).
