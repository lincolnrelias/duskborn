# Bow animation diagnostics

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
