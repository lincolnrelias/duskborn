# Duskborn — Development Progress Tracker
> Last updated: 2026-04-28 (session 5)
> Version: 0.1-dev
> Engine: Unity 6 (URP 17.0.4) · FishNet · FishySteamworks · Steamworks.NET

---

## Legend
- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete
- `[!]` Blocked / needs decision

---

## Open Decisions (from GDD §11)
| # | Question | Priority | Status |
|---|----------|----------|--------|
| 1 | Exact day length (real-time minutes per day) | High | Open |
| 2 | Gold costs per chest tier | High | Open |
| 3 | Echo amounts per run outcome | High | Open |
| 4 | Armor slots: full set (head/chest/legs) vs single rating | Medium | Open |
| 5 | Enemy count + HP scaling formula (2–5 players) | High | Open |
| 6 | Revive countdown duration | Medium | Open |
| 7 | Pixel art icon resolution (16×16 vs 32×32) | Medium | Open |
| 8 | Steam Cloud save for meta-progression | Low | Open |
| 9 | Late join — confirm disabled in v1 | Low | Decided: DISABLED |
| 10 | Final name for meta-currency (placeholder: Echoes) | Low | Open |

---

## Phase 0 — Technical Foundation
> Goal: Runnable project with networking skeleton and deterministic RNG.
> Status: [~] IN PROGRESS

### 0.1 Project Setup
- [x] Unity project confirmed at URP (17.0.4)
- [x] FishNet imported (confirmed — .csproj files present in project root)
- [ ] FishySteamworks imported
- [ ] Steamworks.NET imported
- [ ] Scene hierarchy defined: Bootstrap / MainMenu / Lobby / Game / UI
- [x] Folder structure created: `Assets/_Duskborn/{Core,Network,Gameplay,UI,Data,Art,...}`

### 0.2 Deterministic RNG
- [x] `SeededRNG.cs` — wraps System.Random, all calls routed through it
- [x] `GameSession.cs` — singleton holding seed + RNG instance
- [ ] Unit test: same seed → identical 1000-call sequence on two instances
- [ ] No `UnityEngine.Random` calls in gameplay code (enforced by convention)

### 0.3 P2P Network Skeleton
- [ ] `NetworkManager` scene object configured (FishNet)
- [ ] `GameNetworkManager.cs` — connection lifecycle (host, join, disconnect, scene transition)
- [ ] Steam lobby: create lobby, join by code, join by invite
- [ ] FishySteamworks transport configured
- [ ] Smoke test: 2 instances connect, sync a cube position

### 0.4 Scene Management
- [ ] Scene list: Bootstrap, MainMenu, Lobby, Game, UI (additive)
- [x] `SceneLoader.cs` — static scene name constants + async loader (network version Phase 9)
- [ ] Bootstrap scene created, auto-loads MainMenu on startup

---

## Phase 1 — Core Game Loop
> Goal: Day → Night → Enemies → Death → Win/Lose. No classes, no loot.
> Status: [~] IN PROGRESS

### 1.1 Player Controller
- [x] `PlayerController.cs` — WASD movement; right-click drag orbit camera (fixed-angle, no feedback loop)
- [x] `PlayerStats.cs` — HP, damage, speed, attackSpeed; multiplier system; PlayerRegistry auto-register; death → GameOver
- [x] `PlayerRegistry.cs` — static cache; FindNearest + FindMostIsolated; Clear() removed from Bootstrapper (was wiping OnEnable registrations)
- [ ] Animation states: Idle, Walk, Run, Attack, Hit, Die
- [x] Player prefab assembled in Unity (CharacterController + PlayerInput + components)

### 1.2 Day/Night Cycle
- [x] `DayNightCycle.cs` — singleton, Night# (1–7), Phase, timer, lighting transition
- [x] Events: `OnDayStart`, `OnNightStart`, `OnNightEnd`
- [x] Night 7: safety timer suspended (`BeginBossNight` — fight runs until boss dies or all die)
- [x] `ForceEndCurrentPhase()` — skips current phase regardless of type (used by debug F1)
- [x] Night does NOT end early when all enemies die — runs full timer (design decision)
- [ ] Countdown timer UI (center-top during Day)
- [ ] Directional Light + skybox Gradient wired in Unity Inspector

