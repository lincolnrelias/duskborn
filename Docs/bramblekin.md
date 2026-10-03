# Bramblekin — basic woodland enemy

Replaces the humanoid Swarmer visual and immediate melee attack with an original
small forest scavenger: moss-green skin, broad pointed ears, short tusks, an acorn
cap, leather apron, bark strap, three-toed feet and a knotted wooden cudgel.
The silhouette and earthy palette sit beside Briarback and Thornwing; it has no
charge, projectile or support ability, so those enemies retain their distinct roles.

## Combat and provisional tuning

32 base HP, 5 damage, 6.2 m/s movement, 1.65 m attack reach, 90-degree frontal
acceptance and a 1.1 m elevation limit. Compared with the old Swarmer (50 HP,
5 damage, 5 m/s, 2.5 m reach and instant melee), this is a faster, fragile basic enemy
whose pressure comes from numbers. The navigation radius is .32 m instead of 2 m.

Hunt → .55 s windup → .18 s swing → .75 s recovery → hunt. Facing locks when
windup begins. Damage resolves once at .09 s into the swing, hitting the nearest
living player in the forward reach with clear solid-obstacle line of sight. Multiple
player colliders do not produce multiple hits. Hitches never consume a newly entered
warning or recovery window. Knockback/flinch interrupts the commitment into recovery.
Players can sidestep the committed facing or retreat from the shorter reach, then
punish the recovery. Attack-speed scaling is not used by this fixed beginner cadence.

Budget remains 6, basic pool weight remains 1, and existing night/pool eligibility
is retained. Selection is weighted, not a guaranteed appearance. Bramblekin stays
in serialized `EnemyType.Swarmer` (value 0); enum values are not reordered. All
existing loot, gold and canonical pickup definitions remain unchanged.
These numbers are provisional and require playtesting, especially in large crowds.

## Installation and compatibility

`Assets/_Duskborn/Prefabs/Enemies/Bramblekin.prefab` retains the old basic prefab
GUID `6057c54b8da0c074db4c02e44c528894` and its original NetworkObject ID. The
enemy registry references its new Bramblekin component. Both FishNet collections
include it; the behavior list and cached association are explicitly rebuilt in
edit mode, and NetworkTransform is server authoritative. Targeting is Humanoid,
the Player layer is the hit mask, and the original damage-number config and
EnemyOutline rendering layer are assigned.

The original Swarmer script remains available for ArcherTest, which keeps its
existing weapon-driven behavior. The ranged asset generator updates ArcherTest
from its own prefab rather than cloning the new basic enemy. Thornwing's builder
and corpse-lifetime checks resolve the new basic prefab path.

EnemyBase owns health, death events, impulses and pooling. LootDropper owns rewards.
The existing 10 s corpse duration is retained. Ten articulated anatomical mesh parts
use nine physical joints at death, with disabled living bone colliders and pose
restoration on reuse. Living pose updates stop when dead or ragdolled. Idle, articulated
grounded steps, raised-cudgel warning, swing and recovery are procedural and follow
replicated action state; there are no imported clips or animation root motion.

## Production sources

Editable model, FBX, palette, previews and geometry report:
`Artifacts/Bramblekin/v003/`. Original model has 2,054 triangles, ten parts,
one material and a ten-color palette atlas. Every part is closed/manifold, has
nonzero-area triangles, UVs and unit source scale; foot soles start at z=0.
`Tools/Bramblekin/build_model.py` runs through the official Blender MCP in a new
scene and refuses to overwrite the versioned source. The Blender preview was
visually inspected, including the raised-cudgel pose.

Twelve mono 48 kHz PCM 24-bit foley cues cover strap windup, woody swing contact,
body hurt, body/cloth death and four grass footstep takes. Sources are existing
Kenney CC0 recordings; originals, licenses, SHA-256 hashes, transformations,
inspection results and audition reel are retained under
`Artifacts/NaturalSfx/Bramblekin/v001/`. These are physical cues, not creature voices.
Sources retain their original Ogg fidelity. No synthesized creature placeholder
or paid sound model is used. Playback applies SFX gain once; master gain comes
through AudioListener. Quiet positional steps have lower voice priority, and
one-shots trigger on action changes rather than each frame.

## Reproduction and validation

