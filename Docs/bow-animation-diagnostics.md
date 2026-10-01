# Bow animation diagnostics

## Remaining body-solve flicker and executed regressions (2026-10-01)

Latest capture `bow-20261001-143554-110.json` contains 1,885 frames. The player
is stationary (movement phase and planar speed zero), with full idle-locomotion
weight. Interrupted Load frames 8426, 9084, 9308 and 9621 produce roughly
10.8–11.9 degree hip/foot rotation steps. The same rotation in the hips and feet
isolates a body-solve change rather than a directional stride reset.

BowLocomotionBodyAnchor samples humanoid bodyLocalPosition/bodyLocalRotation
after locomotion mixing, then restores them after the masked action mix, before
final humanoid evaluation. A humanoid Body mask can otherwise change the body
pose that drives the hip solve even with both legs excluded. This preserves the
locomotion body pose instead of compensating leg transforms in LateUpdate.
The existing bounded upper-body correction still runs after Animator evaluation.
The new anchor is enabled only for authored bow locomotion (including retained
movement through retries/cooldown). Other actions keep their original body solve.
Graph destruction precedes native pose-storage disposal.

After the Editor was closed, `Tools/unity.ps1 all` completed successfully:
compilation, asset validation and all 15 test suites passed. The new comparison
checks 1,620 poses in idle and all eight directions, with partial/full bow weights,
abrupt cancellation and redraw. Hips, upper legs and feet match locomotion-only
playback: maximum recorded difference is 0.000 degrees / 0.000000 metres.
Foot-contact calibration checks and all 81 direction/idle transition pairs also
executed successfully (maximum foot rotation per 60 Hz frame: 30.84 degrees).

Executing the previously compile-only eight-direction tests also exposed an
overly broad convergence assertion. Some retargeted poses require more than the
existing 60-degree distributed correction budget; a measured case retains 69.32
degrees of reference error. Tests continue to require exact bounded reduction
and unchanged lower-body poses, and require less than 5 degrees of held reference
error when the input is within the budget. Larger twists are not forced through
the waist. Perfect torso reference alignment outside that budget is not claimed.

These are non-interactive graph tests, not visual verification. Repeat stationary
aim/repeated shots from the latest capture, then strafe, reverse and move diagonally.
Check that feet/hips no longer twitch during rejected draw retries. Also inspect
torso heading/skin deformation, jump, release aim, dodge and switch weapons.

## Full directional gait review (2026-10-01)

Latest capture `bow-20261001-121058-550.json` contains 2,109 frames spanning
forward/backward, left strafe and left diagonals. The interrupted-draw 100+ degree
foot snaps are gone; full authored movement weight is retained. Maximum foot
rotation between consecutive moving samples is about 17.2 degrees. Rightward
directions are absent from this capture, so it cannot establish their appearance.

The earlier movement setup mixed Basic Run (16 frames per cycle) with Strafe
(28 frames). An offline reconstruction of the ASCII FBX hierarchy/linear key
curves revealed mismatched left/right foot lift phases, especially backward and
left strafe. Source curve analysis is not Unity humanoid retargeting validation.

WoodenBowAnimations now references all eight vendor Archer Run takes, including
dedicated diagonals. These share a 20-frame cycle and consistent upper-body
heading while the pelvis turns. Source forward/side/forward-diagonal foot-height
profiles align. Backward and backward-diagonal samples require a 0.7578125 cycle
offset; normalized contact mismatch falls from about 0.246 to 0.011 in source
sampling. The runtime blends only adjacent directions around eight octants.

The same movement playables now survive shot completion/retry/redraw: only the
action clock restarts, discarding stale shot requests. Idle has an independent
clock, and walking phase stops at zero movement. The Archer root-motion variants
cover 3.180065 metres per cycle; this distance calibrates cadence against actual
local player horizontal speed, without applying root motion. Non-owner playback
retains input-driven cadence. Capture version 7 records movement phase and all
nine movement inputs (idle plus eight directions).

Validation: offline runtime/editor compilation and 50 timing assertions pass.
Unity graph checks now include eight-direction asset selection, diagonal weights,
stride continuity/identity, idle cadence, distance-based cadence, retargeted foot
contact alignment and all 81 direction/idle transition pairs. These checks have
compiled but not executed while Unity is open. Run the documented Unity CLI after
closing the project. No natural-looking final result is claimed from source math.

