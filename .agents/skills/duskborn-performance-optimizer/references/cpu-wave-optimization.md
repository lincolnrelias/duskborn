# CPU and Enemy Horde Optimization

On Duskborn nights 5, 6, and 7, dozens or hundreds of enemies converge on players simultaneously. Without careful optimization, the main update loop consumes CPU cycles and causes sharp framerate drops.

---

## 1. Staggered Target Acquisition

Calling `PlayerRegistry.FindNearest(transform.position)` in `Update()` every frame results in complexity $O(E \times P \times \text{FPS})$.

### Recommended Pattern:
Add a temporally jittered interval to avoid spikes in the same frame:

```csharp
private float _targetScanTimer;
private const float ScanIntervalMin = 0.25f;
private const float ScanIntervalMax = 0.40f;

protected virtual void Update()
{
    // ...
    _targetScanTimer -= Time.deltaTime;
    if (_targetScanTimer <= 0f)
    {
        _targetScanTimer = UnityEngine.Random.Range(ScanIntervalMin, ScanIntervalMax);
        AcquireTarget();
    }
}
```

---

## 2. NavMeshAgent Update Frequency

Calling `Agent.SetDestination(target.position)` every frame forces frequent path recalculation by Unity's navigation system:

- Check distance from the previous destination before calling `SetDestination`:
  ```csharp
  if (Vector3.SqrMagnitude(CurrentTarget.position - _lastDest) > 1.5f * 1.5f)
  {
      _lastDest = CurrentTarget.position;
      Agent.SetDestination(_lastDest);
  }
  ```
- At short distances or when enemies are already within `attackRange`, pause navigation using `Agent.ResetPath()` or `Agent.isStopped = true`.

---

## 3. Animation and Physics Deactivation / LOD

- **Ragdolls**: Enemies with `EnemyRagdoll` must keep all rigidbodies at `isKinematic = true` and colliders disabled while alive. Activate ragdoll mode only at the fatal impact and disable it after settling.
- **Animator Culling**:
  - Set enemy `Animator` to `CullingMode = CullUpdateTransforms` to save processing while the monster is outside the camera view.
- **Local Speed Calculation**:
  - Instead of `transform.InverseTransformDirection` and vector updates every frame for distant enemies, calculate only from linear speed magnitude or use tick LOD.

---

## 4. Network Call Optimization (FishNet RPCs)

Avoid individual `[ObserversRpc]` calls for small sound and visual effects on every horde hit:
- Batch damage events if volume is extreme.
- Restrict secondary-effect RPC transmission range using distance-based observers (FishNet Grid / Proximity Conditionals).
