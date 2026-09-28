# Hollow Warden handoff

Updated: 2026-09-28. This is the current continuation guide. It supersedes older
three-root and post-cast exposure descriptions in production notes. The audio/breakup/
progression revision at the end records the latest user-requested integration.

## Status and accepted direction

The user said "all good now" after the pursuing spike barrage and channel-only core
exposure changes, then requested this handoff. Treat the current encounter/visual
direction as accepted. Do not redesign it or restore the old breakable-root objective.
That acceptance does not establish a complete host/client, restart or balance test pass.

The first boss appears on night 3. Assets and implementation remain in the shared
working tree, with many untracked boss files and unrelated modifications. No commit
or push was made. Unity was closed for the latest successful batch compile/asset validation
and passing Warden suite; inspect again before batch validation. No new chat was messaged.

## Current encounter contract

- One night-3 boss alongside ordinary waves. Repeat normal night-three wave budgets
  until dawn if the night is extended. The existing clock hold preserves 45% remaining
  while the boss lives. Defeat awards gold and one epic Hollow Heart once, releases
  the hold, and lets the remaining night finish naturally. Night 7 remains independent.
- Starting stats: 900 HP, 18 damage, 2.3 movement speed, 75 reward; co-op HP scaling.
- Rootbreaker: 8 x 1.8 m stripe, lock at 0.6 s, impact at 1.0 s, total 2.4 s.
- Harvest Sweep: frontal 180 degrees, 3.8 m radius, lock at 0.6 s, impact at 0.9 s,
  total 2.2 s. Phase two speeds these attack windows/clips to 1.15x.
- After three normal attacks: 1.4 s planting preparation, then the spike channel.
- For six seconds, mark ground beneath every living player approximately every
  0.5 s. Each position locks immediately and does not follow subsequent movement.
- Amber-to-red ground boundaries and growing fills warn for 0.75 s before eruption.
  Seven visual spikes rise at each marker. One damage check uses a 2.4 m horizontal
  radius plus player body extent, with vertical separation checks. Damage is 18 base.
- Overlapping markers within one wave hit each player at most once. Spikes retract
  over 0.65 s; they have no colliders, loot or lingering damage. Missed waves are not
  backfilled after hitches; expired spikes do not apply delayed invisible damage.
- Rooted lasts 7.4 s total: six-second warning production plus time for final spikes
  to finish. The boss remains stationary and channeling through that final interval.
- **Only Rooted/channeling exposes the core and applies 1.5x incoming damage.**
  The whole boss remains normally damageable outside this window; there is no new
  separate core hitbox or general invulnerability.
- On channel completion, go directly to one-second Recover with closed plates and
  normal incoming damage. No post-channel stagger/exposure window, in either phase.
- Half health queues PhaseBreak at a safe boundary. It must not interrupt a committed
  attack/channel or leave the chest permanently open. Death cancels pending spikes.

## Visual implementation

`HollowWardenChannelVisual.cs` is a runtime pose layer applied after the Animator.
It raises/spreads the arms, keeps the chest upright and adds a gentle channel motion.
Preparation raises the arms while the plates stay closed. Channeling opens the plates;
all other states force the closed rotations, overriding the old PhaseBreak/Recover
plate animation tracks. The existing FBX remains the base animation.

Channel feedback is restrained: pulsing amber emission, a thin core halo and three
small orbiting sparks. Geometry/materials are reused; no extra point lights or particle
simulation. Effects are hidden on state exit/disable and cleaned up on destruction.
The HUD explicitly tells players the core is exposed while they dodge spikes.

Spike positions, surface normals and start ticks are replicated through a SyncList;
clients present pooled warning/eruption meshes and never apply damage. The retained
HollowWardenRoot prefab supplies the wood material. It is no longer spawned as a
networked breakable objective. Do not delete its asset/reference casually.

No prefab or FBX rebuild is needed for the latest gameplay/pose changes.

## Important files

All gameplay paths below are under `Assets/_Duskborn/Gameplay/Enemies/`:

- `HollowWardenEncounter.cs`: pure state machine, spike timing/radius, damage window.
  Legacy Stagger/Exposed enum/clip names remain, but are not part of the current cycle.
- `HollowWardenBoss.cs`: server targeting, spike scheduling/hits, co-op HP, replication,
  normal attacks and death. It bypasses ordinary EnemyBase AI.
- `HollowWardenPresentation.cs`: animation snapshots, spike warning/eruption pool, HUD.
- `HollowWardenChannelVisual.cs`: channel pose, plate gating and core effects.
- `HollowWardenNight.cs`: night hold, spawn, reward and teardown; also see WaveManager
  and `Assets/_Duskborn/Core/DayNightCycle.cs`.
- `EnemyBase.cs`: preserve Unity-aware Animator null checks in ResetEnemy/OnHPChanged.
- `HollowWardenRoot.cs`: retained static root component, no Animator or owner callback.

Other entry points/assets:

