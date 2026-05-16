# Skill System Unification

**Goal:** Make enemy skill slots accept the exact same `WeaponSkill` ScriptableObject assets that
player weapons use. One `CleaveSkill.asset` must be assignable to both a weapon definition and an
enemy's Skills array.

---

## Why this requires a refactor

| | Player | Enemy |
|---|---|---|
| Skill base class | `WeaponSkill : ScriptableObject` | `EnemySkill : ScriptableObject` |
| Context passed to `Use()` | `ActionContext` (struct, player-specific) | `EnemyActionContext` (class) |
| Cleave skill | `CleaveSkill` → `ctx.Combat.TriggerCleave(…)` | `CleaveEnemySkill` → `ctx.Enemy.ExecuteCleave(…)` |

To share assets both sides need the same base class and a context both can build.

---

## Target architecture

```
ICombatEntity (interface, Duskborn.Gameplay)
  ├── PlayerCombat   implements ICombatEntity
  └── EnemyBase      implements ICombatEntity

CombatContext (class, replaces ActionContext)
  ├── ICombatEntity  Caster        — always set
  ├── Transform      Target        — always set (null for player, set by enemy)
  ├── PlayerCombat   Combat        — null when caster is an enemy
  ├── PlayerStats    Stats         — null when caster is an enemy
  ├── ActionBarService ActionBar   — null when caster is an enemy
  ├── int            SlotIndex     — -1 when caster is an enemy
  └── WeaponActionPlayer WeaponAnimator — null when caster is an enemy

WeaponSkill.Use(CombatContext ctx)   ← unified, replaces both Use signatures
EnemyBase.skills  →  WeaponSkill[]  ← replaces EnemySkill[]
```

---

## Files to DELETE

```
Assets/_Duskborn/Gameplay/Enemies/EnemySkill.cs
Assets/_Duskborn/Gameplay/Enemies/EnemySkill.cs.meta
Assets/_Duskborn/Gameplay/Enemies/EnemyActionContext.cs
Assets/_Duskborn/Gameplay/Enemies/EnemyActionContext.cs.meta
Assets/_Duskborn/Gameplay/Enemies/Skills/CleaveEnemySkill.cs
Assets/_Duskborn/Gameplay/Enemies/Skills/CleaveEnemySkill.cs.meta
```

Also delete the `Skills/` folder and its `.meta` if it becomes empty.

---

## Files to CREATE

### 1. `Assets/_Duskborn/Gameplay/ICombatEntity.cs`

```csharp
namespace Duskborn.Gameplay
{
    /// <summary>
    /// Shared contract for anything that can be the source of a combat skill.
    /// PlayerCombat and EnemyBase both implement this.
    /// Add a new method here whenever a skill needs a new execution type.
    /// </summary>
    public interface ICombatEntity
    {
        void ExecuteCleave(float range, float arcDegrees, float damageMultiplier);
    }
}
```

---

## Files to MODIFY

### 2. `Assets/_Duskborn/Gameplay/ActionBar/IActionBarActions.cs`

Replace the `ActionContext` struct with `CombatContext` class. The interfaces stay the same but
reference the new type.

**Remove** the entire `ActionContext` struct.

**Add** `CombatContext` class (it can live in this same file or a new
`Assets/_Duskborn/Gameplay/CombatContext.cs` — separate file is cleaner):

```csharp
// Namespace: Duskborn.Gameplay   (NOT Duskborn.Gameplay.ActionBar)
using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Player;
using UnityEngine;

namespace Duskborn.Gameplay
{
    public class CombatContext
    {
        public readonly ICombatEntity      Caster;
        public readonly Transform          Target;
        // Player-specific — null when the caster is an enemy.
        public readonly PlayerCombat       Combat;
        public readonly PlayerStats        Stats;
        public readonly ActionBarService   ActionBar;
        public readonly int                SlotIndex;
        public readonly WeaponActionPlayer WeaponAnimator;

        // Player constructor (Target is implicit — found by skills from world state).
        public CombatContext(PlayerCombat combat, PlayerStats stats,
                             ActionBarService actionBar, int slotIndex,
                             WeaponActionPlayer weaponAnimator = null)
        {
            Caster         = combat;
            Target         = null;
            Combat         = combat;
            Stats          = stats;
            ActionBar      = actionBar;
            SlotIndex      = slotIndex;
            WeaponAnimator = weaponAnimator;
        }

        // Enemy constructor.
        public CombatContext(ICombatEntity caster, Transform target)
        {
            Caster         = caster;
            Target         = target;
            Combat         = null;
            Stats          = null;
            ActionBar      = null;
            SlotIndex      = -1;
            WeaponAnimator = null;
        }
    }
}
```