Visual check: while holding aim, move in all eight directions, reverse forward
to backward and left to right, sweep through diagonals, then repeat while firing.
Inspect foot planting/sliding, knee bending, leg crossing, pelvis/waist turning,
shot/redraw seams and start/stop motion. Repeat downhill and with jumps; release
aim, dodge and switch weapons to verify cleanup.

## Residual snap during rejected draw retry (2026-10-01)

Latest capture `bow-20261001-120245-660.json` retains full movement weight across
normal shot completion/redraw. It also contains interrupted Load phases at
frames 2829, 3131, 3283 and 3442: the authored movement layer disappears, then
returns on a fresh Load. At frame 2829 the left foot changes approximately
153.7 degrees between consecutive samples. This is distinct from the original
action-weight dip. The capture does not record the cancellation caller.

Source inspection identifies RejectRangedDrawTarget as a retry path that calls
full CancelAction while aim remains held. It now requests movement preservation:
the rejected action disconnects, but the current gait keeps advancing through
the authoritative retry delay. Ordinary cancellation still removes movement;
preservation is conditional on Combat.IsAiming. The existing phase transfer
continues that same stride into the next accepted/retried draw.

Offline runtime/editor compilation passes. Unity remains open, so graph execution
and visual quality are unverified. Repeat sustained strafe with repeated shots
until a recovery retry occurs; the authored movement layer should remain present
at weight 1 while grounded even when Load is interrupted. Release aim and dodge
after a retry to check cleanup.

## Strafe flicker at redraw (2026-10-01)

Version 6 capture `bow-20261001-115220-921.json` shows steady grounded strafe
through repeated shots. At redraw frames 716, 863, 1015 and 1164, authored
movement weight falls from 1 to 0.175, 0.215, 0.217 and 0.204 respectively.
The movement mixer was following the upper-body action blend, briefly returning
the legs to the base controller on every Load/Release. A new playback also
restarted its stride phase on every draw.

Grounded authored movement now stays at full weight independently of the shot
blend, including the completion/cooldown handoff. A redraw of the same bow/set
inherits the retained movement phase and initializes its clip times and blend
weights before evaluation. Upper-body action timing still restarts normally.
Completion uses the actual animation ground flag instead of forcing a grounded
gait, preserving airborne locomotion during cooldown.

Offline runtime/editor compilation passes. Added a Unity regression for stride
times/weights across completed shot, cooldown and redraw; execution is pending
while Unity is open. Visual verification: hold aim, strafe left/right/diagonally
and fire repeatedly through several redraws. Legs should continue their stride
without snapping to the run controller at each shot boundary. Also jump during
shot completion and release aim to check movement cleanup.

## Airborne aiming and sprint cap (2026-10-01)

Authored bow facing correction now remains enabled during jumps. Ground contact
selects the lower-body movement source only; it no longer switches the upper
body back to the older spine-only tilt correction. Existing distributed joint
limits and action blending still apply. The regression fixture adds 180 sampled
bow poses over the vendor jump clip, checking bounded reference correction and
unchanged hips/feet. These new Unity graph checks are compiled but not executed
because Unity is open.

PlayerController suppresses effective sprint while PlayerCombat.IsAiming and
caps smoothed input immediately at runMultiplier, including sprint-to-aim
transitions. Releasing aim restores the stored sprint toggle. Offline runtime
and editor compilation passes. Visual checks: aim and jump while moving left,
right and forward; start aiming during a sprint, toggle sprint while aiming,
and release aim to verify regular run speed during aim and sprint restoration.

## Downhill contact gaps (2026-10-01)

Latest saved capture inspected: `bow-20260930-164407-259.json` (version 4).
Its 244 Hold frames have no base-controller transitions; it predates authored
aim locomotion and does not record ground contact. It cannot establish the
cause of flicker in the current setup.

Current source switched authored aim locomotion and facing correction off on
each false IsGrounded frame. PlayerController now retains animation grounding
for 100 ms after contact loss, preventing brief downhill contact gaps from
switching the gait. Explicit jumps clear the grace immediately. Walking off a
ledge can retain the grounded animation for at most 100 ms; movement physics
and jump eligibility are unchanged. Version 6 records controllerGrounded and
animationGrounded alongside aim-layer weight to distinguish contact chatter
from action blending in the next reproduction.

Offline runtime/editor compilation passes. Visual behavior remains unverified.
In a user-started session, hold aim and walk downhill in several directions,
then jump and walk off a ledge. Check stable gait on slopes, immediate jump
animation, and prompt airborne animation at ledges; save a new capture if
flicker persists.

