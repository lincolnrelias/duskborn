# Briarback

An original bark-armored woodland boar for Duskborn's regular night waves. Its
wide snout, ivory tusks, moss plates and thorn ridge distinguish it from the
Swarmer and Hollow Warden. Amber eyes and an orange ground lane signal danger.
It pressures players to move sideways while Swarmers chase them.
At close range it switches to a faster frontal headbutt, preventing safe constant
circling inside the charge windup distance.

## Difficulty and counterplay

Initial spawn difficulty value: **18 budget points**, versus **6 for one
Swarmer**. This is a provisional design value, not a playtested difficulty score.
At 120 HP it has 2.4 times the current Swarmer's 50 HP, hunts more slowly
(3.2 versus 5 m/s), and deals an 18-damage burst rather than a 5-damage melee hit.
The readable warning, committed direction and recovery justify keeping its
cost near three Swarmers instead of treating it as an elite or miniboss.

Attack sequence:

1. Spawn with one second of attack grace and seek the nearest living player.
2. Within seven meters, require a clear NavMesh line and similar elevation.
3. Stop and lock the direction for a **0.9-second windup**. Display a ground
   lane covering the full eight-meter sweep plus the 0.7-meter hit-radius margin.
4. Rush straight at **10 m/s for at most 0.8 seconds**. Never retarget the rush.
   Damage each living player at most once using a swept query to avoid tunneling.
   Stop at NavMesh edges and solid non-player/non-enemy obstacles.
5. Remain vulnerable during **1.4 seconds of recovery**. The three-second cooldown
   starts when recovery begins. Long frames cannot consume a newly entered state.

It has no damage resistance, critical chance or unavoidable contact attack.
Dodge sideways during the windup and attack during recovery. Ordinary hits still
damage it during its committed attack; its charge does not use the generic
Swarmer melee or weapon-action loop.

Close-range selection takes priority: at **2.2 m or less**, choose headbutt;
between 2.2 and 7 m, choose charge; farther away, continue hunting. Both require
similar elevation, a clear navigation line and an unobstructed solid-collider
line. A cooling-down headbutt never falls back to a point-blank charge.

The headbutt has a **0.4-second windup**, a **0.18-second hit window**, and
**0.7 seconds of recovery**. It follows the nearby target for the first 0.2 seconds
of windup, then locks facing for the final 0.2-second dodge window. Its warning
is a 120-degree frontal sector with 2.2 m reach. A head-tuck, upward strike and
recoil animate the attack. It deals **12 base damage** (two thirds of charge
damage), once per living player per attack, with an obstruction check at impact.
The headbutt cooldown is 1.5 seconds from the start of recovery and is independent
of charge cooldown; the common state machine still prevents overlapping attacks.
Both attacks retain the one-second spawn grace and reset on pooling/death.
The spawn budget remains a provisional 18 points pending playtesting of both attacks.

## Spawn policy

The prefab is registered as `EnemyType.Briarback` (appended to preserve existing
serialized enum values). WaveManager creates its pool from EnemyPrefabRegistry
and uses the existing seeded budget picker and jittered 120-second timeline.
Spawns use the existing 30-50 m perimeter with grounded NavMesh sampling. Failed
placements do not increment the living count.

Night 1 retains its existing Swarmer-only data. Night 2 uses budget 120 and
Briarback weight 0.22 alongside Swarmer weight 1. Night 3 retains budget 100 and
Swarmer-only support for the Warden encounter. Nights 4, 5 and 6 use budgets
160, 190 and 220, with Briarback weights 0.32, 0.38 and 0.44. Night 7 remains
under the existing separate progression rules.

Across 128 seeds, solo expected counts measured by the actual timeline generator
were 2.35 on night 2, 3.96 on night 4, 5.21 on night 5 and 6.47 on night 6.
Weighted selection can produce a night without this enemy; it is not a guaranteed
encounter. Existing budget scaling adds 40% per additional player, and HP scaling
adds 25% per additional player. The scaled maximum HP is replicated for health UI.

## Model, presentation and loot

The model has 1,842 triangles, 1,013 source vertices, 17 bones, two material slots,
and one 64x8 palette. It measures approximately 1.47 m wide, 2.88 m long and
1.91 m tall including the ears and thorns. Its grounded pivot is at the origin.
Seven Generic clips contain idle, walk, windup, charge, recover, hurt and death
poses. Locomotion is in place; server navigation moves the root.
The builder also creates three non-looping headbutt animation assets from the
idle skeleton, with head pitch curves and planted feet. The controller has ten
states in total; the source FBX retains its original seven clips.

