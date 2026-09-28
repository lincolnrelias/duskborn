# Hollow Warden — night 3 production plan

Approved direction: the second, simplified concept. Broad flat faces, large root fist,
two stone shoulder slabs, sparse moss, amber eyes and an exposed amber heart.
This document tracks production; unchecked items are not implemented.

Current pass (2026-09-27): `Artifacts/HollowWarden/v002` contains the grounded-foot
source and FBX: 1,018 triangles, 21 bones, two materials and twelve prototype clips.
Unity's baked idle mesh measures 4.066 m tall with its soles at Y=0. Export uses
`FBX_SCALE_UNITS` to preserve animated scale. Blender round-trip validation, 50 pure
C# assertions, and the clean `build-warden` import/prefab/mechanic checks pass.

The server adapter, pursuing spike barrage, presentation and night 3 integration are installed.
Boss prefab: `Assets/_Duskborn/Resources/Bosses/HollowWarden.prefab`.
The broader `all` run passed compilation and asset validation; only 3 of 13 test suites
passed, including HollowWardenTests. Ten other suites failed; see
`Logs/UnityCli/test-results.json` and `unity-test.log`. This is not a fully green project.
Next: live solo/co-op playtesting, animation contact polish, encounter tuning and audio.

Continuation validation: the 50 pure assertions and offline runtime/Warden Editor
compilation pass after the Animator fix. `Tools/HollowWarden/Compile.ps1` provides
that compilation gate without launching Unity. Root reset/death callback regressions
for absent/destroyed Animators were added to `HollowWardenTests`; execution is pending
until Unity is closed. These checks do not establish live root spawning or cleanup.

## 1. Encounter and asset contract

- [x] Inspect existing enemies, networking and day/night flow.
- [x] Save the approved concept alongside the production files.
- [x] Define attack timings, animation names and completion rules below.
- [ ] Tune numeric values in an actual encounter; all values below are starting points.

Target: a 3.8–4.2 m tall boss, feet on the origin plane, in-place animation, Generic rig.
Aim for 800–1,500 triangles, a compact palette, a separate emissive heart/eyes material,
one skinned character mesh and roughly 20 bones. Spend geometry on silhouette.
Root arm is the character's right arm; preserve this through export.

Night 3 is a mid-run boss encounter, not the final night 7 encounter. Implemented behavior:
spawn once at nightfall; replace ordinary night 3 waves; hold the night until boss death;
award the encounter reward once; proceed to day 4. Do not mark the run won.
If spawn validation fails, do not leave the night permanently held: report the failure
and release the hold. Night 7 behavior remains independent.

## 2. Model and materials

- [x] Build and inspect a prototype against the approved silhouette, front/side/rear.
- [x] Check topology, positive volume, triangle count, height, UVs and material assignments.
- [x] Keep shoulder slabs simple; avoid bark noise, excessive spikes and thin roots.
- [x] Use separated overlapping wooden masses around joints to permit rigid weights.
- [x] Save editable Blender source, reference, preview and a reproducible build script.
- [x] Flatten all eight heel/toe islands to the ground plane with broad sole edges.
- [ ] Polish limb joins and verify foot planting throughout animation.

Deliverable: `Artifacts/HollowWarden`, with editable source outside Unity's Assets folder.
Gate: clear fist/heart silhouette at gameplay scale and no conspicuous gaps in key poses.

## 3. Rig and animation

- [x] Root, hips, chest, head, upper/lower arms, hands, upper/lower legs, feet,
  two chest plates, heart and shoulder stones; rigid weights on segmented wood.
- [x] Prototype Idle (2 s), Walk (1.2 s), Spawn (2 s).
- [x] Prototype Rootbreaker (2.4 s), HarvestSweep (2.2 s), RootPlant (1.4 s), Rooted (2 s loop).
- [x] Prototype Stagger (1 s transition), Exposed (2 s loop), Recover (1 s), PhaseBreak (2 s), Death (3 s).
- [ ] Inspect contact frames, joint intersections, loop seams and root motion.
- [x] Export FBX at 30 fps with baked actions; reimport into an isolated Blender scene
  to verify clips, scale, armature and bindings. Unity import is a separate gate.