The bow asset currently references Kevin Iglesias's
`Archer Animations/Animations/Combat/Archer@BowShot01.fbx`.
The unused custom RangedPoseTimeline was removed. The asset generator now
references this vendor clip and the existing BowMask, so regenerating assets
cannot restore the old custom animation reference.

## Capture a reproduction

1. In a session you start yourself, equip the bow and select the live player's
   root object (not the prefab asset).
2. Open **Duskborn > Diagnostics > Bow Animation Capture**, click
   **Use selected player's action component**, label the scenario, then **Start capture**.
3. Return focus to the game. With the camera level and facing fixed, stand still,
   hold aim, then move forward, backward, left, right and diagonally, about two
   seconds each. Fire while moving in each direction. Repeat once without aim
   and once with sprint. Use separate labeled captures if needed.
4. Click **Stop and save**. Captures also stop after 60 seconds, 3,600 frames,
   target deactivation/destruction, window closure or leaving the session.
   JSON is written to `Logs/BowAnimation/bow-<UTC timestamp>.json`.
   The window prints the exact path. A failed save can be retried while open.

This is opt-in, editor-only instrumentation with no prefab wiring. It never
changes masks, parameters, transforms, clip times, movement or gameplay.
No per-frame capture data is allocated without a subscriber. Recording allocates
diagnostic data, so it is not a performance benchmark. Avoid changing weapons
or rigs during a capture: mask and asset metadata are recorded at the start.
Start while the bow is drawn if you want active action source paths in metadata.

## Evidence and interpretation

Every sampled LateUpdate records the weapon graph's actual controller layers,
current/next clip names and weights, state hashes/times and transition progress,
controller and Animator VelocityX/Y, overlay weight/time/speed, draw/release state,
root-motion flag, mask name, stored aim pitch and CharacterController velocity
in player space. Capture metadata includes controller/avatar/clip asset paths and
humanoid/transform mask flags.

For hips, spine, chest, upper chest, hands and feet, frames include local
quaternions and rotations/positions in Animator space. Torso left lean measures
the hips-to-chest axis projected onto the Animator's XY plane; positive is left.
Bone left lean uses each bone's authored local Y axis and must be compared
against that same rig's stationary baseline. Missing humanoid bones are omitted.
The sampler runs in WeaponActionPlayer.LateUpdate; later script-driven bone
changes in other LateUpdates are outside this sampling point.

- Full bow weight with lean correlated to hip rotation suggests inherited
  locomotion motion below the mask. A full-weight upper-body override does not
  guarantee an upright torso when its parent pelvis tilts.
- Weight below one during the held pose suggests unexpected blending/timing.
- Incorrect directional clips or controller parameters disagreeing with Animator
  parameters suggests blend-tree input or playable parameter routing.
- Lean already present while stationary suggests the vendor pose, retargeting,
  avatar setup or mask needs isolated investigation.
- A spike only around release/redraw suggests transitions rather than steady aim.
- A held pose with nonzero stored pitch but unchanged bones is expected currently:
  pitch is not applied to the skeleton.

These observations identify hypotheses, not proof of causality. The next step
following recording is to compare matching directions and aim phases, then isolate
the implicated layer on an offline duplicate rig. Do not compensate with arbitrary
spine rotations before that comparison.

Target behavior: stable bow/torso direction throughout draw, hold, shot and
recovery, with natural feet and hips while moving in all eight directions.
Genshin-style aiming is the user-provided visual target; movement tuning has
not been changed in this diagnostic pass.

## Validation

`Tools/RangedCombat/Compile.ps1` checks runtime/editor C# offline.
`Tools/RangedCombat/TestTiming.ps1` checks the authoritative ranged attack gate.
Unity import, capture UI operation, actual pose output and visual quality still
require verification. Do not run `Tools/unity.ps1 all` until the project is closed
in Unity. No automated runtime reproduction has been collected yet.

## Recorded baseline: 2026-09-30

Analyzed both user captures, prioritizing `bow-20260930-160741-946.json`
(1,278 frames). The earlier capture begins with a Stone Axe, so its initial
WeaponMask metadata does not describe its later bow actions.

In the latest capture, all 202 held-pose frames have action weight 1, clip speed
0, and clip time 1.380791664 seconds. Animator and playable movement parameters
match throughout; there are no controller state transitions or root-motion frames.

While holding aim with Run01 - Left dominant, torso lean averages 4.80 degrees
left (113 frames, range 3.53 to 6.06). With Run01 - Right dominant it averages
1.19 degrees right (60 frames). The largest left lean is 8.24 degrees during
release, still at full bow weight. These are frame-weighted statistics, and
some samples include blend-tree mixtures; they are not isolated single-clip tests.

