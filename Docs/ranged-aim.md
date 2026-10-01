# Ranged aiming

Equip a ranged weapon and hold RMB to aim. The camera moves to 70% of its normal
distance (subject to its existing minimum and collision checks) and 85% of its
normal FOV. A centered crosshair matches the camera ray used to choose the shot
direction. Projectile gravity and world obstructions still apply.

LMB fires one arrow. Clicking during the draw queues that release until the
authored windup completes. Keeping RMB held starts another draw after recovery;
holding RMB alone never fires. Releasing RMB cancels an unfinished draw.
Movement, strafing, sprinting and jumping keep their existing speed and controls.

Dodge cancels the drawn arrow, gives the roll animation full control, and resumes
aim with a fresh draw after the roll's facing/recovery timer if RMB is still held.
Releasing RMB during the roll prevents resumption. With ranged equipment, Alt
is reserved for dodge rather than also toggling cursor unlock. Menus, building,
focus loss, death and weapon changes interrupt aiming.

The bow uses Kevin Iglesias's separate Archer@BowShot01 Load, Hold and Release
takes. Load completes into the looping Hold take. Only an explicit LMB-up shot
request permits Release, including when the request is queued during Load. A
manual clip clock prevents graph evaluation from overshooting into a release.
The combined clip still defines the authoritative projectile event timing; the
split takes map onto that timeline without counting time spent holding.

While aiming on the ground, a dedicated locomotion mixer uses the package's
forward/backward runs and forward-facing Strafe01 Left/Right takes. It replaces
the sideways-facing Run01 Left/Right clips and blends cardinal motions for
diagonals. Grounded aim locomotion is retained through the local owner's cooldown
while RMB stays held. Airborne movement falls back to the normal controller.

The authored bow upper body overlays this locomotion. The vendor reference is
sampled in the graph; correction is applied after humanoid evaluation in
WeaponActionPlayer.LateUpdate to avoid changing the humanoid hip solve.
Residual heading correction
is distributed over Spine, Chest and UpperChest, at most 20 degrees per joint
(60 total), scaled with the bow blend. This replaces the full rotation forced
through one spine joint. If the three-joint chain is unavailable, the existing
bounded tilt correction is used. The original pelvis/leg transforms from the
selected locomotion clips are not overwritten by the torso correction.

Camera pitch is stored/replicated but currently does not rotate the animated chest.
Unity graph tests pass for held facing in all eight directions and idle, joint
correction bounds, unchanged lower-body transforms, and RMB-only holds. Visual
gait, skin deformation, phase transitions and multiplayer still need a user-run check.
See [bow animation diagnostics](bow-animation-diagnostics.md).
Draw, release, cancellation and pitch are replicated to observers. The server
still validates windup, recovery and one projectile per draw generation.

Reference: [Genshin Impact's bow aiming](https://genshin-impact.fandom.com/wiki/Bow).
The aimed-shot presentation informed this implementation; RMB hold, normal-speed
movement and automatic resumption after dodge follow this project's request.
Elemental charging and damage bonuses are not part of this change.

The reticle uses curved bow limbs and a nock diamond. Its limbs tighten and turn
gold as the authored draw completes; a lower tension arc fills alongside it.
Release briefly expands the reticle. Server-confirmed enemy hits flash coral
diagonal ticks (gold and larger for critical hits).

Flying arrows have a tapered ivory/gold trail lasting 0.14 seconds; it stops
emitting on impact. Enemy/player impacts emit a short directional crimson spray;
world impacts emit ochre chips. Bursts are pooled with a cap of 24 active systems
and at most 32 particles each. Impact audio uses the existing flesh/surface clip
variants and SFX volume controls, with a clearer mix for the local shooter.
Impact classification is sent by the server, and duplicate messages are ignored.

## Validation

Offline runtime/editor compilation: `Tools/RangedCombat/Compile.ps1`.
Server/phase timing tests: `Tools/RangedCombat/TestTiming.ps1` (47 assertions).
Unity compilation, asset validation and all 15 project test suites passed on
2026-09-30. In-game visuals and multiplayer remain unverified.

Manual checks in a user-run session:

- Hold RMB, strafe/sprint/jump and look up/down. Confirm zoom, crosshair, both arms,
  held arrow/string, and normal leg movement. Hold for 10 seconds without firing.
- Click LMB early in the draw and again after a long hold. Each click should fire
  one arrow; keep RMB held to redraw. Check nearby walls still block the arrow.
- Dodge with Alt and Ctrl while drawing, fully drawn and after shooting. Confirm
  the crosshair disappears during the roll and returns only while RMB remains held.
- Release RMB mid-roll; open a menu; switch to melee; lose focus; die. Confirm no
  stale arrow, delayed shot, stuck zoom or upper-body overlay.
- Repeat with a remote client observing: confirm held pose, pitch, release,
  interruption and a single authoritative projectile, including with network delay.
- Strafe in both directions while drawing/releasing on each player avatar; confirm
  the bow remains upright and leg animation continues without foot sliding.
- Check trail contrast in daylight/night; hit a moving enemy, kill an enemy, then
  hit a wall. Confirm one blood burst/flesh sound for enemies and chips/surface
  sound for walls, with no blood on walls and no trails on nocked/embedded arrows.

Feedback shader import, mask behavior on the animated rig, effect appearance and
audio balance still require the manual checks above. Editor regression tests also
cover the runtime mask flags, feedback asset wiring and duplicate replica impacts.
