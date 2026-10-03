# Thornwing

Small moth-like beast that creates low ranged pressure beside the melee Swarmer.
Introduced in the first night's ordinary weighted spawn pool, continuing through
nights 2–6. The existing day/night system spawns enemies during the night portion
of each day. No boss HUD or encounter logic.

## Role and provisional balance

24 HP, 3 noncritical damage per spit, 4.8 m/s approach speed, 7.68 m/s short dart,
9 m attack range, 18 m/s straight projectile. Compared with the Swarmer's 50 HP,
5 damage, 5 m/s and cost 6, Thornwing has less than half the HP and much lower
effective damage frequency. Its range and movement pressure justify a provisional
budget cost of 8; its pool weight is 0.25 beside Swarmer's 1. Values await playtesting.

Ground navigation is the authoritative anchor. The creature floats around 1.15 m
above that anchor. A stable shared flight transform anchors the body, centered
hitbox and mouth socket; only the wings animate during living flight. This is low hovering flight that
follows terrain and routes around obstacles; it does not cross cliffs or fly above
the player's reach. Its root capsule extends from 0.475 m to 1.825 m, with radius
0.42 m, so existing ground-origin melee overlaps and player arrows can hit it.
Visual animation never moves the damage collider.

A lateral/retreat dart lasts at most 0.4 s and chooses a destination no farther
than 1.6 m away, once per 2.4 s. It stops during the warning and recovery. The
0.5 s amber-eye warning tracks for its first 0.4 s, then commits its aim.
Each valid release launches one swept-collision projectile, followed by 0.8 s
stationary recovery and 1.7 s cooldown. Frame hitches cannot consume the recovery
on the release frame. Stagger interrupts the windup; rejected/blocked shots still
recover. Elevation difference is limited to 1.5 m and solid obstacles block shots,
including thin walls between the body's center and the offset mouth. No poison,
slow, homing, crits, or point-blank instant-hit fallback.

Darts project lateral candidates onto physical terrain, then require a complete
path for the correct agent type, at most 1.6 m of total travel, at most 0.65 m
endpoint elevation change and a rise/run limit of 0.9.
Every 0.15 m along the path checks nearby physical ground support, surface normal
and clearance for the living capsule. Buried/stale navigation, unsupported drops,
steep surfaces and obstacles reject the dash. The agent follows the validated
path and cancels a dash if that path becomes incomplete or stale.
It tries both directions and shorter alternatives. Only a successful dodge uses
the 2.4 s dodge cooldown; rejected routes retry after 0.3 s. Dodge cooldown advances
through shot windup/recovery. Short darts disable automatic braking and accelerate
at 60 m/s², restoring normal movement settings when finished or interrupted.
Ground support allows the bake to sit up to 0.35 m above terrain; terrain more
than 0.25 m above navigation still rejects the route, along with drops and walls.

Amber eyes have enabled emission at intensity 2 while hunting and brighten to 6
during windup. Two shared-material additive wing trails last 0.28 s, emit while
the root moves, stop on death, and clear on disable/reuse or a teleport over 3 m.

Death hands motion to five rigidbodies: the head and abdomen are jointed to the
body, while both wings detach and tumble independently. Loose wings stay owned
by the corpse for cleanup. Living pose animation stops as soon as physics takes
over, including when the death impulse arrives before health replication.
A 2 N·s fatal-hit impulse distributes 80% by mass over all five bodies,
including loose wings; the remaining 20% applies at the struck part's hit point
to produce local rotation. The impulse is applied once per life. The
root collider disables immediately; bone colliders contact the ground, then
freeze after 2 s. The corpse and wings use the Swarmer's 10 s despawn lifetime,
copied from its prefab by the builder. Reuse
restores the pose, collider, attack grace, timers, audio, health and reward guard.
Remote observers disable their NavMeshAgent and use server NetworkTransform and
replicated action/health state. Replicated maximum HP supports co-op scaling.
Rewards: 1–2 gold and a 20% chance of one canonical sap pickup using the existing
loot manager and registered pickup prefab.