Update `ILeftClickAction` and `IRightClickAction` in the same file:

```csharp
// Change ActionContext → CombatContext in both interfaces:
public interface ILeftClickAction  { void OnLeftClick (CombatContext ctx); }
public interface IRightClickAction { void OnRightClick(CombatContext ctx); }
```

---

### 3. `Assets/_Duskborn/Gameplay/Equipment/WeaponSkill.cs`

```csharp
using Duskborn.Gameplay;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    public abstract class WeaponSkill : ScriptableObject
    {
        [SerializeField] public float            cooldown  = 5f;
        [SerializeField] public WeaponActionData animation;

        public abstract void Use(CombatContext ctx);
    }
}
```

---

### 4. `Assets/_Duskborn/Gameplay/Equipment/CleaveSkill.cs`

```csharp
using Duskborn.Gameplay;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(fileName = "CleaveSkill", menuName = "Duskborn/Weapon Skills/Cleave")]
    public class CleaveSkill : WeaponSkill
    {
        [SerializeField] private float range           = 3f;
        [SerializeField] private float arcDegrees      = 180f;
        [SerializeField] private float damageMultiplier = 1f;

        public override void Use(CombatContext ctx)
        {
            ctx.Caster.ExecuteCleave(range, arcDegrees, damageMultiplier);
        }
    }
}
```

---

### 5. `Assets/_Duskborn/Gameplay/Equipment/WeaponBehaviour.cs`

Change the `ActionContext` parameter to `CombatContext`:

```csharp
using Duskborn.Gameplay;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    public abstract class WeaponBehaviour : ScriptableObject
    {
        public abstract void OnActionEvent(WeaponEventType type, int actionIndex, CombatContext ctx);
    }
}
```

---

### 6. `Assets/_Duskborn/Gameplay/Equipment/MeleeWeaponBehaviour.cs`

Update the override signature to `CombatContext`. The body uses `ctx.Combat` which is non-null for
player attacks (the only caller of `OnActionEvent`):

```csharp
public override void OnActionEvent(WeaponEventType type, int actionIndex, CombatContext ctx)
```

---

### 7. `Assets/_Duskborn/Gameplay/Equipment/WeaponItem.cs`

Change both click action signatures and the `OnLeftClick`/`OnRightClick` bodies:

```csharp
// Interface methods:
public virtual void OnLeftClick (CombatContext ctx) { … }
public virtual void OnRightClick(CombatContext ctx) { … }
```

Remove `using Duskborn.Gameplay.ActionBar;` if it was only needed for `ActionContext`.
Add `using Duskborn.Gameplay;`.

---

### 8. `Assets/_Duskborn/Gameplay/Equipment/WeaponActionPlayer.cs`

Change every occurrence of `ActionContext` to `CombatContext`:

- Field `private ActionContext _activeCtx` → `private CombatContext _activeCtx`
- `PlayAction(int actionIndex, WeaponItem weapon, ActionContext ctx)` signature
- `PlaySkillAction(WeaponSkill skill, ActionContext ctx)` signature

Add `using Duskborn.Gameplay;`, remove `using Duskborn.Gameplay.ActionBar;` if no longer needed.

---

### 9. `Assets/_Duskborn/Gameplay/Player/PlayerCombat.cs`

**Implement `ICombatEntity`:**

```csharp
public class PlayerCombat : NetworkBehaviour, ICombatEntity
```

Add `using Duskborn.Gameplay;`.

**Rename `TriggerCleave` to `ExecuteCleave`** (interface contract):

```csharp
// Was: public void TriggerCleave(float range, float arcDegrees, float damageMultiplier)
public void ExecuteCleave(float range, float arcDegrees, float damageMultiplier)
{
    if (!_stats.IsAlive) return;
    RequestCleaveRpc(range, arcDegrees, damageMultiplier);
}
```

**Update `BuildContext()`** to return `CombatContext`:

