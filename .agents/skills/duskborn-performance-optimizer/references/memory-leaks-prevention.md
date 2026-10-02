# Memory Leak Prevention and GC Alloc Control

In Unity C#, memory leaks usually result from:
1. References retained by **C# Delegates and Events**.
2. Uncontrolled material instantiation (`renderer.material`).
3. FishNet network objects disabled or destroyed without detaching callbacks.
4. Accumulating native memory instances that the garbage collector (GC) cannot release.

---

## 1. Safe Event and Delegate Pattern

Whenever a class registers a method with a static or instance event, unsubscribe it at the appropriate lifecycle point:

```csharp
// ❌ INCORRECT: Retains memory after GameObject destruction.
private void Awake()
{
    _currentHP.OnChange += OnHPChanged;
    PlayerRegistry.OnPlayerSpawned += HandlePlayerSpawned;
}

// ✔️ CORRECT: Explicit unsubscription in OnDestroy or OnDisable.
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

## 2. Eliminate GC Alloc in Physics Queries

Enemies and weapons frequently check proximity and attack areas (e.g. Warrior Cleave and enemy attacks).

```csharp
// ❌ INCORRECT: Allocates a new heap array on every call.
Collider[] hits = Physics.OverlapSphere(transform.position, radius, mask);
foreach (var hit in hits) { ... }

// ✔️ CORRECT: Preallocated static or reusable buffer without GC overhead.
private static readonly Collider[] HitBuffer = new Collider[32];

public void ExecuteMeleeCleave(float radius, LayerMask mask)
{
    int count = Physics.OverlapSphereNonAlloc(transform.position, radius, HitBuffer, mask);
    for (int i = 0; i < count; i++)
    {
        var col = HitBuffer[i];
        // Process the impact.
        HitBuffer[i] = null; // Release the reference.
    }
}
```

---

## 3. Material Management and SRP Batcher

Accessing `renderer.material` clones the material property in video memory (VRAM) and breaks SRP Batcher compatibility:

- To read properties: use `renderer.sharedMaterial`.
- To change colors or temporary effects (e.g. damage flash / `HitFlash`):
  - Use a `MaterialPropertyBlock` configured once per object or shared.
  - For Duskborn outlines, modify the rendering layer channel (`renderingLayerMask`) without instantiating materials.

```csharp
// ✔️ Correct MaterialPropertyBlock example.
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

## 4. Coroutine and Yield Optimization

Avoid repeatedly instantiating wait objects:

```csharp
// ❌ INCORRECT: 1 allocation on every execution.
IEnumerator DespawnTimer(float delay)
{
    yield return new WaitForSeconds(delay);
    Despawn();
}

// ✔️ CORRECT: Cache common intervals or use manual counters.
private static readonly WaitForSeconds WaitHalfSecond = new WaitForSeconds(0.5f);
private static readonly WaitForSeconds WaitOneSecond = new WaitForSeconds(1.0f);
```

---

## 5. Recycling and Pooling with FishNet

In Duskborn, nighttime enemies scale to large counts. Instead of `Instantiate` and `Destroy` through `DespawnType.Destroy`:
- Use preallocated instance collections (`EnemyPool`).
- When disabling an entity:
  1. Disable expensive components (`NavMeshAgent.enabled = false`, `Collider.enabled = false`).
  2. Clear targets and references (`CurrentTarget = null`).
  3. Reset skill and effect timers (`ResetEnemy()`).
  4. Recycle the `NetworkObject` through FishNet pooling (`Despawn(..., DespawnType.Pool)`).
