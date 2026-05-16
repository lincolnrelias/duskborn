# Handout: Weapon Hit Audio System

## Goal

Add surface-aware hit sounds to weapon attacks. Every swing plays a sound — no hit
plays the "swing/whoosh" sound; a hit plays the surface-specific sound for the thing
that was struck (enemy, tree, stone, …). Tag-based lookup with a fallback default so
adding a new surface type never requires touching existing code.

---

## How the combat pipeline works today

```
PlayerCombat / EnemyBase
  └─ WeaponActionPlayer.PlayAction(actionIndex, WeaponItem, CombatContext)
       └─ WeaponActionData.Events  (WeaponActionEvent[])
            └─ WeaponBehaviour.OnActionEvent(WeaponEventType, actionIndex, CombatContext)
                 └─ MeleeWeaponBehaviour
                      HitboxOpen  → ctx.Caster.ExecuteBasicMelee() / ExecuteHeavyMelee()
                      ──────────────────────────────────────────────────────────────
                      PlayerCombat.ExecuteBasicMelee()  → RequestAttackRpc  (server)
                      EnemyBase.ExecuteBasicMelee()     → PerformBasicMelee (server)
```

Key enum today (`WeaponEventType`):
```
HitboxOpen, HitboxClose, SpawnProjectile, Custom0, Custom1, Custom2
```

---

## Architecture

### New enum value

Add `Swing` to `WeaponEventType`. Assign it at the start of the swing animation clip
(e.g. normalised time 0.1). This fires the whoosh sound even when nothing is hit,
and acts as the "swing" trigger.

```csharp
public enum WeaponEventType
{
    HitboxOpen,
    HitboxClose,
    SpawnProjectile,
    Swing,          // ← new: fires whoosh / swing SFX
    Custom0,
    Custom1,
    Custom2,
}
```

### ScriptableObject: `WeaponAudioProfile`

Lives alongside `WeaponBehaviour`. Holds the swing clip and a list of surface entries.
Lookup order: exact tag match → `"Default"` entry → silence.

```csharp
// Assets/_Duskborn/Audio/WeaponAudioProfile.cs
[CreateAssetMenu(menuName = "Duskborn/Audio/Weapon Audio Profile")]
public class WeaponAudioProfile : ScriptableObject
{
    [SerializeField] public AudioClip[]      swingClips;   // whoosh, plays on Swing event
    [SerializeField] public SurfaceAudioEntry[] surfaces;  // tag → hit clips

    public AudioClip PickSwing() => swingClips.RandomOrNull();

    public AudioClip PickHit(string tag)
    {
        AudioClip[] found = null;
        foreach (var e in surfaces)
        {
            if (e.tag == tag)   { found = e.clips; break; }
            if (e.tag == "Default") found ??= e.clips;
        }
        return found?.RandomOrNull();
    }
}

[System.Serializable]
public class SurfaceAudioEntry
{
    public string      tag;    // Unity tag on the struck GameObject
    public AudioClip[] clips;  // random pick at runtime
}
```

### Helper extension

```csharp
// put in a static utility file, e.g. AudioExtensions.cs
public static AudioClip RandomOrNull(this AudioClip[] clips) =>
    clips is { Length: > 0 } ? clips[Random.Range(0, clips.Length)] : null;
```

### `WeaponAudioPlayer` — MonoBehaviour on the weapon prefab (or the entity root)

Receives audio requests and plays them. Use an `AudioSource` on the same GameObject.
Client-side only — audio does not need to be networked.

```csharp
// Assets/_Duskborn/Audio/WeaponAudioPlayer.cs
public class WeaponAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    public void PlaySwing(WeaponAudioProfile profile)
    {
        var clip = profile?.PickSwing();
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    public void PlayHit(WeaponAudioProfile profile, string surfaceTag)
    {
        var clip = profile?.PickHit(surfaceTag);
        if (clip != null) audioSource.PlayOneShot(clip);
        else DuskLog.Warn(LogChannel.Combat,
            $"WeaponAudioPlayer: no hit clip for tag '{surfaceTag}'.");
    }
}
```

