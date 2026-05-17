# Handout: Weapon Hit Effect System

## Goal

Spawn a particle-system prefab at the point of contact whenever a weapon or skill hits
something. Tag-based surface lookup with a default fallback (same pattern as audio).
Per-skill override prefab for custom VFX. Effects are **visual-only, client-local** —
no NetworkObject, no server authority, each client spawns and cleans up independently.

---

## How it plugs into the existing pipeline

The audio system already added `RpcOnHitAudio` (TargetRpc → owner only). Effects need
`[ObserversRpc]` instead — particles must be visible to every client watching the fight,
not just the attacker.

```
PlayerCombat (server)
  RequestAttackRpc / RequestHeavyAttackRpc / RequestCleaveRpc / RequestNodeHitRpc
    └─ RpcOnHitEffect [ObserversRpc] (string tag, Vector3 position)
         └─ WeaponEffectPlayer.SpawnHitEffect(tag, position)
              └─ WeaponEffectProfile.PickEffect(tag)  ← resolved locally on each client
                   └─ Instantiate prefab at position
                        └─ Coroutine: WaitUntil !ps.IsAlive(true) → Destroy
```

**Key difference from audio:** `RpcOnHitAudio` is a `TargetRpc` (owner only, uses
`CurrentWeapon` to resolve the profile). `RpcOnHitEffect` is an `ObserversRpc` (all
clients). Non-owner clients won't have `CurrentWeapon` set, so the profile must be
stored directly on `WeaponEffectPlayer` as a serialised field — one profile per
entity prefab, set in the inspector. See the profile resolution section below.

---

## Architecture

### `WeaponEffectProfile` — ScriptableObject

Same tag-based lookup pattern as `WeaponAudioProfile`.

```csharp
// Assets/_Duskborn/Effects/WeaponEffectProfile.cs
[CreateAssetMenu(menuName = "Duskborn/Effects/Weapon Effect Profile")]
public class WeaponEffectProfile : ScriptableObject
{
    [SerializeField] public SurfaceEffectEntry[] surfaces;

    public GameObject PickEffect(string tag)
    {
        GameObject found = null;
        foreach (var e in surfaces)
        {
            if (e.tag == tag)       { found = e.prefab; break; }
            if (e.tag == "Default")   found ??= e.prefab;
        }
        return found;
    }
}

[System.Serializable]
public class SurfaceEffectEntry
{
    public string     tag;
    public GameObject prefab; // root must have a ParticleSystem
}
```

### `WeaponEffectPlayer` — MonoBehaviour

Sits on the same root GameObject as `WeaponActionPlayer`, `WeaponAudioPlayer`,
`WeaponHitNotifier`. The profile is a **direct serialised field** so all clients
(owner or observer) can resolve it without needing `CurrentWeapon`.

```csharp
// Assets/_Duskborn/Effects/WeaponEffectPlayer.cs
public class WeaponEffectPlayer : MonoBehaviour
{
    [SerializeField] private WeaponEffectProfile profile;

    private WeaponActionPlayer _actionPlayer;

    private void Awake() => _actionPlayer = GetComponent<WeaponActionPlayer>();

    public void SpawnHitEffect(string tag, Vector3 position)
    {
        var prefab = _actionPlayer?.CurrentSkill?.hitEffectOverride
                  ?? profile?.PickEffect(tag);
        if (prefab == null) return;

        var go = Instantiate(prefab, position, Quaternion.identity);
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null) StartCoroutine(DestroyWhenDone(go, ps));
        else            Destroy(go, 5f); // safety fallback if no PS on root
    }

    private IEnumerator DestroyWhenDone(GameObject go, ParticleSystem ps)
    {
        yield return new WaitUntil(() => !ps.IsAlive(withChildren: true));
        Destroy(go);
    }
}
```

### `WeaponSkill` — per-skill override

```csharp
// add alongside hitAudioOverride
[SerializeField] public GameObject hitEffectOverride; // null = use entity profile
```