### 1.3 Basic Enemy — Swarmer
- [x] `EnemyBase.cs` — HP (non-compounding scaling), PlayerRegistry targeting, melee attack, pool-safe reset; fixed SetActive/Agent.enabled order; Agent.Warp on spawn; GoldManager.AddGold on death
- [x] `EnemyPool.cs` — Queue pool; clears event subs on reset; exposes aggregate OnAnyEnemyDied; Initialize() for runtime construction
- [x] `Swarmer.cs` — subclass of EnemyBase
- [x] `EnemyType.cs` — enum (Swarmer/Runner/Spitter/Brute/Elite)
- [x] Swarmer prefab assembled in Unity (NavMeshAgent + Enemy layer + EnemyBase on root)

### 1.4 Wave Spawner — Budget + Timeline System
- [x] `EnemySpawnPool.cs` (SO) — named set of {EnemyType, Cost, Weight} entries; pools are pure data
- [x] `NightDefinition.cs` (SO) — budget per night + which pools are active; replaces WaveDefinition
- [x] `SpawnEvent.cs` / `SpawnTimeline.cs` — data classes: (timestamp, enemyType) events, sorted list
- [x] `TimelineGenerator.cs` — static; spends budget, shuffles, distributes with per-slot jitter
- [x] `WaveManager.cs` — registry-driven; builds pools internally; radius-based spawn positions
- [x] `EnemyPrefabRegistry.cs` (SO) — maps EnemyType → prefab + pool size; single source of truth
- [x] `EnemyPool.cs` — supports both Inspector config and runtime Initialize(prefab, size)
- [x] `SpawnPerimeter.cs` — retired (radius-based annulus in WaveManager replaces it)
- [x] `WaveDefinition.cs` — retired (empty stub, GUID preserved)
- [x] `EnemyPrefabRegistry` asset created + Swarmer prefab assigned
- [x] `EnemySpawnPool` asset created (Night 1 basic pool)
- [x] `NightDefinition` asset created for Night 1
- [x] WaveManager configured in scene (registry, definitions, spawn radius)
- [x] Alive count bug fixed: pool subscription moved to BuildPools (once per pool, not per spawn); HandleEnemyDied guards with _waveActive
- [ ] NightDefinitions for Nights 2–6 (deferred — need more enemy types first)

### 1.5 Win / Lose Conditions
- [x] `GameStateManager.cs` — GameOver / Win / Running states
- [x] `GameBootstrapper.cs` — starts GameStateManager + DayNightCycle + resets GoldManager on load
- [x] GameOver auto-triggers: PlayerStats.HandleDeath → PlayerRegistry.AliveCount == 0
- [ ] Win trigger: stub boss for Night 7
- [ ] End screen (placeholder)

### 1.6 Debug & HUD (placeholder — replaces Phase 10 UI until polish pass)
- [x] `GameDebugController.cs` — F1 skip day, F2 end night, F3 damage player, F4 print timeline
- [x] `GameHUD.cs` — OnGUI overlay: phase, night, timer, state, gold, enemies alive/queued, per-player HP

---

## Phase 2 — Classes & Combat
> Goal: 3 playable classes with abilities, passives, and status effects.
> Status: [~] IN PROGRESS

### 2.0 Basic Combat ✅ VERIFIED
- [x] `PlayerCombat.cs` — left-click OverlapSphere, cooldown from AttackSpeed, crit roll, GetComponentInParent for child colliders
- [x] Input: Input.GetMouseButtonDown(0) primary + OnAttack(InputValue) Send Messages fallback
- [x] Enemy layer set + assigned; player prefab has PlayerCombat + enemyLayer wired
- [x] Gold accumulates on kill (GoldManager), counter visible in HUD

### 2.1 Class Architecture
- [x] `ClassDefinition.cs` (ScriptableObject) — name, base stats (HP/speed/damage/attackSpeed)
- [x] `PlayerClass.cs` — reads ClassDefinition, calls SetBaseStats on Start
- [x] `ClassAbility.cs` — abstract base: ModifyDamage, OnAttackCompleted, OnAttackMissed, TryUseAbility
- [x] `PlayerCombat.cs` — calls ClassAbility hooks; Q key triggers active ability
- [ ] ClassDefinition assets created in Unity (Warrior / Ranger / Mage SO assets)
- [ ] Class selection in Lobby UI (Phase 9+)

### 2.2 Warrior ✅ VERIFIED
- [x] Passive: consecutive hit stack (+8% per hit, max 3×, resets on miss or target switch)
- [x] Cleave (Q): 180° arc OverlapSphere, hits all enemies in front, 4s cooldown
- [x] `WarriorClass.cs`
- [x] enemyLayer assigned; stack behavior verified in play