- `Tools/NaturalSfx/sfx.ps1 doctor` discovers working Python/FFmpeg/FFprobe.
- Run `Tools/Bramblekin/build_sfx.py` with Python for source-based sound exports.
- `Tools/Bramblekin/Test.ps1`: 15 independent attack-clock checks passed.
- `Tools/Bramblekin/Compile.ps1`: offline runtime/editor compilation passed.
- In an isolated copied project while the live Editor remains open, `Tools/unity.ps1 build-bramblekin` installs
  and validates imported meshes/materials/audio, targeting, registry, network
  associations, grounded pose samples, ragdoll handover/reset and canonical loot.
- Focused spawn checks: 240 seeded night/player-count scenarios produced 5,306
  basic spawns with all budgets bounded. Imported procedural pose samples cover
  61 points each for hunt, windup, swing and recovery without foot penetration
  below the .015 m tolerance.
- Final `Tools/unity.ps1 all`: compilation and asset validation passed, with all
  19 regression suites passing and zero failed suites. The test process emitted
  a shutdown warning about 12 persistent native allocations; its source was not
  isolated by this enemy task, and the test runner still exited successfully.

Manual checks remaining: start night 1, confirm the silhouette and cudgel warning
at gameplay distance, sidestep/retreat during windup, punish recovery, and try
slopes, trees and dense packs. Kill and recycle instances, collect rewards, and
check corpse collision/settling. Compare host/client movement, mid-action joining
and pooled reuse. Listen to the audition and the in-game combat mix; naturalness,
repetition, live slope contact and multiplayer visual agreement are unverified.
No Unity UI control or Play Mode was used during production. No commit was made;
generated terrain and unrelated working changes were preserved.

## Gait and killing-impact revision (2026-10-03)

Movement increased from 3.6 to 6.2 m/s; acceleration increased from 14 to 24.
The original closed geometry is split into thigh/shin/foot pieces without adding
triangles or changing the creature's appearance. Two-link knee solving animates
opposed 0.40 m foot strides with up to 0.12 m clearance, upright soles, crouched
weight shifts, torso bounce/twist and counter-swinging arms. Cadence follows actual
travel (1.6 m per cycle); footsteps occur at alternating landing phases. Rest
poses and pooling reset include every articulated part. Grounding was sampled
through the imported model, and 16 exact imported pose deltas were rendered in
Blender for inspection (`gait-preview.gif`). Live terrain appearance remains a
manual check. Editable v001 and the intermediate v002 remain recoverable.

Bramblekin's death impulse is now 22 N s: 65% is distributed by connected body
mass, with the remaining contact impulse applied at the supplied killing-hit
point. The nearest physical collider selects the struck limb, preserving torque
from off-center hits. Other enemies retain their existing impulse settings.
A per-life guard prevents repeated death impulse delivery, and reset clears it.
EnemyBase clears contact data after each directed hit and on reuse, preventing
a later undirected hit from borrowing an old impact. Direction still comes from
the authoritative melee/projectile hit and is replicated by the existing death RPC.

Isolated preview physics checked right/left/forward killing-hit momentum,
off-center angular response, duplicate delivery, and pooled reset. Final copied-
project compile, validation and all 19 suites passed. The existing 12-allocation
shutdown warning remains. Results are retained in `Artifacts/Bramblekin/v003/`
and `Logs/UnityCli/`; the live Unity Editor was not controlled or put in Play Mode.

## Connected upper-body motion (2026-10-03)

Shoulders compress and roll with alternating steps. Arm swings lag the torso,
while the head has a delayed neck spring, nod and counter-turn to keep its gaze
readable. The torso now coils into the cudgel windup, leads the downward swing,
and settles through follow-through. Smoothstep curves share identical normal
phase endpoints; upper-body pose damping also handles interrupted windups and
late replicated phase arrival. Locomotion continues during deceleration instead
of resetting legs when an attack begins. Cosmetic changes preserve attack timing,
movement speed, sound cues and killing-hit ragdoll behavior.

Imported gait and attack deltas include the runtime damping and can be rendered
with `Tools/Bramblekin/preview_motion.py` through Blender MCP. v004 retains these
previews without overwriting the prior model or gait revision. Live movement on
slopes and network observers still requires a gameplay check.

Validation: offline runtime/editor compilation passed; isolated copied-project
`Tools/unity.ps1 all` passed compile, asset validation and all 19 suites. Checks
include four normal animation boundaries, interrupted windup damping, imported
foot contact, ragdoll impulse and reuse. Rendered and inspected v004 gait and
attack poses using the actual imported model and runtime damping. Retained logs
are `Logs/UnityCli/bramblekin-v004-unity-*.log`. The existing 12-allocation shutdown
warning remains. No live Editor control or Play Mode was used.