The production source is `Artifacts/Briarback/v003/Briarback.blend`, accompanied
by the FBX, palette, original synthesized mono creature sounds, neutral and hoof
renders, manifest and sampled animation grounding results. v001 and v002 preserve
earlier passes. Each foot now has two separate tapered keratin claws with a
visible cleft and grounded soles. No paid generation service, third-party model or external license was
used. The Blender scene is separate from previously open content.

The game prefab uses the existing EnemyBase damage numbers, hit flash, outline,
health provider, shared world health bar, player target registry, death event and
FishNet pooling path. Replicated phase, origin, direction and start tick drive
client animation, local audio and the warning lane.
The hurt pose plays during hunting without interrupting the committed attack.
Gameplay damage and loot are server-only; clients disable their NavMeshAgent.
Per-charge hit tracking,
reward guard, attack clock and presentation are reset on pool reuse.

During a moving charge, the presentation reuses Hollow Warden's faceted dust,
stone mesh, materials, ballistics and fade. Smaller alternating impact sites are
spaced every 0.65 m along the actual path, with ground sampling and a reusable
60-piece pool. Impacts fade within 1.05 seconds after emission stops. Teleports
do not draw a trail across the map; despawn and pool reuse clear it.
Headbutts emit a smaller three-site burst of the same dust and stones beneath
the forward strike, timed to the upward head pose at 0.06 seconds. Each attack
emits once, fades normally and uses the same pool; stale attacks do not replay.

Death hands the skeleton to the existing EnemyRagdoll system: ten rigid bodies,
nine joints, disabled living bone colliders, and no self collisions. Animation
stops driving the corpse. The ragdoll settles for 2.5 seconds and the corpse
despawns after 4.5 seconds. Pool reuse restores bone poses and living collision.
The imported death clip remains an authoring asset; gameplay death uses physics.

Drops use the existing LootManager, crafting material definitions and network
WorldItemPickup system: 4-8 gold, 80% chance of 1-2 leather, and 35% chance of one
sap, with the normal rarity scaling. The two definitions previously lacked drop
prefabs. New leather/sap pickups are registered in both FishNet collections and
carry their own canonical definition reference so remote clients can resolve
their item IDs in a player build. This also makes these existing materials
collectible from other loot tables that reference them.

## Rebuild and verification

Run `Tools/Briarback/build_model.py` through the official Blender MCP to build
a separate scene in a fresh source pass. It refuses to overwrite existing
Briarback source/actions. Run `ground_animations.py` with the v001 rig active
to generate the corrected v002 export. Run `cloven_hooves.py` with the v002 rig
active to generate v003. These scripts preserve the earlier files.

With this project closed in Unity, run:

```powershell
.\Tools\unity.ps1 build-briarback
.\Tools\unity.ps1 all
```

The builder imports the model and the recorded-foley sounds staged in
`Artifacts/NaturalSfx/Briarback/v004/unity`, builds materials/controller/prefab,
wires loot and health UI, registers prefab collections and installs the night
definitions. It patches only the scene's night-definition list and preserves
other pending scene edits. BriarbackTests participates in the full CLI suite.
The independent clock can also be checked while Unity is open:

```powershell
.\Tools\Briarback\Test.ps1
```

Verified: closed mesh topology, UVs, full bone weights, Blender neutral/death
and hoof renders, all seven source clips grounded at 60 Hz, Unity import and grounded
rest-pose scale, imported animation samples at 60 Hz (within 2 cm of the ground,
worst sampled penetration 0.28 cm during hurt), serialized references, server-authoritative
transform setting, canonical loot and prefab registrations, deterministic budgets
for 128 seeds per night with 1-4 players, and 125 independent clock checks.
The v003 headless tests also simulate ragdoll gravity, impulse and floor collision
for two seconds in an isolated editor preview physics scene, verify pose/collider
reset, and check the shared trail's bounded pool, world-space spacing and fade.
Headbutt checks cover close/far attack selection, independent cooldowns, full
warning/recovery on long frames, frontal hit geometry, death/pool reset, controller
wiring, clip duration and sampled grounding of all three new animation states.