### 2.3 Ranger — SKELETON
- [x] `RangerClass.cs` — first-shot crit passive wired; Rain of Arrows stubbed
- [ ] Projectile system (deferred — post items/crafting)
- [ ] Movement speed passive (deferred)

### 2.4 Mage — SKELETON
- [x] `MageClass.cs` — Arcane Burst stubbed
- [ ] Mana resource (deferred — post items/crafting)
- [ ] Spell system (deferred)

### 2.5 Status Effects
- [ ] Deferred — post items/crafting

### 2.6 Combat Math
- [ ] Floating damage numbers (deferred)
- [ ] Hit flash VFX (deferred)

---

## Phase 3 — Loot, Economy & Crafting
> Status: [~] IN PROGRESS

### 3.1 Gold System
- [x] `GoldManager.cs` — shared pool; AddGold/TrySpend/ResetGold/OnGoldChanged; host-authoritative in Phase 9
- [x] Gold awarded directly on enemy death (EnemyBase → GoldManager.AddGold)
- [x] Gold counter HUD
- [x] Gold resets to 0 on run start (GameBootstrapper)
- [ ] Gold drop: world pickup collectable (deferred — direct award sufficient for now)

### 3.2 Item System
- [x] `ItemDefinition.cs` (ScriptableObject) — name, rarity, effect type, effect value
- [x] `ItemRarity` enum: Common, Uncommon, Rare, Legendary, Cursed
- [x] `ItemEffectType` enum: BonusDamage, BonusHP, BonusMoveSpeed, BonusAttackSpeed, BonusCritChance, DamageReduction
- [x] `PlayerInventory.cs` — List<ItemDefinition>; AddItem(); ApplyAll() resets + re-applies all multipliers to PlayerStats
- [x] Item count shown in HUD
- [ ] ItemDefinition SO assets created in Unity (manual setup step)
- [ ] `PlayerInventory` added to player prefab (manual setup step)

### 3.3 Chest System
- [x] `Chest.cs` — E to interact (proximity trigger), gold cost, flat-random loot table, one-use, disables on open
- [ ] Chest prefab built in Unity (mesh + SphereCollider IsTrigger + Chest component)
- [ ] Placed in scene for verification
- [ ] Rarity-weighted rolls per chest tier (deferred)
- [ ] Chest UI: cost prompt (deferred)

### 3.4 Resource Gathering
- [x] `ResourceType.cs` — enum: Wood, Stone, Fiber, Iron
- [x] `ResourceType.cs` — enum: Wood, Stone, Fiber, Iron
- [x] `ResourceNode.cs` — HP (hit count), drops on depletion via SeededRNG; GreenOutline rendering layer support
- [x] `ResourceInventory.cs` — per-player Dictionary<ResourceType,int>; Add/TrySpend/GetCount
- [x] `AttackRangeTrigger.cs` — child-object event forwarder; SphereCollider (IsTrigger) defines melee range
- [x] `PlayerCombat.cs` — subscribes to AttackRangeTrigger; tracks nodes in range; outline on closest; hits on left-click
- [x] Resource HUD widget — Wood/Stone/Fiber/Iron counts in GameHUD

### 3.5 Workbench & Crafting
- [ ] `Workbench.cs` — interactable, opens crafting panel
- [ ] `CraftingRecipe.cs` (ScriptableObject) — required resources, output item
- [ ] Tier 1 recipes (Wood, Stone, Fiber)
- [ ] Tier 2 recipes (Iron Ore required)
- [ ] Tier 3 recipes (Thornbark Core required — unlocked post-boss)
- [ ] Gear slots: Weapon (1), Armor (1), Accessory (1)
- [ ] Crafting UI panel

---

## Phase 4 — World Generation
> Status: [ ] NOT STARTED

### 4.1 Seed Distribution
- [ ] Host generates seed, broadcasts before scene load
- [ ] All clients init `SeededRNG` with same seed before `WorldGenerator.Generate()`
- [ ] Validation test: two instances generate identical worlds from same seed

### 4.2 Terrain Layout
- [ ] `WorldGenerator.cs` — chunked map grid
- [ ] Chunk types: Plains, Forest, Rock, River
- [ ] Tree + resource node placement in Forest chunks
- [ ] Rock formations (stone/iron nodes, natural barriers)
- [ ] Player spawn: central clearing, guaranteed flat