```csharp
private CombatContext BuildContext()
{
    var bar = actionBarInstaller?.Service;
    return new CombatContext(this, _stats, bar, bar?.SelectedIndex ?? 0, _weaponActionPlayer);
}
```

Update the return type annotation and any `ActionContext` local variable types.

---

### 10. `Assets/_Duskborn/Gameplay/Enemies/EnemyBase.cs`

**Implement `ICombatEntity`:**

```csharp
public abstract class EnemyBase : NetworkBehaviour, ICombatEntity
```

Add `using Duskborn.Gameplay;`.

**Change the skills field from `EnemySkill[]` to `WeaponSkill[]`:**

```csharp
[SerializeField] private WeaponSkill[] skills;
```

Add `using Duskborn.Gameplay.Equipment;`.

**Update `BuildContext()`** to return `CombatContext`:

```csharp
private CombatContext BuildContext() => new(this, CurrentTarget);
```

**Update `TryUseSkill()`** — the call signature is unchanged since `WeaponSkill` still has
`cooldown`, `CanUse` no longer exists (it was on `EnemySkill`). Range checking must move into
the enemy loop or skills must handle it themselves.

Because `WeaponSkill` has no `CanUse`, `EnemyBase` needs its own range check before calling `Use`.
You have two options — pick one:

**Option A — Range check on `WeaponSkill`** (recommended):
Add a virtual method to `WeaponSkill`:
```csharp
// In WeaponSkill.cs:
[SerializeField] public float range = 2f;

public virtual bool CanUse(CombatContext ctx)
{
    // Default: pass if no target (player context) or target in range (enemy context).
    if (ctx.Target == null) return true;
    return Vector3.Distance(ctx.Caster.transform.position, ctx.Target.position) <= range;
}
```

Then `EnemyBase.TryUseSkill` calls `skills[i].CanUse(ctx)` — same as before.

**Option B — Range check stays in EnemyBase** — simpler if you don't want a `range` field on every
player skill. Add a `[SerializeField] public float range` to `EnemySkillSlot` wrapper instead.
(More invasive, skip for now.)

Go with **Option A**.

`CleaveSkill` already has a `range` field for its arc logic. The `CanUse` default uses this same
`range` for the distance check, which is exactly the right behavior.

---

### 11. `Assets/_Duskborn/Gameplay/Items/AppleItem.cs`

If it implements `ILeftClickAction`, update the signature:

```csharp
public void OnLeftClick(CombatContext ctx) { … }
```

Add `using Duskborn.Gameplay;`.

---

## Editor steps after the code compiles

1. **Enemy prefabs** — the old `EnemySkill[]` field is gone. In the Swarmer (and any future
   enemy), find the **Skills** array on the `EnemyBase` component. Assign `CleaveSkill.asset`
   (the same asset already used by the axe). Unity's serialized type changed so the slot will
   be empty — re-drag the asset.

2. **`CleaveSkill.asset`** — now has a `range` field from `WeaponSkill` used by `CanUse` for
   enemies. Set it to the melee/cleave range you want (e.g. 3). The player ignores `CanUse`
   entirely since it dispatches through `TryWeaponSkill` with its own logic.

3. **Delete the old `CleaveEnemySkill.asset`** if one was created — it will be stale.

---

## Compile-order checklist

Run through these in order to avoid chasing cascading errors:

- [ ] `ICombatEntity.cs` created
- [ ] `CombatContext` class created (replaces `ActionContext` struct)
- [ ] `IActionBarActions.cs` updated (interfaces use `CombatContext`)
- [ ] `WeaponSkill.cs` updated (`Use(CombatContext)`, `range`, `CanUse`)
- [ ] `WeaponBehaviour.cs` updated
- [ ] `MeleeWeaponBehaviour.cs` updated
- [ ] `WeaponItem.cs` updated
- [ ] `WeaponActionPlayer.cs` updated
- [ ] `CleaveSkill.cs` updated
- [ ] `PlayerCombat.cs` updated (implements ICombatEntity, ExecuteCleave)
- [ ] `EnemyBase.cs` updated (implements ICombatEntity, WeaponSkill[])
- [ ] `AppleItem.cs` updated
- [ ] Delete `EnemySkill.cs`, `EnemyActionContext.cs`, `CleaveEnemySkill.cs`