## Club, goblin voice and doubled attack cadence (2026-10-03)

The short vertical cudgel is replaced by a heavier knotted oak club held forward
of the right fist, with a leather grip and readable blunt head. The club remains
in the right-arm rigid mesh, so it follows procedural motion and jointed death
without a separate network object. v005 preserves editable source and exports;
10 parts, one material and 2,096 triangles.

All attack phases run at twice the prior rate: windup .275 s, swing .09 s, strike
at .045 s, recovery .375 s; full nominal cycle .74 s instead of 1.48 s. Pose
curves and attack damping scale with this cadence; the established running gait
keeps its existing response. Serialized attack speed is 2. Health, damage, movement
and spawn cost remain unchanged; faster pressure requires live balance checking.

Sound v002 uses artisticdude's CC0 Goblins Sound Pack from OpenGameArt, plus the
existing CC0 Kenney foley. Four distinct vocal performances per windup/hurt/death
cue replace the earlier non-vocal placeholders. Four sleeve swishes accompany
club swings; four wood/body layers play on actual player contact through a
server-triggered observer RPC, avoiding hit sounds on misses. Four grass steps
remain restrained for crowds. Swing tails can decay naturally during recovery.
All 24 mono masters are 48 kHz PCM 24-bit with editable source, processing recipes,
license links and hashes retained under `Artifacts/NaturalSfx/Bramblekin/v002/`.
The lossless vocal source is 44.1 kHz stereo PCM 24-bit; Kenney's Ogg source fidelity
is preserved in the originals. No pitch-shift duplicates or synthesized creature
voices are used. Technical checks passed without clipping and with cue onset
below .05 s. `audition.wav` presents windup/swing/hit/hurt/death/step variations.
Role selection, naturalness, repetition and live combat mix await listening.

Rebuild the club through Blender MCP with `Tools/Bramblekin/build_club.py`, audio
with `Tools/Bramblekin/build_audio.py`, then install with `Tools/unity.ps1 build-bramblekin`.
Previous versions remain recoverable and existing imported asset GUIDs are preserved.

Final validation: 16 pure attack-clock checks passed, offline runtime/editor
compilation passed, isolated `build-bramblekin` and `all` passed with all 19 suites.
Imported fast attack poses were rendered and inspected; assets copied back match
the retained source/master hashes in v005/installed-validation.json. Existing
12-allocation shutdown warning remains. Live checks: verify club clearance during
turns and crowded attacks, .275 s warning readability, host/client contact sounds,
and listen for fitting voice roles and repetition at actual combat volume.

## Larger club and close approach (2026-10-03)

Club wood is twice as long around the fist with a 45% thicker head; the leather
grip keeps its original hand fit. v007 retains the editable source and export
with 10 rigid parts, one material and 2,096 triangles. Attack reach is 3.3 m
instead of 1.65 m, but attacks start only at 1.2 m. Navigation stops at .8 m,
inside the engagement threshold, so the enemy approaches rather than swinging
from the outer edge of its enlarged reach. A close commitment leaves a 2.1 m
radial retreat margin. The existing fixed facing, 90-degree sector, elevation
limit, solid-obstacle rejection, one-victim strike, fast cadence and recovery
remain in effect. Deliberate retreat or a committed sidestep should avoid the
strike; live feel and balance still require playtesting.

Focused checks cover close engagement versus outer damage reach, a small
movement remaining inside the sweep, the 3.3 m boundary and rejection beyond
it, plus facing and height restrictions. Reproduce with
`Tools/Bramblekin/build_large_club.py` through Blender MCP, then the usual
`Tools/unity.ps1 build-bramblekin`.

Validation: 17 focused clock checks and offline C# compilation passed. Isolated
`build-bramblekin` and `all` passed, then all 19 suites passed again after adjusting
the long-club follow-through and adding clearance coverage. Sampled minimum club
height is .00458 m above the flat sole plane. Imported attack poses were rendered
and inspected. Logs and installed asset hashes are retained with v007. The existing
12-allocation shutdown warning remains. Manual check: lure one enemy close, compare
a small retreat with moving beyond 3.3 m, test sidestepping and slope/crowd club
clearance, then compare host/client presentation. No live Editor control was used.