Authoritative gameplay clocks drive hits. Animation events may play sound/FX but must
never independently apply damage on clients. Phase 2 speeds only attack clips to 1.15x;
its gameplay windows use the same multiplier. Telegraphs remain at least 0.75 s.

## 4. Unity import and prefab

- [x] Add an idempotent Editor build/validation command to the existing CLI workflow.
- [x] Import FBX as Generic, disable root motion, explicitly configure clip looping,
  build URP palette and emissive materials, and map both material slots.
- [x] Create twelve named Animator states, driven by replicated action snapshots.
- [x] Assemble `HollowWarden` prefab: NetworkObject, network transform, NavMeshAgent,
  body collider, damage receiver, presentation and encounter controller.
- [x] Register network prefabs through the project's existing FishNet convention.
- [x] Author reusable root-cluster prefab and cached ground-telegraph meshes.
- [x] Validate references, clips, facing, imported scale and network registration.

Gate: CLI imports and validates the prefab without missing scripts/materials/clips.
Do not modify a scene by hand to emulate successful Unity import.

## 5. Attacks, phases and multiplayer

Start with one standalone, engine-independent encounter state machine. Its adapter
owns navigation, target selection, network replication, hit queries and presentation.
Use one server-selected action sequence and one damage application per target per attack.

### Rootbreaker

Aim at a living player during early windup; lock direction at 0.6 s. Ground stripe
approximately 8 m long and 1.8 m wide. Impact at 1.0 s; recovery until 2.4 s. Base hit
is 1.2x configured boss damage. Telegraph and hit must use the same locked geometry.
MVP is a single line impact; traveling roots can be visual only.

### Harvest Sweep

180-degree frontal arc, 3.8 m reach. Lock facing at 0.6 s, impact at 0.9 s, recover
until 2.2 s. Base hit is 1.0x boss damage. Back side is safe. Use player hitbox extent
when testing boundaries; deduplicate players with multiple colliders.

### Buried Heart — pursuing spike barrage (2026-09-28)

After three attacks, plant for 1.4 seconds and channel a six-second barrage. Every
0.5 seconds mark the current ground beneath each living player. Positions lock when
marked; amber-to-red rings and a growing fill show the 0.75-second eruption countdown.
Each marker erupts into seven spikes with a 2.4 m horizontal damage radius, accounting
for player body extent and vertical separation. Each wave deals normal boss damage
at most once per player, even where teammates' markers overlap.

Spikes retract after 0.65 seconds. No collision or lingering damage follows the one
impact. New warnings stop after six seconds; allow pending warnings/visuals to finish,
The core is exposed for the channel itself, including the final spike-drain time,
taking 1.5x incoming damage. It closes immediately on entering one-second recovery;
there is no post-cast exposure or stagger window. Boss movement
stops during the cast. This replaces the former three breakable root objectives.

Only the server targets players and applies damage. Replicated positions, surface
normals and start ticks drive pooled local warnings/eruptions, including late observers.
Death, reset and server teardown clear spikes. Hitches never backfill missed volleys;
every newly created warning gets its full delay. Missing ground skips that marker.
The default 5 m/s player can escape a freshly centered warning by continuing to run;
latency, slowed loadouts, slopes and multiplayer fairness still require live tuning.

### Half-health transition

At <=50% health, queue one PhaseBreak at the next safe action boundary (never silently
shorten an already displayed windup or delete its committed impact). Speed subsequent attack clips/windows to 1.15x. The core stays closed outside
the spike channel, including during the half-health transition. No damage pulse during the transition. Death interrupts everything immediately.

### Integration checklist

- [x] Pure state machine and offline tests: attack windows, skipped frames, duplicate
  root events, root timeout, phase transition once, death cancellation, reset.
- [x] Server-only targeting/hit resolution and bounded allocation-free hit queries.
- [x] Replicate action ID, start tick/time, facing, phase and roots; clients only present.
- [x] Navigation stops during committed attacks, planting, exposure and death.
- [x] Update AI for target death/disconnect and for no remaining living players.
- [x] Boss health bar with co-op maximum health, phase feedback, spike cast feedback and telegraphs.
- [ ] Audio: wood windup, heavy slam, sweep, root break, chest crack and death.
- [x] One-shot night 3 spawn/death/reward flow and idempotent teardown on session exit.

