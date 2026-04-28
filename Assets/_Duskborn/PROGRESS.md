# Duskborn — Development Progress Tracker
> Last updated: 2026-04-27 (session 2)
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
- [x] `PlayerController.cs` — WASD movement, third-person camera
- [x] `PlayerStats.cs` — HP, damage, speed, multiplier system; registers with PlayerRegistry; death triggers GameOver check
- [x] `PlayerRegistry.cs` — static cache of alive players; FindNearest + FindMostIsolated
- [ ] Animation states: Idle, Walk, Run, Attack, Hit, Die (need Animator + clips)
- [ ] Placeholder mesh + prefab assembled in Unity
- [ ] Health bar HUD (placeholder)

### 1.2 Day/Night Cycle
- [x] `DayNightCycle.cs` — singleton, Night# (1–7), Phase, timer, lighting transition
- [x] Events: `OnDayStart`, `OnNightStart`, `OnNightEnd`
- [x] Night 7: safety timer suspended (boss fight runs until boss dies or all die)
- [ ] Countdown timer UI (center-top during Day)
- [ ] Directional Light + skybox Gradient wired in Unity Inspector

### 1.3 Basic Enemy — Swarmer
- [x] `EnemyBase.cs` — HP (non-compounding scaling), PlayerRegistry targeting, melee attack, pool-safe reset
- [x] `EnemyPool.cs` — Queue pool; clears event subs on reset; exposes aggregate OnAnyEnemyDied
- [x] `Swarmer.cs` — subclass of EnemyBase
- [x] `EnemyType.cs` — enum (Swarmer/Runner/Spitter/Brute/Elite)
- [ ] Swarmer prefab assembled in Unity (NavMeshAgent + placeholder mesh)

### 1.4 Wave Spawner
- [x] `WaveDefinition.cs` (ScriptableObject) — pure data; uses EnemyType enum (no scene refs)
- [x] `WaveManager.cs` — owns EnemyType→EnemyPool map; subscribes to aggregate pool events; no per-enemy leaks
- [x] `SpawnPerimeter.cs` — perimeter spawn points, gizmo visualizer
- [ ] Night 1 WaveDefinition asset created in Unity
- [ ] Pool GameObjects assigned in WaveManager Inspector

### 1.5 Win / Lose Conditions
- [x] `GameStateManager.cs` — GameOver / Win / Running states
- [x] `GameBootstrapper.cs` — starts GameStateManager + DayNightCycle on scene load
- [x] GameOver auto-triggers: PlayerStats.HandleDeath checks PlayerRegistry.AliveCount == 0
- [ ] Win trigger: stub boss for Night 7 (calls GameStateManager.TriggerWin on death)
- [ ] End screen (placeholder)

---

## Phase 2 — Classes & Combat
> Goal: 3 playable classes with abilities, passives, and status effects.
> Status: [ ] NOT STARTED

### 2.1 Class Architecture
- [ ] `ClassDefinition.cs` (ScriptableObject) — name, base stats, passive, ability data
- [ ] `PlayerClass.cs` — holds ClassDefinition, applies stats on Start
- [ ] Class selection in Lobby UI
- [ ] Class synced to all clients before run start

### 2.2 Warrior
- [ ] Melee hitbox (180° arc, short range)
- [ ] Passive: consecutive hit stack (+8% per hit, max 3×, reset on miss/target switch)
- [ ] Cleave: 180° arc, all enemies in front, short cooldown
- [ ] Swing VFX + hit flash

### 2.3 Ranger
- [ ] Projectile system (pooled arrow prefabs)
- [ ] Passive: first shot on new target = guaranteed crit; 2s movement → +10% atk speed
- [ ] Rain of Arrows: brief channel → wide arc volley

### 2.4 Mage
- [ ] Mana resource + ManaBar HUD
- [ ] Spell attack (mana cost)
- [ ] Passive: kill → restore mana; crit → random status (Burn/Freeze/Shock)
- [ ] Arcane Burst: short-range AoE explosion

### 2.5 Status Effects
- [ ] `StatusEffectSystem.cs` — component on all damageable entities
- [ ] Burn: DoT, spreads if stacked
- [ ] Freeze: immobilize, shatters on next hit
- [ ] Shock: reduces armor/resistance
- [ ] Slow: reduces NavMesh agent speed
- [ ] Stun: disables agent + attack briefly

### 2.6 Combat Math
- [ ] Damage formula (base × weapon tier × modifiers × crit)
- [ ] Crit system (base 0%, boosted by items/skills; 1.5× multiplier)
- [ ] Floating damage numbers (pixel art style)

---

## Phase 3 — Loot, Economy & Crafting
> Status: [ ] NOT STARTED

### 3.1 Gold System
- [ ] `GoldManager.cs` — shared pool, host-authoritative
- [ ] Gold drop: enemy death → spawn pickup collectable
- [ ] Gold pickup: player overlap → add to pool
- [ ] Gold counter HUD
- [ ] Gold resets to 0 on run start

### 3.2 Item System
- [ ] `ItemDefinition.cs` (ScriptableObject) — name, rarity, icon, effect type, values
- [ ] Rarity enum: Common, Uncommon, Rare, Legendary, Cursed
- [ ] `PlayerInventory.cs` — list of held items (passive stacking)
- [ ] `ItemEffectApplier.cs` — reads inventory, applies stat multipliers / triggers

### 3.3 Chest System
- [ ] `Chest.cs` — interactable, gold cost, tier (Basic/Advanced/Legendary)
- [ ] Rarity roll on open (weighted by chest tier)
- [ ] Chest UI: gold cost, confirm/cancel
- [ ] Chest placed by WorldGenerator (Phase 4)

### 3.4 Resource Gathering
- [ ] `ResourceNode.cs` — Wood/Stone/Fiber/Iron, HP, drops on hit
- [ ] Player interaction: swing animation → node HP decrements → drop
- [ ] `ResourceInventory.cs` — per-player counts
- [ ] Resource HUD widget

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