The decisive held-pose comparison: local spine/chest/upper-chest/hand rotations
stay constant (within floating-point noise), while their rotations in Animator
space change by up to 66.20 degrees relative to the first held frame. The hips
change by the same amount. This is total 3D rotation, mostly directional turning,
not a 66-degree sideways lean. The held upper-body pose is therefore being
carried by its moving pelvis parent. Increasing overlay weight cannot eliminate
this inherited movement.

The existing mask's transform paths refer to DummySkeleton/B-hips, while the
active Ironroot rig uses Hips/Spine. Humanoid mask flags still apply; path mismatch
alone does not prove masking is broken, since the held upper-body locals are
constant. Future isolation must use the active avatar and actual bone hierarchy.

Correction target: preserve the Kevin Iglesias clip's upper-body orientation in
player space while retaining locomotion hips and legs. Validate this on an
isolated duplicate rig before applying it to gameplay. Do not override pelvis
rotation without also addressing the feet, or add a fixed compensating roll.
A source-pose-based upper-body anchor or compatible directional aim locomotion
needs comparison against this baseline, including draw/release and diagonal
movement. No gameplay animation correction was applied in this analysis pass.

Repeat the analysis with `Tools/RangedCombat/AnalyzeCapture.ps1` (latest capture)
or provide `-Path`. It checks quaternion math, filters active bow frames, and
compares the same paused clip time. Version 2 captures explicitly record whether
the action input is connected and report zero effective action weight otherwise;
version 1 could show a stale mixer-slot weight during Locomotion.

## Revised correction after collapse report (awaiting visual verification)

The first full-orientation anchor was incorrect. The latest recording,
`bow-20260930-162822-531.json`, contains 1,073 frames. Its spine reference error
was zero at full weight, but the local spine rotated up to 170.95 degrees while
strafing left. The check measured target orientation and missed the resulting
waist deformation. Copying the source clip's complete sideways archer heading
against an opposing locomotion pelvis concentrated excessive twist at one joint.

BowPoseAnchor now uses the shortest swing that aligns the spine up axis toward
the source clip's up axis. It does not copy the source heading. This correction
is capped at 25 degrees relative to the incoming blended pose and scaled by the
existing action weight. The cap is an incremental correction budget, not a claim
about anatomical joint limits. No pelvis, leg, position, movement or timing data
is changed. The same vendor clip and playback clock still provide the reference.

This is a limited correction for the lean and the introduced waist collapse.
It deliberately retains directional torso turning. Full Genshin-like aiming
still requires compatible directional aim locomotion and camera-pitch posing;
forcing a larger spine twist is not a substitute for those animations.

A quaternion-only reconstruction of 137 held frames, using the original
capture's local spine pose at the same clip time, predicts a maximum local spine
angle of 11.51 degrees (captured full-lock result: 170.33 degrees). The largest
modeled tilt correction is 9.22 degrees. This is mathematical evidence only,
not Unity graph execution or visual verification.

### Verification

- In a user-started session, hold aim and strafe left through several full strides,
  then shoot. Repeat right, backward and diagonally, and turn the camera.
- Compare **Stabilize Bow Pose** enabled and disabled on WeaponActionPlayer.
  Enabled should reduce tilt without folding/twisting the waist. Feet should retain
  their original animation. Release aim, dodge and switch weapons to check cleanup.
- Version 4 captures display **Bow anchor / tilt error**. Full quaternion reference
  error is retained for investigation but zero is no longer a success criterion:
  heading is deliberately free. Tilt error should decrease; it may remain nonzero
  when the correction reaches its bound or is blending in/out.
- Local spine rotation and skin deformation matter as well as upright orientation.
  The tests now check correction magnitude and reject yaw-only correction.

The implementation uses Unity animation jobs and
[TransformStreamHandle.SetRotation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animations.TransformStreamHandle.SetRotation.html).
The graph dependency is vendor clip -> reference sample -> layer mixer -> bounded
spine tilt correction -> Animator. It allocates no duplicate rig at runtime.
Cancelling sets weight to zero; graph destruction precedes native storage disposal.

Offline runtime/editor compilation passed. The compiled BowPoseAnchorTests suite
is wired into RangedCombatTests. Its cloned-rig graph checks cover nine directions,
two headings, four clip times and three blend weights, unchanged lower-body bones,
and cancellation. Additional math checks cover opposing headings and tilt bounds.
These Unity tests have not executed because the project remains open in Unity.
Run Tools/unity.ps1 all only after closing this project in Unity. Visual quality
and the collapse fix remain unverified until a new user-run session/capture.

