# Engineering Audit — Duskborn — 2026-10-01

This audit used source inspection and incremental refactoring. It mapped project-owned systems in `Assets/_Duskborn` and `Assets/Inventory/Scripts` (283 C# files in the inventory, including Editor files). This count describes the search scope, rather than a line-by-line review of every file. The analysis investigated the flows below and consulted FishNet source to confirm pool activation order. External packages were not refactored.

The checkout already contained many changes, primarily translations. Concurrent editing occurred during the audit; some fixes were overwritten and reapplied to the updated content. Existing changes were preserved. The audit did not commit or modify scenes, prefabs, assets, or serialized fields.

## 1. Discovery: How the Game Works

- **Entry and session:** `MainMenuController` loads the gameplay scene; `GameSession` holds the seed/RNG; `NetworkBootstrapper` chooses a local host through a mutex. `GameStateManager` publishes state and survived nights through SyncVars. `InGameMenuController` persists across scenes and observes `sceneLoaded`.
- **World:** `ChunkGridManager` creates terrain chunks, cooks colliders, generates water/props/foliage, and prepares NavMesh. Generation uses coroutines and a budget between chunks. The FishNet spawner readiness gate waits for world completion. The current path uses `SampleScene`; `SceneLoader` retains names from an earlier flow and was not used as a basis for scene migration.
- **Player and input:** movement/camera, combat/dodge, stats, and interaction components cooperate. `HotkeyManager` centralizes bindings; `LocalPlayerContext` holds the local owner's components; `PlayerRegistry` supplies players for target acquisition.
- **Combat and AI:** `WaveManager` consumes a timeline derived from night definitions. `EnemyPool` uses FishNet pooling; `EnemyBase` controls HP, target acquisition, navigation, attacks, death, and delayed return. Briarback, Thornwing, and Hollow Warden specialize behavior. Projectiles perform physics sweeps; the allocating fallback on buffer saturation preserves correctness.
- **Items and equipment:** `InventoryService` is engine-independent C# containing the grid, rules, and events. Backpack and action bar use instances of this service. Definitions create runtime items; `PlayerBuffContainer` recomposes stats through equipment/weapon contributors and relics.
- **Gathering and crafting:** death/depletion triggers `LootDropper`; `LootManager` creates network drops. Resources are delivered through TargetRpc to the client wallet, as explicitly intended by the code. Immediate crafting happens in the UI; forges produce through `PlacedBuilding`. Definitions and recipes are already data-driven and do not require conversion into new ScriptableObjects.
- **Building and persistence:** `BuildingWorld` validates/applies commands, maintains stations, publishes snapshots, and saves infrastructure with the host's wallet/discoveries. Payment uses client request/acknowledgment and server revalidation. Saving uses a temporary file and replacement with backup; loading validates content before applying it. This checkpoint is not a full session save.
- **Presentation and resources:** programmatic UI connects inventory, crafting, building, and HUD. Audio maintains persistent sources and a 3D pool. Damage, ghost, and afterimage effects use runtime materials/meshes, some with incomplete ownership.

No `Task`/`async` flow was found in the searched project-owned paths. The main waiting risks involve coroutines, callbacks, and shared state. This does not exclude internal package asynchrony.

## 2. Prioritized Findings

Each entry identifies evidence, scenario, solution, and risk. No finding was classified CRITICAL without evidence of impact at that scale. HIGH indicates substantial risk; it does not imply reproduction in a live session.

### F01 — HIGH — Reset Removes Death Listeners — Fixed

**Files:** `Gameplay/Enemies/EnemyBase.cs`, `EnemyPool.cs`, `Gameplay/Loot/LootDropper.cs`; consulted source: `ThirdPartyAssets/FishNet/Runtime/Utility/Performance/DefaultObjectPool.cs`.

`DefaultObjectPool.RetrieveObject` calls `SetActive(true)` before returning. `EnemyPool.Spawn` calls `ResetEnemy` afterward; reset assigned `OnDied = null`, removing the listener that `LootDropper.OnEnable` had just installed. The audio listener installed in Start was also removed on reuse.

**Scenario:** killing an instantiated/recycled enemy can lose loot and death callbacks, although the pool receives its own callback installed after reset.

**Applied solution:** preserve the event during reset; each subscriber manages its subscription. The pool removes its listeners even from already despawned objects when clearing its list or being destroyed. Reviewed subscribers perform explicit cleanup, including the boss coordinator. The dropper comment was corrected.

**Risk:** low to medium because this is a pooling/network boundary. Compilation was validated; loot/audio during actual spawning still require Unity/FishNet integration checks.

### F02 — HIGH — Crafting Charges without Delivery — Fixed for Failure Return Values

**Files:** `UI/CraftingUIManager.cs`, `Gameplay/Crafting/CraftingRecipe.cs`, `Inventory/Core/InventoryService.cs`.

The UI spent ingredients before calling the factory and ignored the return value of `TryAddItem`. An empty slot does not guarantee acceptance by rules. A missing installer was treated as available space. Resource callbacks can also fill the slot between payment and insertion.

**Scenario:** a recipe without valid runtime output, inventory rejecting the item, or a callback filling the final slot consumes resources while reporting success.

**Applied solution:** a local `CraftingRecipe` method creates output before payment, checks rules and slots, captures aggregated costs, checks insertion, and refunds the same charge when insertion returns false. The UI captures the selected recipe and registers icons before insertion notification. Material/processing paths and the previous equipment output quantity were preserved.

**Risk:** low to medium. Seven regressions cover delivery, full/missing inventory, missing factory/definition output, rule rejection, and reentrant slot occupancy. Arbitrary inventory callback exceptions and overflow caused by callbacks during refund are not handled as a general transaction; no rollback framework was introduced.

### F03 — MEDIUM — Removed Deadline Contaminates Reapplication — Fixed

**File:** `Gameplay/Combat/StatusEffectController.cs`.

`ServerRemove` removed the bit but retained `_endTimes`; `ServerApply` always took Max with that value. An indefinite effect removed and reapplied with finite duration remained indefinite. A removed long application also extended the next one.

**Applied solution:** clear deadlines for removed bits and extend an earlier deadline only when the bit remains active. Reapplying active effects still extends their duration.

**Risk:** low. Five duration/flag regressions passed. The current dodge path uses finite applications; no project-owned callers of `ServerRemove` were found, so the indefinite case is a confirmed API defect rather than a reproduction of permanent invulnerability in current gameplay.

### F04 — HIGH — Runtime Terrain Meshes Lack Explicit Ownership — Fixed in Runtime Path

**Files:** `Gameplay/World/TerrainChunk.cs`, `ChunkGridManager.cs`.

`BuildMesh` creates a native object; `ApplyMesh` replaced filter/collider references, and `ClearGrid` destroyed only GameObjects. Generated chunk meshes had no explicit disposal.

**Scenario:** regenerating the grid at runtime can leave previous meshes allocated until external resource cleanup. This is native resource retention risk, distinct from a temporary managed heap list.

**Applied solution:** `TerrainChunk` stores and destroys the mesh accepted by `ApplyMesh` during play, when replacing it and when destroying the chunk. Both reviewed generation callers pass `BuildMesh` output; meshes already serialized in scenes do not enter this ownership path.

**Risk:** medium. No serialized field changed. The API now assumes ownership of meshes assigned at runtime: future callers must not pass shared asset meshes. Edit-mode generation, water, and foliage resources remain in F05. Native memory was not measured during this audit.

### F05 — HIGH — Incomplete Water and Foliage Disposal — Documented

**Files:** `Gameplay/World/ChunkGridManager.cs` (`GenerateWaterPlane`, `RemoveWaterPlane`), `Foliage/ChunkFoliagePlacer.cs` (`ClearFoliage`, `CreateBatchGameObject`).

Water replaces a mesh created by `CreateWaterPlaneMesh`; foliage creates combined meshes and fallback materials. Cleanup destroys holders without explicit ownership/disposal of these resources. Memory cost depends on configuration and regeneration frequency.

**Scenario:** successive regenerations, especially dense foliage, accumulate orphaned native resources until external cleanup.

**Recommendation:** separate ownership for generator-created resources; release them on replacement/cleanup while preserving shared assets. Include memory captures before/after regeneration and reload. **Risk:** medium to high because the same code supports Editor and runtime generation/saving; do not destroy serialized meshes/materials based on inferred names.

### F06 — HIGH — Recalculation Accumulates Equipment Bonuses — Documented

**Files:** `Gameplay/Loot/PlayerInventory.cs` (`PlayerBuffContainer.ApplyAll`), `Gameplay/Equipment/PlayerEquipmentContainer.cs` (`ApplyBonus`), `Gameplay/Player/PlayerStats.cs`.

The equipment contributor adds LifestealBonus, ThornsDamageBonus, and GatheringSpeedBonus. Reset in `ApplyAll` does not zero these three properties before reapplying equipment.

**Scenario:** with gear containing these bonuses, switching weapons, equipping/removing pieces, or gathering relics triggers recalculation and adds the same bonus again. Removing a piece does not necessarily remove the accumulated value.

**Recommendation:** make recomposition idempotent with explicit gear and temporary bonus layers; test repeated ApplyAll and equipment removal. **Risk:** medium: directly resetting everything can erase temporary ThornsBuff, which shares the property in F07. Resolve both findings together without changing balance values.

### F07 — HIGH — Temporary Buffs Share Recomposed Accumulators — Documented

**Files:** `Gameplay/Player/PlayerCombat.cs` (`RequestApplyConsumableRpc`, `RemoveSpeedBuffLater`, `RemoveDamageBuffLater`), `Gameplay/Loot/PlayerInventory.cs`.

Consumables add to Speed/DamageBuffAdditive fields; `ApplyAll` clears and recomposes these fields. The expiry coroutine remains scheduled to subtract the consumable value afterward.

**Scenario:** drinking a buff and changing gear/weapons can erase it early; at expiry, the coroutine can subtract from a legitimate recomposed relic bonus. The result depends on action order.

**Recommendation:** store temporary modifiers separately and recompose from active modifiers; test overlap, equipping during duration, death, and expiry. **Risk:** medium to high because of HP callbacks, equipment, and server state interactions. Do not blindly migrate to another effects system.

### F08 — HIGH — RPCs Accept Gameplay Parameters without Sufficient Validation — Documented

**Files:** `Gameplay/Player/PlayerCombat.cs` (cleave and consumables), `PlayerInteractor.cs` (drop/pickup/chest), `PlayerInteractor.Building.cs` (payment).

Cleave receives reach/arc/multiplier; consumables receive type/value/duration without resolving a server-validated item; dropping receives ID/quantity and creates an object without server-side debit. Pickup/chest proximity depends on local filtering. The client-authoritative wallet and boolean building acknowledgment are explicit in the code.

**Scenario:** a faulty/modified client requests healing, drops, or gathering outside UI restrictions. Default ServerRpc ownership identifies the player but does not validate provided values or the consumed item's existence.

**Recommendation:** define the trust boundary; validate finiteness, distance, health, cooldown, item/recipe, and quantity on the server. Real inventory authority requires migrating wallet, action bar, crafting, and payments together. **Risk:** high; do not change the client-authoritative contract during local refactoring, which could duplicate host spending/delivery.

### F09 — HIGH — Spawn Waiting for World Does Not Invalidate Connection — Documented

**Files:** `ThirdPartyAssets/FishNet/Runtime/Generated/Component/Spawning/PlayerSpawner.cs` (`SpawnPlayerDelayed`), `Network/PlayerSpawner.cs`.

Both routines wait for readiness and then use the captured connection. The FishNet path integrates with the procedural gate; the project-owned spawner was not found serialized in searched scenes/prefabs.

**Scenario:** a player disconnects during slow generation; the routine continues when the world is ready and attempts spawning for the old connection. Exact behavior depends on FishNet internal validation; it was not reproduced.

**Recommendation:** validate connection/server during waiting and immediately before spawn; cancel waits on stop/dispose and test reconnection. **Risk:** medium, especially because this requires updating an adaptation inside vendored code. Avoid fixing only the inactive project-owned spawner and declaring the flow resolved.

### F10 — MEDIUM — Ghost/Afterimage Effects Have Incomplete Static Lifecycle — Documented

**Files:** `Effects/CombatFeel/AfterimageTrail.cs`, `Gameplay/Combat/StatusEffectVisuals.cs`.

Runtime ghost materials remain static without explicit destruction. The afterimage pool is static, but its container belongs to the scene; individual meshes have correct OnDestroy cleanup. Snapshots still perform two allocating hierarchy queries per dodge emission.

**Scenario:** after a scene change, the pool retains destroyed wrappers until another emission; sessions/reloads can retain materials. Each dodge creates repeated arrays proportional to renderer count. This distinguishes reference retention, native resources, and ordinary managed allocations.

**Recommendation:** define pool/material ownership and clean up at the appropriate lifecycle point; reuse renderer lists and refresh them when weapon/appearance changes. **Risk:** medium: permanent hierarchy caching can exclude a newly equipped weapon. Profiling is required to quantify benefit; no FPS improvement is promised.

### F11 — MEDIUM — WaveManager Follows Singleton Instead of Subscribed Instance — Documented

**File:** `Gameplay/Enemies/WaveManager.cs` (`Start`, `OnDestroy`).

Start accesses `DayNightCycle.Instance` without a guard, and OnDestroy attempts unsubscription from the current singleton. If the provider is replaced before the subscriber, the original instance's listener remains. Disabling only the component retains callbacks for new nights.

**Scenario:** a test scene without a cycle, provider reload/replacement, or disabling waves for debugging receives unexpected callbacks. In the normal scene, the provider usually shares lifetime; do not assert a permanent leak in that flow.

**Recommendation:** capture the provider when subscribing, unsubscribe from that same provider, and define whether disabling pauses waves or only Update. **Risk:** low to medium; changing OnEnable/OnDisable without handling late-created providers introduces order dependence.

### F12 — MEDIUM — Full Building Snapshots Grow with World and Player Counts — Documented

**Files:** `Gameplay/Building/BuildingWorld.cs` (`Update`, `Capture`, `Broadcast`), `PlayerInteractor.Building.cs`.

Once per second, the server serializes all BuildingStates and sends JSON to every peer, even without changes. Capture uses LINQ/a list; seed lookup queries the scene. Approximate costs are O(B) for capture/serialization and O(B × P) payload, rather than per-frame work.

**Scenario:** many buildings and station contents increase periodic allocation and traffic.

**Recommendation:** measure bytes/second and GC first; publish only on state changes, with a full snapshot for late joiners. Preserve forge progress updates. **Risk:** medium; incomplete invalidation can leave clients stale.

### F13 — MEDIUM — Interpolated Combat Logs Cost Even with Channel Disabled — Documented

**Files:** `Core/Logging/DuskLog.cs`, `Gameplay/Enemies/EnemyBase.cs`, and combat callers.

IsEnabled filtering runs inside the logger, after string construction. Conditional removes Log/Warn in builds without UNITY_EDITOR/DEVELOPMENT_BUILD, so those calls have no cost in normal releases.

**Scenario:** Editor/development horde profiling with disabled channels still pays formatting cost per hit; enabled logs also amplify cost.

**Recommendation:** guard proven frequent callers or use a localized logging solution if profiling warrants it. **Risk:** low. Do not guard every rare message or present this as a release-build improvement.

### F14 — MEDIUM — Failed Add Changes Wallet Revision — Fixed

**File:** `Gameplay/Loot/ResourceInventory.cs` (`Add`).

Revision incremented before checked arithmetic that can throw OverflowException. Count remained unchanged, but the load gate saw gameplay activity.

**Scenario:** extreme input/configuration makes a credit attempt fail and blocks checkpoint loading although the wallet received no credit.

**Applied solution:** calculate the checked count before any mutation and increment Revision after assignment. **Risk:** low. A regression confirms unchanged count and revision on overflow.

## 3. Work Batches and Architecture Boundaries

1. **Completed: local callback/state fixes:** F01/F03/F14; preserve public events and APIs.
2. **Completed: safe immediate crafting:** F02; method on the existing recipe, without a new service/factory/interface.
3. **Completed: runtime terrain mesh ownership:** F04; preserve Editor generation/serialization. Complete water/foliage ownership (F05) in a batch with regeneration tests and asset protection.
4. **Recommended next batch: stats:** F06/F07 together; test idempotence and temporary effects before changing fields.
5. **Network batch:** authority/validation contract F08; spawn cancellation F09. Separate trust changes from local cleanup.
6. **Profile-driven batch:** F10/F12/F13; wave lifetime F11 with provider/disable tests.

`BuildingWorld` concentrates catalog, visuals, validation, commits, snapshots, and persistence; large crafting/inventory UIs mix presentation and domain logic. Future testable operations can move into existing models, with persistence separated as it grows. This pass does not justify DI, an event bus, repositories, or a global framework. The independent inventory grid, data-driven recipes/definitions, staggered target acquisition, and FishNet pooling are useful existing boundaries and were preserved.

## 4. Validation Performed

Unity was open during this audit. No Unity CLI session, Unity MCP, or Editor/Play Mode control was performed by the audit.

- `Tools/EngineeringAudit/CompileOffline.ps1`: runtime `Assembly-CSharp` and `Assembly-CSharp-Editor` compiled through Roslyn with cached Unity references/defines. Outputs isolated in Temp; no import, asset processing, FishNet IL post-processing/weaving, or player build. Existing obsolete API/unused field warnings remain.
- `Tools/EngineeringAudit/RunTests.ps1`: **40** existing building/forge tests and **13** new regressions passed. The existing harness was updated to compile inventory dependencies and simulate the initial database. Tests compile production code but simulate Unity/FishNet and asset factories.
- `Tools/RangedCombat/TestTiming.ps1`: **50** assertions passed.
- `Tools/Briarback/Test.ps1`: **125** checks passed.
- `Tools/HollowWarden/Test.ps1`: **96** assertions passed.
- Reviewed call sites, subscriptions/cleanup, icon/callback order, and serialized field preservation. No Inspector reference migration required.

There are **324 offline checks** in total; suites use different units (tests, checks, and assertions). This does not mean 324 engine integration tests. F01 pooling order was established from source, but actual callbacks and F04 native resource release were not exercised by the harness.

The global whitespace check found trailing spaces in translation changes to `ChunkGridManagerEditor.cs`, `handout_props_and_chests.md`, and `terrain_generation_handout.md`, outside this refactoring's edited sections. They were not indiscriminately cleaned.

## 5. Remaining Verification

With the project closed in Unity, run `Tools/unity.ps1 all` for import, weaving, assets, and Editor suites. Before any commit, run `Tools/unity.ps1 clear-terrain` and purge `NavMesh-TerrainManager*.asset` files as required by AGENTS.md. The engineering audit itself made no commit; existing terrain and NavMesh were preserved.

Manual user checks not performed by the audit:

1. Kill enemies across two waves/reuses and confirm loot, death sounds, and alive count; exercise the boss too.
2. Craft with the last free slot, a full backpack, and recipes producing consumables/equipment; confirm exact cost, item, and icon. Confirm material crafting and forge behavior remain intact.
3. Consecutive dodges: confirm invulnerability ends and visuals return. Exercise effect removal/reapplication when appropriate tooling exists.
4. Regenerate terrain at runtime and change/reload scenes; check collision and use Memory Profiler to compare mesh counts after disposal. Do not infer leaks from total memory, caches, or temporary retention without ownership analysis.
5. Host and client: gather, craft, build, load the initial checkpoint, and disconnect while the world generates; F08/F09 risks remain open.

Visuals, audio, network behavior, and native memory/FPS improvements remain **unverified at runtime**. This delivery is a broad discovery/audit pass with five local fixes, rather than a declaration that no other defects exist.