The boss derives from EnemyBase for existing melee targeting, replaces ordinary AI,
and overrides its death delay to preserve the three-second animation. Night 3 uses
an explicit finite lighting hold and guarded completion; night 7 remains independent.
Missing ground skips spike markers; failed boss spawning falls back to ordinary progression.
Starting tuning: 900 HP, 18 damage, 2.3 movement speed, 75 encounter reward. The retained root prefab supplies the spike material.

## 6. Validation and release gate

- [x] Offline compilation of the pure encounter core and state-machine scenario tests.
- [x] Unity batch compilation of the adapter and Editor builder.
- [x] Run `./Tools/unity.ps1 all` with Unity closed (ten other suites failed).
- [x] Clean `./Tools/unity.ps1 build-warden`, including asset and mechanic checks.
- [ ] User visual check: scale beside player, walk/feet, every attack pose, chest opening,
  telegraph-to-hit agreement, root visibility on slopes, death animation and materials.
- [ ] Host + client check: one boss, same telegraphs, each player hit once, root breaks
  synchronized, death/reward once, day 4 starts, and later night 7 still functions.
- [ ] Test restart/disconnect, boss death during any attack, missing prefab/NavMesh,
  missing spike ground, and damage crossing 50% during windup/cast/exposure.
- [ ] Tune solo/co-op health, damage, ranges and recovery using actual player loadouts.
- [ ] Before any commit: close the project in Unity, run clear-terrain through the
  wrapper and purge `NavMesh-TerrainManager*.asset` as required by AGENTS.md.

The user closed Unity before batch validation. No Editor UI control or Play Mode was used.
Blender renders and automated checks do not establish live gameplay correctness.

Manual verification: advance to night 3; inspect idle/walk feet and both attack warnings;
run through the spike barrage and verify automatic exposure; cross half health; kill the boss and check
one reward and day 4. Repeat with a client and check disconnect/spike cleanup. Inspect
slopes, animated contact and warning-to-hit agreement before accepting visual quality.


### Spike barrage validation — 2026-09-28

- 65 pure encounter assertions pass, including hitch spacing, automatic exposure,
  death/reset cancellation, horizontal body extent, vertical separation and running escape.
- Unity batch compilation and asset validation passed with the project closed.
- The initial full run passed 2/13 suites, including a failure in the prior inactive-root
  test fixture. That fixture now wires its temporary FishNet component owner and uses
  the postprocessed public Awake method; it never starts a server or changes a prefab.
- After correction, `./Tools/unity.ps1 test-warden` passes all focused boss checks with
  no captured errors. Ten other suites failed in the full run; the full suite was not
  repeated after the focused repair. Logs are in `Logs/UnityCli`.
- No Play Mode, Editor UI control, model rebuild or commit. Live visual/co-op checks remain pending.

### Channel-only exposure update

The channel is now the sole vulnerability window in both phases. A runtime rig pose
layer raises/spreads the arms, holds the chest upright, opens its plates and adds a
restrained amber pulse/halo with three motes. Recovery, phase transition and death close
the plates and disable these effects. No FBX/prefab rebuild is required. 85 pure tests
and offline runtime/Editor compilation pass; Unity-backed and live visual checks for
this update remain pending because the project is open.

## Current integration revision — 2026-09-28

See `hollow-warden-handoff.md` for the authoritative continuation contract. Boss audio,
local mesh breakup (18-second lifetime), a guaranteed epic Hollow Heart and its Arcane Table
cost are integrated. Night 3 keeps normal mobs spawning, replenishes normal wave budgets
through extended time, and releases the existing midnight hold on defeat instead of forcing dawn.
Earlier wave-replacement/immediate-day-4 descriptions and pending-audio checkboxes are historical.
Offline validation: 96 assertions and runtime/Warden Editor compilation pass. Live audio,
breakup/ground contact, loot pickup/crafting and host/client lifecycle checks remain required.

Unity batch result for this revision: compilation, asset validation and HollowWardenTests
pass (including audio references, fragment weights/budget, epic pickup registration and
heart cost/consumption). Full suite: 3/13 passed; ten previously recorded suite names fail.
See Logs/UnityCli/test-results.json. No live visual/audio/multiplayer verification is implied.