### 4.3 Points of Interest
- [ ] Workbench site prefab chunks at seeded positions
- [ ] Shrine spawn points (min 3, seeded)
- [ ] Chest spawn points (seeded, tier by distance from spawn)
- [ ] Runtime NavMesh bake after terrain gen

### 4.4 Enemy Spawn Perimeter
- [ ] Map bounds defined
- [ ] `SpawnPerimeter.cs` — N evenly-spaced spawn points at map edge
- [ ] WaveManager reads SpawnPerimeter for night spawning

---

## Phase 5 — Enemy Roster & Night Escalation
> Status: [ ] NOT STARTED

### 5.1 Enemy Archetypes
- [ ] Runner — high speed, targets most-isolated player
- [ ] Spitter — ranged projectile, keeps distance
- [ ] Brute — high HP, slow, bodyblock
- [ ] Elite — named unit, very high HP/damage, guaranteed gold drop

### 5.2 Night Escalation Table
- [ ] Nights 1–7 WaveDefinitions created
- [ ] Night 1: Swarmers only | Night 2: +Runners | Night 3: +Spitters | Night 4: +Brutes | Night 5: +Elite | Night 6: all types max density | Night 7: Boss

### 5.3 Player Count Scaling
- [ ] Enemy count formula applied in WaveManager
- [ ] Enemy HP formula applied on spawn
- [ ] Validated across 1, 2, 3, 4, 5 player counts

### 5.4 Revival System
- [ ] `DownedState.cs` — entered at 0 HP (not instant death)
- [ ] Countdown timer, immobile, can't attack
- [ ] Revive check: no enemies within radius → teammate can revive
- [ ] Full death on countdown expiry
- [ ] GameOver check after each death event

---

## Phase 6 — Boss Fight (Thornbark)
> Status: [ ] NOT STARTED

- [ ] `BossBase.cs` extending EnemyBase — phase tracking at 50% HP
- [ ] Boss health bar (full-width HUD)
- [ ] Boss arena (flat clearing)
- [ ] Phase 1: Stomp, VineWhip, RootEruption (with telegraphs)
- [ ] Phase 2: All P1 faster + SeedlingSwarm + ThornBarrage
- [ ] Seedling spawner (uses enemy pool)
- [ ] Co-op pressure: seedling count above threshold → escalating difficulty
- [ ] Run completion → Echoes award trigger

---

## Phase 7 — Shrines
> Status: [ ] NOT STARTED

- [ ] `Shrine.cs` base — interactable, one-time use, state tracking
- [ ] Buff Shrine: random buff from pool, applied permanently to interacting player
- [ ] Buff pool: +15% move speed, lifesteal on hit, +1 item to next chest (min 3 buffs)
- [ ] Challenge Shrine: mini-event (kill X or survive Y), reward on success

---

## Phase 8 — Meta-Progression
> Status: [ ] NOT STARTED

- [ ] `MetaProgressionManager.cs` — local save/load (JSON)
- [ ] Echoes balance tracked per player
- [ ] End-run Echoes award (formula: nights survived / win)
- [ ] `SkillTreeData.cs` (ScriptableObject) — 5 nodes, Echo cost, effect per class
- [ ] `PlayerMetaData.cs` — serializable, per-class unlocked node indices
- [ ] Skill tree UI: pixel art nodes, lock/unlock, purchase flow
- [ ] `SaveManager.cs` — serialize to `Application.persistentDataPath`
- [ ] `PlayerClass.cs` applies unlocked nodes on run start

---

## Phase 9 — Multiplayer Sync
> Status: [ ] NOT STARTED

- [ ] `NetworkPlayer.cs` — syncs position, rotation, animation
- [ ] `NetworkedPlayerStats.cs` — HP, mana (owner writes, others observe)
- [ ] Class selection synced at lobby
- [ ] Enemies host-authoritative: position/HP/state replicated
- [ ] Status effects applied by host, replicated
- [ ] `NetworkGoldManager.cs` — ServerRpc for spend, gold pool synced
- [ ] Chest/Shrine use validated by host via ServerRpc
- [ ] Day/Night cycle: host-authoritative, phase changes broadcast
- [ ] Wave start/end: host spawns, broadcasts
- [ ] Revival: host validates, grants

---

## Phase 10 — UI/UX
> Status: [ ] NOT STARTED

- [ ] HP bar (chunky pixel blocks)
- [ ] Mana bar (Mage only)
- [ ] Gold counter
- [ ] Night counter + Day countdown timer
- [ ] Downed player indicators
- [ ] Item pickup notification (toast)
- [ ] Inventory panel
- [ ] Crafting UI
- [ ] Skill tree UI (pixel art)
- [ ] Main menu
- [ ] Lobby screen
- [ ] End screen