Live visual behavior and multiplayer gameplay remain unverified. Manual checks:

1. Start a normal solo night 2 and verify a grounded Briarback arrives through
   the normal wave system; confirm none appear on nights 1 or 3.
2. Dodge sideways after the warning appears. Confirm the rush keeps its direction,
   reaches no farther than the lane, hits once, stops at trees/buildings and leaves
   the full recovery opening. Check split hooves and the dust-and-stone trail
   during the rush, including fading when stopped. Repeat on sloped terrain.
   Stay within 2.2 m to provoke the faster headbutt; check its head-tuck/strike,
   frontal warning, ground burst on the strike, final locked dodge window and one 12-damage hit. Move outside
   that range to provoke charges, and check that trees block the headbutt.
3. Kill it and collect gold, leather and sap. Check inventory/crafting recognition,
   ragdoll collapse, despawn and a reused instance with clean pose, HP, warning,
   trail and attack state.
4. Repeat with a host and remote client, including joining mid-windup. Check
   warning-to-hit agreement, health-bar scaling, sounds and one set of loot.

No Unity UI control, Play Mode or commit was performed. Before any later commit,
follow AGENTS.md: clear generated terrain through `Tools/unity.ps1 clear-terrain`
and purge leftover `NavMesh-TerrainManager*.asset` files.

## Recorded-foley audio

The prefab now uses the natural sound set from `Artifacts/NaturalSfx/Briarback/v003`:
two takes each of charge warning, charge, headbutt, hurt, death and hoof contact,
plus two 0.38-second headbutt warnings. The v004 revision replaces all attack/warning
vocals with different recorded creature-growl performances and removes wood/creak
layers after the original attacks were heard as chair-like squeaks. Charge uses
coarse throat effort over dull hoof thuds; headbutt uses brief effort with a dull
body/punch contact at 0.06 seconds. Hurt, death and hunting hooves retain v003.
The original four audio GUIDs are preserved. All 14 clips are mono 48 kHz PCM,
preloaded with importer normalization disabled to preserve the designed gains.
Source credits and CC0 URLs are included in `BB_Audio_Credits.txt` beside the clips.
Hashes, editing receipts and installed GUIDs are retained outside Assets.

Client presentation selects variants without consecutive repeats. Replicated phases
trigger warnings and attacks once; headbutt playback seeks to the replicated age
so its recorded 0.06-second contact stays aligned. Stale headbutt attacks do not
replay. Recovery preserves the short headbutt tail. Hurt and hunting hoof contact
use separate spatial sources so they cannot interrupt the attack warning. Hooves
follow horizontal distance while hunting (0.75 m per step, at most one per frame,
0.18-second minimum interval); charge already includes hoof layers. Teleports
are ignored. Death stops living sounds and plays through AudioManager's spatial
pool when available, allowing the tail to survive enemy despawn. Pool reuse resets
selection/cadence state, and disable stops the attached sources.

`Tools/Briarback/install_natural_sfx.py` installs these files and serialized references
without launching Unity; `BriarbackBuilder` uses the same staged files on rebuild
instead of restoring the synthesized placeholders. Existing AudioDatabase and
WeaponAudioProfile mappings remain unchanged because Briarback owns its local cues.

The complete current builder set is `Artifacts/NaturalSfx/Briarback/v004/unity`.
The revision manifest and per-render receipts retain the new CC0 growl sources,
filters, gains and hashes. All 14 GUIDs were preserved; eight attack/warning files
changed and the six hurt/death/hoof files stayed byte-identical. A matched preview
is `v004/candidates/Briarback_Attacks_v004.wav`: two charge warnings, two headbutt
warnings, two charge cues, then two headbutts. Naturalness remains a listening check.

Audio integration validation: offline runtime/editor compilation and the 125
independent attack-clock checks passed; file hashes, mono/48 kHz PCM headers,
headbutt warning duration, importer settings and all prefab audio GUID references
were checked offline. Unity import and the added Editor asset assertions were not
executed because Unity was open. Audible quality and multiplayer playback remain
unverified. After closing Unity, run `Tools/unity.ps1 all`. Manually listen for both
warnings, the headbutt impact and tail, walking hoof cadence, hurt during a charge,
death through despawn, SFX/master mute, host/client agreement and pool reuse.