- `Assets/_Duskborn/Editor/HollowWardenBuilder.cs` and `HollowWardenTests.cs`.
- `Assets/_Duskborn/Editor/Cli/DuskbornCli.cs`, `Tools/unity.ps1`.
- `Tools/HollowWarden/Test.ps1`, `EncounterTests.cs`, `Compile.ps1`.
- Boss: `Assets/_Duskborn/Resources/Bosses/HollowWarden.prefab`.
- Root/material source: `Assets/_Duskborn/Prefabs/Enemies/HollowWardenRoot.prefab`.
- Model/controller/materials: `Assets/_Duskborn/Art/Models/HollowWarden/`.
- Editable model: `Artifacts/HollowWarden/v002`; v001 is historical.
- Reference/reproduction: `Artifacts/HollowWarden/README.md`,
  `Tools/HollowWarden/build_model.py`, `ground_feet.py`, `validate_model.py`.

Model: 1,018 triangles, 21 bones, two materials, twelve prototype clips, 4.066 m tall.
Eight foot islands have flat soles. Preserve `FBX_SCALE_UNITS` on export; omitting it
previously caused 100x animated scale. Follow the Blender skill if editing source assets.

## Validation: what is actually established

Latest audio/breakup/progression revision:

- 96 offline assertions and offline runtime/Warden Editor compilation pass.
- Unity `all`: compile and asset validation pass. HollowWardenTests passes, including
  readable fragment weights/budget, serialized audio clips, epic pickup registration,
  and Arcane Table heart requirement/consumption checks. Overall 3/13 suites pass.
- The ten failing suite names match the previously documented list below. This is not
  a clean full suite and does not establish that every individual failure is unchanged.
- Live audio, breakup contact/lifetime, host/client loot and extended-night behavior
  remain unverified. No Editor UI control, Play Mode, commit or push.

Earlier channel-only exposure revision:

- `./Tools/HollowWarden/Test.ps1`: **85 assertions passed**.
- `./Tools/HollowWarden/Compile.ps1`: runtime and Warden Editor C# compilation passed
  with warnings, using existing project references. Does not import assets, perform
  FishNet weaving, execute Unity tests or validate rendering.
- User accepted the result with "all good now". No exhaustive multiplayer/visual
  matrix or specific balance measurements were reported.
- Unity was open, so no Unity batch test ran after this latest revision.

Earlier spike-barrage revision, before channel-only exposure:

- Unity batch compilation and asset validation passed.
- Full `all` run passed 2/13 suites. Warden's prior inactive-root regression fixture
  failed along with ten other suites. The fixture was corrected: FishNet makes Awake
  public, and an inactive edit-mode clone needs its component owner wired for SyncVars.
  This fixture-only setup neither starts a server nor changes production prefab data.
- After that correction, `./Tools/unity.ps1 test-warden` passed without captured errors.
  This is the latest passing Unity test run, and predates the final exposure/pose change.
- Other failed suites: Building, SceneFurnace, CharacterPanel, Crafting,
  CursorAndMenuFocus, DayNightCycle, ItemTierDrop, SpatialOccupancyMap,
  UIResolutionScaling, WorldHealthBar. Do not claim a clean full suite or assume
  causality without baseline evidence. Logs under `Logs/UnityCli` are generated and
  may be overwritten by later runs.

## Next steps, in order

1. **Close the remaining validation gap.** Check whether this project is open in Unity.
   If open, continue offline and leave batch validation pending; do not close/control
   the Editor yourself. Once closed, read the CLI rules/docs and run
   `./Tools/unity.ps1 test-warden` on the current revision. Use `all` when warranted
   by broader gameplay changes, but triage its known failures separately.
2. **Targeted co-op and lifecycle checks with the user.** Confirm every living player
   gets warnings; overlapping teammate markers hit once per wave; late observers see
   existing warnings; death mid-warning cancels damage/effects; disconnect/restart
   removes hazards/HUD; reward happens once and day 4 starts. Verify phase two closes
   the core outside channel. Do not repeat an entire visual approval pass unnecessarily.
3. **Review integrated audio, breakup and loot.** Check wood preparation, channel loop,
   spike eruption, slam/sweep, phase transition and layered death against gameplay.
   Verify Master/SFX controls, immediate loop stop, 18-second fragment cleanup, one epic
   heart pickup and consumption when building the Arcane Table. Check extended waves
   and natural dawn after defeat. Implementation is complete; live acceptance is pending.
4. **Measured tuning and remaining polish.** Test solo/co-op, slower loadouts and
   realistic latency. Default 5 m/s movement escapes a fresh marker, but practical
   fairness is not proven. Check warning-to-hit agreement on slopes and animated foot
   contact/joint intersections. Adjust only where playtests identify a problem;
   preserve the accepted channel-only vulnerability and simple low-poly style.
5. **Prepare for integration only when requested.** Review the actual diff and isolate
   boss work from unrelated changes. Before any commit, follow terrain cleanup below.