---

## Phase 11 — Polish & Audio
> Status: [ ] NOT STARTED

- [ ] Custom URP cel/flat shader on all 3D meshes
- [ ] Day/Night/Boss palette transitions
- [ ] Hit flash, death burst, ability VFX
- [ ] Status effect VFX (Burn, Freeze, Shock)
- [ ] Shrine VFX
- [ ] Day/Night/Boss music + adaptive transitions
- [ ] SFX: footsteps, melee, ranged, spell, enemy attack, enemy death, UI

---

## Phase 12 — Steam & Launch Prep
> Status: [ ] NOT STARTED

- [ ] Steam lobby (create, join by code, friend invite)
- [ ] Steam Overlay
- [ ] Achievements (list TBD)
- [ ] Steam Cloud save (optional v1)
- [ ] Balance pass (close all Open Decisions)
- [ ] Final playtests (1-player, 2-player, 5-player)

---

## Packages Installed
| Package | Version | Status |
|---------|---------|--------|
| Unity URP | 17.0.4 | Installed |
| Unity InputSystem | 1.13.1 | Installed |
| Unity AI Navigation | 2.0.6 | Installed |
| FishNet | — | Installed (confirmed) |
| FishySteamworks | — | MISSING — install after FishNet |
| Steamworks.NET | — | MISSING — install after FishySteamworks |

## Files Written (Session 7 — 2026-04-29, resource gathering)
| File | Change |
|------|--------|
| `Gameplay/Loot/ResourceType.cs` | NEW — enum: Wood, Stone, Fiber, Iron |
| `Gameplay/Loot/ResourceInventory.cs` | NEW — per-player resource counts; Add/TrySpend/GetCount |
| `Gameplay/Loot/ResourceNode.cs` | NEW — world object; hit-count HP, SeededRNG drop, GreenOutline support |
| `Gameplay/Player/AttackRangeTrigger.cs` | NEW — child-object trigger forwarder; drives node detection + outline |
| `Gameplay/Player/PlayerCombat.cs` | AttackRangeTrigger ref; node set tracking; outline on closest; hit on attack |
| `Gameplay/Player/PlayerInteractor.cs` | Reverted to chests-only (resource detection moved to PlayerCombat) |
| `UI/GameHUD.cs` | Added ResourceInventory cache; Wood/Stone/Fiber/Iron lines in HUD |

## Files Written (Session 6 — 2026-04-28, items + chest system)
| File | Change |
|------|--------|
| `Gameplay/Loot/ItemDefinition.cs` | NEW — SO: ItemRarity/ItemEffectType enums + EffectValue |
| `Gameplay/Loot/LootTable.cs` | NEW — SO: ItemDefinition[] array; assigned to Chest |
| `Gameplay/Loot/PlayerInventory.cs` | NEW — holds items, ApplyAll() writes stat multipliers |
| `Gameplay/Loot/Chest.cs` | NEW — data + TryOpen(PlayerInventory); no trigger, no input |
| `Gameplay/Player/PlayerInteractor.cs` | NEW — player-side SphereCollider trigger; closest-chest link; E to open |
| `UI/GameHUD.cs` | Added Items count line + PlayerInventory cache in Start |

## Files Written (Session 4 — 2026-04-28, pool + spawn cleanup)
| File | Change |
|------|--------|
| `Gameplay/Enemies/EnemyPrefabRegistry.cs` | NEW — SO mapping EnemyType → prefab + pool size |
| `Gameplay/Enemies/WaveManager.cs` | Removed 5 pool fields; builds pools from registry; radius-based spawn |
| `Gameplay/Enemies/EnemyPool.cs` | Added Initialize(prefab, size) for runtime construction |
| `Gameplay/World/SpawnPerimeter.cs` | Retired — replaced by min/max radius annulus in WaveManager |

## Files Written (Session 3 — 2026-04-28, wave system redesign)
| File | Change |
|------|--------|
| `Gameplay/Enemies/EnemySpawnPool.cs` | NEW — named pool of {EnemyType, Cost, Weight} entries |
| `Gameplay/Enemies/NightDefinition.cs` | NEW — replaces WaveDefinition; budget + pool refs |
| `Gameplay/Enemies/SpawnTimeline.cs` | NEW — SpawnEvent (timestamp, type) + SpawnTimeline data class |
| `Gameplay/Enemies/TimelineGenerator.cs` | NEW — budget spender + timestamp distributor |
| `Gameplay/Enemies/WaveManager.cs` | Rewritten — generates timeline, observes in Update |
| `Gameplay/Enemies/WaveDefinition.cs` | Retired — empty stub (GUID preserved) |