### `PlayerCombat` — broadcast to all observers

Add one new RPC and call it from every hit site alongside `RpcOnHitAudio`.

```csharp
[ObserversRpc]
private void RpcOnHitEffect(string tag, Vector3 position)
{
    GetComponent<WeaponEffectPlayer>()?.SpawnHitEffect(tag, position);
}
```

Call sites — pass the **point of contact**, not just the entity centre:

| ServerRpc | Tag | Position |
|---|---|---|
| `RequestAttackRpc` | `hitEnemies[0].tag` | `cols[firstHitIndex].ClosestPoint(origin)` |
| `RequestHeavyAttackRpc` | same | same |
| `RequestCleaveRpc` | same | same |
| `RequestNodeHitRpc` | `nodeObj.tag` | `nodeObj.transform.position` |

`col.ClosestPoint(origin)` gives the surface contact point rather than the entity
pivot — worth using for enemies so sparks spawn at the weapon's impact point, not at
the enemy's feet.

---

## Profile resolution — why the serialised field matters

`RpcOnHitAudio` is `TargetRpc` (owner only). The owner IS playing the action, so
`WeaponActionPlayer.CurrentWeapon` is valid and the audio profile lookup works.

`RpcOnHitEffect` is `ObserversRpc` (everyone). Other clients are not calling
`PlayAction`, so `CurrentWeapon` is null for them. Storing the profile directly on
`WeaponEffectPlayer` means every client — owner or not — resolves it the same way:

```
WeaponEffectPlayer.profile  ← set once in the player/enemy prefab inspector
```

The skill override (`CurrentSkill?.hitEffectOverride`) still works on the **owner**
client because `CurrentSkill` is only populated during `PlaySkillAction`, which runs
on the owner. Non-owner observers will fall through to the profile lookup — they will
see the default surface effect rather than the custom skill effect. This is acceptable
for a first pass; a proper fix would require sending a skill index in the RPC.

---

## Effect prefab conventions

- Root must have a `ParticleSystem` — the cleanup coroutine watches it with
  `IsAlive(withChildren: true)`, so child systems are included automatically.
- Do **not** add `NetworkObject` — effects are local-only, no server state.
- Orient assuming the prefab spawns at world position with identity rotation. If
  surface-normal alignment is needed later, add a `Vector3 normal` parameter to
  `RpcOnHitEffect` and pass `col.normal` from the server-side overlap.
- The 5-second `Destroy(go, 5f)` fallback in `WeaponEffectPlayer` catches prefabs
  that accidentally have no root `ParticleSystem`.

---

## Touch points summary

| File | Change |
|---|---|
| `WeaponSkill.cs` | Add `GameObject hitEffectOverride` |
| `PlayerCombat.cs` | Add `[ObserversRpc] RpcOnHitEffect(string tag, Vector3 position)`, call from all 4 hit RPCs |
| **New** `Effects/WeaponEffectProfile.cs` | ScriptableObject — tag → prefab entries |
| **New** `Effects/WeaponEffectPlayer.cs` | MonoBehaviour — Instantiate + coroutine cleanup |

`WeaponDefinition` / `WeaponItem` do **not** need the profile threaded through them —
it lives directly on the entity prefab component, not on the weapon asset.

---

## What to verify before starting

- Confirm `[ObserversRpc]` fires on the host/server itself, not only remote clients.
  FishNet fires observer RPCs on the server too when it is also a client — effects
  should appear for the host player.
- Confirm that `col.ClosestPoint(origin)` is available on all collider types in use
  (it works on `BoxCollider`, `SphereCollider`, `CapsuleCollider`, `MeshCollider`
  with read/write enabled). If any colliders are non-readable, fall back to
  `col.transform.position`.
- Confirm particle prefabs have **Stop Action → None** (not Destroy) so the
  `IsAlive` check can fire before Unity auto-destroys them.