### `WeaponDefinition` — add the profile field

```csharp
// existing field block in WeaponDefinition
[SerializeField] private WeaponAudioProfile audioProfile;
public WeaponAudioProfile AudioProfile => audioProfile;
```

Pass it through to `WeaponItem` the same way `behaviour` and `actions` are passed.

### `WeaponItem` — store and expose it

```csharp
public WeaponAudioProfile AudioProfile { get; }
// add to constructor parameter list and assignment, parallel to Behaviour
```

### `MeleeWeaponBehaviour` — fire audio events

This is where the audio hooks in. Two moments:

1. **`Swing` event** — play the whoosh immediately, before any collision check.
2. **`HitboxOpen` event** — the existing damage path already returns a list of
   things hit. After `ExecuteBasicMelee/ExecuteHeavyMelee` you can fire hit audio
   for each target.

The problem: `MeleeWeaponBehaviour.OnActionEvent` runs on the server for enemies and
on the client (owner) for players. Audio must play on the *client*, not the server.

**Approach — keep audio purely client-side:**

- The `CombatContext` already has `WeaponAnimator` (non-null for players) which is
  on the client. Attach a `WeaponAudioPlayer` to the same GameObject as
  `WeaponActionPlayer` and get it via `ctx.WeaponAnimator.GetComponent<WeaponAudioPlayer>()`.
- For enemies, `WeaponActionPlayer` is on the enemy prefab. Same get-component.
- Because `WeaponActionPlayer.Update` (which fires events) runs on all clients via
  FishNet's `NetworkBehaviour` replication, audio fires locally on each observer.
  **Verify this assumption first** — if events only fire server-side, you will need
  an `ObserversRpc` to broadcast the audio cue.

### `CombatContext` — optionally add `WeaponAudioPlayer`

Either look it up each time (cheap `GetComponent` is fine once per swing) or cache
it as an optional field on `CombatContext`, similar to `WeaponAnimator`.

---

## Touch points summary

| File | Change |
|---|---|
| `WeaponEventType.cs` | Add `Swing` |
| `WeaponDefinition.cs` | Add `[SerializeField] WeaponAudioProfile audioProfile` |
| `WeaponItem.cs` | Add `AudioProfile` property, thread through constructor |
| `MeleeWeaponBehaviour.cs` | Handle `Swing` event (play whoosh) and post-hit audio |
| `WeaponActionData` inspector | Add a `Swing` event at the start of each action clip |
| **New** `WeaponAudioProfile.cs` | ScriptableObject — swing clips + surface entries |
| **New** `WeaponAudioPlayer.cs` | MonoBehaviour — `AudioSource` wrapper |
| **New** `AudioExtensions.cs` | `RandomOrNull` helper |
| `LogChannel` | Add `Audio` channel |

---

## Tag convention

Use Unity's built-in tag system. Suggested baseline set:

| Tag | Surface |
|---|---|
| `Enemy` | flesh / enemy hit |
| `Default` | fallback for any unmatched tag |
| `Wood` | trees, crates |
| `Stone` | rocks, walls |
| `Metal` | armoured enemies, gates |

Tag the enemy root GameObject `Enemy`, resource nodes `Wood` or `Stone`, etc.
New surface types require only: tag the object + add a `SurfaceAudioEntry` to the
profile asset. No code changes.

---

## What to verify before starting

- Confirm whether `WeaponActionPlayer.Update` (and therefore `OnActionEvent`) runs
  on all clients or only on the server. Open a two-client session and check if
  animation events fire on non-owner clients. This determines whether audio can be
  purely local or needs an RPC.
- Confirm `AudioSource` settings needed (spatial blend, min/max distance) for the
  game's camera distance.