## Files Written (Session 2 — 2026-04-27, bug fixes)
| File | Change |
|------|--------|
| `Core/GameSession.cs` | Removed self-referential using; seed uses TickCount not UnityEngine.Random |
| `Core/DayNightCycle.cs` | Night 7 suspends timer (BeginBossNight); no silent stop on expired timer |
| `Core/GameBootstrapper.cs` | NEW — starts GameStateManager + DayNightCycle on scene load |
| `Gameplay/Player/PlayerStats.cs` | Registers with PlayerRegistry; death checks AliveCount → GameOver |
| `Gameplay/Player/PlayerRegistry.cs` | NEW — static player cache; FindNearest + FindMostIsolated |
| `Gameplay/Enemies/EnemyType.cs` | NEW — EnemyType enum |
| `Gameplay/Enemies/EnemyBase.cs` | Non-compounding scaling; PlayerRegistry targeting; gold uses SeededRNG; ResetEnemy nulls events |
| `Gameplay/Enemies/EnemyPool.cs` | Re-subscribes pool handler after reset; aggregate OnAnyEnemyDied event |
| `Gameplay/Enemies/WaveDefinition.cs` | EnemyType enum instead of EnemyPool scene ref |
| `Gameplay/Enemies/WaveManager.cs` | Owns EnemyType→Pool map; subscribes to pool aggregates; unsubscribes cleanly |

## Files Written (Session 1 — 2026-04-27)
| File | System | Phase |
|------|--------|-------|
| `Core/SeededRNG.cs` | Deterministic RNG | 0.2 |
| `Core/GameSession.cs` | Session singleton + seed holder | 0.2 |
| `Core/GameStateManager.cs` | Win/Lose/Running state machine | 1.5 |
| `Core/DayNightCycle.cs` | Day/Night timer, events, lighting | 1.2 |
| `Core/SceneLoader.cs` | Scene name constants + async loader | 0.4 |
| `Gameplay/Player/PlayerStats.cs` | HP, damage, speed, multiplier system | 1.1 |
| `Gameplay/Player/PlayerController.cs` | WASD movement, third-person camera | 1.1 |
| `Gameplay/Enemies/EnemyBase.cs` | NavMesh AI, melee attack, death, pool interface | 1.3 |
| `Gameplay/Enemies/EnemyPool.cs` | Queue-based enemy pool | 1.3 |
| `Gameplay/Enemies/Swarmer.cs` | Night 1 enemy (uses EnemyBase defaults) | 1.3 |
| `Gameplay/Enemies/WaveDefinition.cs` | ScriptableObject: wave composition per night | 1.4 |
| `Gameplay/Enemies/WaveManager.cs` | Spawns waves, tracks alive count, ends night | 1.4 |
| `Gameplay/World/SpawnPerimeter.cs` | Perimeter spawn points with gizmo | 1.4 |

---

## Folder Structure (target)
```
Assets/
  _Duskborn/
    Core/           ← GameStateManager, DayNightCycle, SeededRNG, SceneLoader
    Network/        ← GameNetworkManager, NetworkPlayer, NetworkGoldManager
    Gameplay/
      Player/       ← PlayerController, PlayerStats, PlayerClass, PlayerInventory
      Enemies/      ← EnemyBase, EnemyPool, Swarmer, Runner, Spitter, Brute, Elite
      Classes/      ← Warrior, Ranger, Mage components + ClassDefinition SOs
      Combat/       ← StatusEffectSystem, DamageSystem, ProjectilePool
      Loot/         ← GoldManager, ItemDefinition, PlayerInventory, Chest, ResourceNode
      Crafting/     ← Workbench, CraftingRecipe SOs
      World/        ← WorldGenerator, SpawnPerimeter, SeededRNG
      Boss/         ← BossBase, Thornbark
      Shrines/      ← Shrine, BuffShrine, ChallengeShrine
    UI/             ← HUD, InventoryPanel, CraftingPanel, SkillTreeUI, LobbyUI, EndScreen
    Data/           ← All ScriptableObjects (items, recipes, classes, waves, skills)
    Meta/           ← MetaProgressionManager, SaveManager, PlayerMetaData
    Art/            ← Shaders, VFX, Materials
```