## Assets and reproduction

2026-10-03 update validation: offline runtime/Editor compilation, Unity batch
compilation and asset validation passed. In `Temp/ThornwingBatch`, Thornwing's
navigation, all-five-body directional death momentum, duplicate impulse rejection,
floor contact, pooled trail cleanup and reuse tests passed. The project regression
run passed 18 suites and failed the unrelated Bramblekin compact-footprint check.
Evidence is saved under `Logs/UnityCli/Thornwing-20261003`; the isolated copy is removed.
Live dash motion, eye glow/bloom, trail appearance and host/client agreement remain
manual checks: watch repeated shots/dashes near terrain and walls, kill from both
sides, and inspect the next pooled life for stale wings or ribbons.

- Current mesh/source/preview: `Artifacts/Thornwing/v002/`; 792 triangles,
  six rigid mesh renderers, two character materials and an eight-color palette.
  The original `v001` is preserved. Closed geometry, outward normals and UV checks
  passed. No armature or root motion; living wing flap and warning are procedural,
  while death uses jointed physics and loose wings. Living rigidbodies have no
  physics interpolation; interpolation starts at ragdoll handover and resets on reuse.
- `Tools/Thornwing/build_model.py` runs through official Blender MCP in a new
  scene. It preserves unrelated Blender content. Its manifest records hashes and
  geometry checks; the preview was visually inspected.
- Natural source-layered sounds: `Artifacts/NaturalSfx/Thornwing/v001/`.
  Kenney Impact Sounds and RPG Audio CC0 source files, included licenses, hashes,
  recipes, WAV masters and audition remain outside Assets. Two separate takes
  each for tension/spit/hurt/death and four cloth takes for quiet wing flutter.
  No oscillator/noise synthesis. Source Ogg Vorbis is lossy; export does not
  restore detail. `Tools/Thornwing/build_sfx.py` rebuilds candidates with Python
  and FFmpeg and refuses overwriting existing candidate/receipt files.
  Warning follows replicated windup, spit follows the release RPC, flutter plays
  during living flight, and hurt/death follow health events with a polling fallback.
  Death stops warning/flutter and plays its cue once. Sources use linear 3D
  attenuation from 6–28 m, with attack/voice priority 64 and flutter priority 160.
  Cue gains are warning 0.85, spit 0.9, hurt 0.8, death 0.95 and flutter 0.3.
  SFX volume applies at the source; master volume applies once at the listener.
- Stronger death and continuous flight buzz: `Artifacts/NaturalSfx/Thornwing/v002/`.
  Death layers recorded punch/chitin-like contact, soft impact and cloth collapse;
  the two new takes peak around -4.1 dBFS versus -9.9 dBFS previously. Original
  masters remain in v001 and installed death GUIDs are preserved. Buzz uses Joseph
  Sardin's CC0 [recorded insect wings](https://bigsoundbank.com/fly-and-glass-s0759.html),
  adapted at 1.15x rate with restrained filtering and a circular 0.25 s crossfade.
  The public MP3 source remains lossy. Two candidate excerpts were produced;
  runtime uses the more sustained first take. It is a 4 s mono loop, peak -8 dBFS,
  gain 0.35, local 3–24 m attenuation, priority 128 and randomized phase/pitch per
  life. It continues during windup, stops on death/disable and clears on reuse.
  All new masters are 48 kHz PCM 24-bit with zero clipped/nonfinite samples.
  `Tools/Thornwing/build_buzz_sfx.py` records source hashes, recipes and inspections.
  `Buzz_Audition.wav` repeats the installed loop three times for listening.
  Loop perception, naturalness and combat mix remain unverified.
- Runtime assets: `Assets/_Duskborn/Art/Models/Thornwing/`; prefab:
  `Assets/_Duskborn/Prefabs/Enemies/Thornwing.prefab`.