## Authored bow phases and aim strafes (version 5)

The tilt-only change above could not keep the bow facing forward. Inspection found
unused vendor assets already in the project: Archer@BowShot01 - Load / Hold /
Release and BasicMotions@Strafe01 - Left / Right. WoodenBowAnimations now wires
these takes to the bow's action. No replacement/custom animation was authored.

RMB starts Load, then loops the distinct Hold take indefinitely. An explicit
shot request permits Release. Manually advanced, zero-speed clip playables
prevent automatic graph time from crossing a hold boundary. The clock queues
early requests, discards old requests on a fresh draw, and maps release time back
to the original combined-clip projectile event. Server shot validation is unchanged.

Grounded aiming replaces the general directional run blend with forward/backward
runs and the dedicated Strafe takes. Cardinal clips share a normalized gait cycle;
diagonals blend the two adjacent directions. Movement speed is unchanged, so foot
sliding still needs checking against the authored strides. Local-owner cooldown
retains aim locomotion while RMB stays held. Observer playback uses the new phases
and aim movement during active draws; observer cooldown gaps remain to be checked.

With compatible locomotion selected, the residual upper-body facing difference is
split across Spine/Chest/UpperChest (20 degrees maximum at each joint). It cannot
put the previous 171-degree correction through one waist joint. Differences beyond
60 degrees remain as residual facing error rather than forcing more deformation.
When the complete chain is unavailable, only bounded tilt correction is applied.

Version 5 capture includes the actual Load/Hold/Release clip, shotRequested,
an Authored Aim Locomotion layer with clip weights, bowFacingEnabled and
bowUpperChestReferenceError. The underlying Base Layer may still show directional
runs even when the aim movement layer replaces its contribution. The Hold clip is
now a loop, so fixed-clock comparison of bone local rotations is disabled by the
analyzer for version 5. Inspect the actual phase and upper-chest reference error.

Validation: 47 offline clock/server assertions pass, including a long RMB-only
hold, a long frame across Load, early release, explicit release, completion and a
fresh redraw. Runtime/editor compilation passes. AuthoredBowPlaybackTests adds 660
Unity graph pose comparisons: Load/Hold/Release, left/right/diagonal motion, upper
joint limits, lower-body invariance and bounded target convergence. It is wired
into the existing RangedCombatTests suite. Unity graph execution remains pending
while the project is open; these tests have only been compiled.

Manual checks once imported: hold RMB alone for ten seconds (no Release clip or
projectile); move left/right/back/diagonally; click/release LMB early and after a
long hold; keep RMB down through several shots; release RMB, dodge, switch weapon
and repeat with an observer. Check torso heading, waist shape, hold/release seams,
feet, cooldown gaps, and one projectile per explicit shot request.

## Completed Unity validation: 2026-09-30

After the project was closed, the documented Unity CLI compiled the project and
validated its assets. The final regression run passed all 15 suites, with zero
failures (Logs/UnityCli/test-results.json).

Executing the previously compile-only tests exposed that an in-graph transform
correction could change the final humanoid hip solve. The implementation now
samples only the vendor reference in the graph, then applies bounded corrections
to the already evaluated spine transforms in WeaponActionPlayer.LateUpdate.
Diagnostics sample immediately after that correction. The graph's pose output
is otherwise untouched; the hips/feet are not corrected or counter-rotated.
Editor test clip clones use AnimationUtility to clear demo events safely.

Passed checks:

- BowPoseAnchorTests: 1,080 pose comparisons plus cancellation, across headings,
  clip times, weights and directions, with correction limits and lower-body
  invariance. Partial-weight math uses the corrected graph's actual input pose;
  separate-rig comparison is retained for hips and feet.
- AuthoredBowPlaybackTests: 960 Load/Hold/Release comparisons. A held bow stays
  within 5 degrees of its authored upper-chest orientation in all eight movement
  directions and idle. No joint receives more than 20 degrees of added rotation.
  Hips and feet match the uncorrected aim-locomotion fixture. RMB-only sampling
  never enters Release; an explicit request finishes the release sequence.
- Offline clock/server suite: 47 assertions, including long frames, long holds,
  early requests, duplicate requests, cancellation and fresh redraws.

These are non-interactive cloned-rig tests, not Play Mode or visual verification.
User-run checks are still needed for skin deformation, gait/foot sliding, seams
between takes, camera pitch, reticle alignment and multiplayer. The earlier
"tests pending" notes above describe the history and are superseded by this run.