Concise manual sequence: night 3 -> channel while moving -> deliberately stand in a
warning -> attack the exposed boss during channel -> confirm closure on recovery ->
cross half health -> kill during a pending warning -> verify reward/day 4. Repeat the
network-specific cases with a client and test restart/disconnect.

## Preserve these fixes and constraints

- Unity MCP is unavailable on the user's free plan. Never retry it or ask for upgrades.
- Never directly control the Unity Editor or enter Play Mode. Use source, offline
  compilation, logs and the project's non-interactive CLI. User performs live checks.
- Read `.cursor/rules/unity-cli-workflow.mdc` and `Docs/unity-cli.md` before batch work.
  Never run the Unity CLI while this project is open. Recheck processes each new session.
- FishNet registration must include `Assets/_Duskborn/Network/DefaultPrefabObjects.asset`
  (GUID `0694227392ea7e04cacc6aaaa1c0cd9d`), the collection used by SampleScene.
  Registering only `Assets/DefaultPrefabObjects.asset` previously prevented spawning.
  The builder/validator already handle this. Do not fake runtime initialization timestamps
  or serialize NetworkBehaviours into production prefabs to work around spawn errors.
- Keep EnemyBase's explicit Unity-object Animator null checks; static roots intentionally
  have no Animator. Never add an empty Animator merely to mask the old exception.
- Preserve unrelated FurnaceEffects/FurnaceEffectsEditor, forge/cauldron building assets,
  `Tools/FurnaceEffects/Compile.ps1`, other prefab changes and the user's SampleScene terrain.
- Before **any commit**, with Unity closed, run `./Tools/unity.ps1 clear-terrain` and
  purge `NavMesh-TerrainManager*.asset` files as AGENTS.md requires. No commit was requested
  in this handoff turn. Do not overwrite the scene or discard unrelated work.

## Audio, breakup and progression revision — 2026-09-28

This revision supersedes the earlier wave replacement, immediate dawn and pending-audio notes above.

- Normal night-3 waves run alongside the boss. After their finite timeline completes,
  further normal night-3 budgets repeat until dawn, preserving living enemies and co-op scaling.
  There is no additional stat multiplier: prolonged pressure comes from continued spawning.
- The existing midnight hold preserves 45% of the night clock. Killing the boss releases
  that hold immediately; the remaining night finishes naturally. It never calls EndNight.
- Death grants the existing 75 gold once and drops one guaranteed epic `material_hollow_heart`
  (Coração Oco). The Arcane Table requires one heart in addition to its existing materials.
  The definition lives under Resources/Bosses/Items and is registered on clients and in
  inventory UI. It reuses the registered stone pickup mesh with epic loot effects.
- HollowWardenFeedback uses existing AudioDatabase references: wood preparation/eruption,
  heavy swing, stone impact/phase break, heartbeat channel loop, and layered wood/stone death.
  Six action/channel/spike sources bound concurrency; two death sources live with the debris.
  SFX volume updates live; AudioListener applies master volume. Generic Swarmer audio is bypassed.
- Replicated death creates local fragments from the baked rigidly weighted mesh, capped at 24.
  They fly outward, settle through surface casts without gameplay colliders, remain for 18 seconds,
  and shrink over their final two seconds. Generated meshes are destroyed on cleanup.
  Debris/death sounds survive the boss's three-second network despawn and clear on session exit.
  Fragment physics are cosmetic and may differ across peers; the item pickup is server spawned.
- FBX read/write is enabled for skin weights in player builds and preserved by the builder.
  No model/rig regeneration or prefab rebuild is required; Unity must reimport the changed FBX metadata.
- New files: HollowWardenFeedback.cs, HollowWardenDebris.cs, HollowWardenNightRules.cs.
  Regression coverage adds finite night hold/replenishment tests and Unity asset/audio/loot/crafting gates.
- Offline result: 96 assertions passed; runtime and Warden Editor compilation passed with warnings.
  Unity batch compile and asset validation pass; Warden suite passes. Full suite is 3/13,
  with the ten previously documented failing suite names. See Logs/UnityCli/test-results.json.

Manual checks remaining: listen through both attacks and the channel; change Master/SFX volume;
kill during channel and verify immediate loop/hazard cancellation, breakup, surface contact and
18-second cleanup; collect the epic heart and build the Arcane Table; verify normal waves before
and after the kill, extended spawning past the original night duration, natural dawn, and one reward.
Repeat with a client and disconnect/restart. Late clients only see debris if they observe the boss
before its network despawn. No commit or push requested/performed.

Scene wiring follow-up: SampleScene assigns just one normal wave definition (whose
NightNumber metadata is zero). WaveManager now uses the actual started night number
and falls back to the last configured normal definition for night 3 when no third slot
exists. An explicit third entry takes precedence. Other nights retain their existing
selection behavior. Four regression assertions cover this fallback, bringing the pure
suite to 96. This avoids editing the user's generated-terrain scene.

Final focused validation after the scene fallback: `Tools/unity.ps1 test-warden`
succeeded with no captured errors. Log: Logs/UnityCli/unity-test-warden.log.