- `Tools/unity.ps1 build-thornwing` imports/configures materials/audio, builds
  projectile and enemy/reward assets, admits both FishNet collections and the
  enemy registry, adds the pool, patches only the scene's night-reference list,
  and runs focused checks. Rebuilding Briarback preserves this independent pool.

## Verified checks

The focused builder passed in batch Unity 6000.4.1f1 with the interactive project
closed. Tests cover imported geometry/UVs/facing, hover bounds, vertical wing-flap axis,
ground melee reach, player-arrow eligibility, solid and mouth-offset wall
rejection, clock range/elevation/cooldown/interrupt/hitch/death/reset boundaries,
pooled pose/collider reset, health UI, audio format/gain policy, canonical loot,
registry uniqueness and both FishNet prefab collections. The edit-mode lifecycle
fixture explicitly associates FishNet SyncVars; it does not simulate a live
connection. Audio: 13 installed mono 48 kHz PCM 24-bit cues, no clipped/nonfinite
samples; new death peaks around -4.1 dBFS and buzz peaks at -8 dBFS. Sound
naturalness and in-game mix are unverified.

An isolated preview physics scene verifies the jointed body and two unjointed
wings under gravity and a death impulse, floor contact, wing separation,
idempotent activation, and repeated parent/pose/collider restoration. This does
not verify live slope contact or the final appearance of a death in combat.
The fixture also moves and turns a living root through twenty physics steps and
checks every body/wing follows its parent. Imported checks verify the hitbox
center matches the flight anchor and the shot socket sits near the visible face.

Baked navigation fixtures verify supported flat and gradual uphill/downhill
darts, including terrain-projected 30-degree slopes from horizontal candidates,
rejecting physical walls absent from the bake, buried navigation,
unsupported drops, off-mesh/long destinations and steep elevation changes.
These exercise the runtime path validator without starting an agent in Play Mode.

Across 128 seeds for each night and 1–4 players (3,072 generated timelines, each
regenerated to check determinism), budgets remained bounded. Solo Thornwing means
for nights 1–6 were 3.01, 2.45, 3.01, 3.05, 3.31 and 3.59; maxima were 7, 7, 7, 7,
9 and 11. Night 1 had zero Thornwings in 1/128 sampled seeds. These are complete
night counts, not simultaneous-enemy caps or guaranteed appearances.

Final `Tools/unity.ps1 all` passed on 2026-10-01: compilation succeeded;
validation checked 2 build scenes, 48 prefabs and 74 resource assets; all 17 static
Editor suites passed with zero failures. Logs are in `Logs/UnityCli` and the actual
suite summary is `Logs/UnityCli/test-results.json`. Focused builder reruns retained
one registry/pool/network entry and existing asset GUIDs. No player build or live
multiplayer session was run.

## Remaining manual checks

1. Start a day-1 session and observe natural spawning, chest-height flight on
   slopes, warning readability, melee/unarmed/cleave and bow hits, dodgeability,
   short darts and the stationary recovery window. Check around buildings/trees
   and elevation changes; confirm it feels annoying without endless kiting.
2. Check repeated hurt/death sounds, flutter density and warning/spit levels in
   crowded combat. Listen to `candidates/Thornwing_Audition.wav` first. No auditory
   perception or live mix validation was performed by the agent.
3. With a host and remote client, check matching projectile hits/HP/death/rewards,
   joining mid-windup and near a corpse, and respawning a recycled enemy. Check
   body ragdoll and detached wing contact on slopes, cleanup, and confirm
   wings/health UI restore without old cues.

Live appearance, animation transitions, slope death contact, balance, sound mix
and multiplayer agreement remain unverified. No Editor UI control or Play Mode
was used. Before any later commit, clear generated terrain using the documented
CLI command and purge leftover `NavMesh-TerrainManager*.asset` files.
