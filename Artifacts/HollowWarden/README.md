# Hollow Warden — current prototype

Production checklist: [../../Docs/hollow-warden-production.md](../../Docs/hollow-warden-production.md).

`Reference/approved-concept.png` is the selected visual direction. `v002` is the
current grounded-foot revision; `v001` preserves the initial prototype. The source
is `v002/HollowWarden.blend`; its FBX contains one skinned mesh, 21 bones and twelve
prototype actions. The model has 1,018 triangles, two materials and a compact palette.
`v002/preview.png` and `v002/feet.png` are Blender renders.

## Reproduction

Run `Tools/HollowWarden/build_model.py` through the official Blender MCP in a fresh
file. It refuses to overwrite loaded HW actions. `ground_feet.py` revises the source
scene's eight foot islands and writes v002, exporting only the twelve source actions
through temporary NLA tracks. FBX_SCALE_UNITS is required to preserve animated scale.

Run `validate_model.py` with the source scene active. Set WARDEN_VALIDATION_DIR to
its version directory when the open Blender filepath differs. It creates a separate
FBX QA scene without including it in the saved production source.

With the Unity project closed, run `./Tools/unity.ps1 build-warden`. This imports the
Generic model, builds URP materials and an Animator, creates boss/root prefabs and
HUD, registers FishNet assets and runs targeted checks. Logged build errors fail the
command. The boss lives at `Assets/_Duskborn/Resources/Bosses/HollowWarden.prefab`.

Run `./Tools/HollowWarden/Test.ps1` for the independent C# encounter core. It may run
while Unity is open. Use `Compile.ps1` beside it for offline runtime/Editor compilation.

## Implemented

- One reachable night 3 spawn, finite night hold, one reward and progression to day 4.
- Server-authoritative Rootbreaker and Harvest Sweep, locked facing and deduplicated hits.
- Player-targeted delayed spike volleys, countdown ground warnings and core exposure only while channeling.
- Half-health transition and faster attacks; channel-only exposure in both phases.
- Replicated action snapshots, channel pose with open chest/core glow, ground warnings and boss HUD.

## Verification and remaining work

Blender checks passed for topology, volume, skin weights, UVs, loop endpoints and all
FBX clips. The Unity baked idle mesh measures 4.066 m high with sole Y=0. The latest
clean boss build and its asset/mechanic checks passed. The broader project run passed
compile and asset validation, but ten other test suites failed; consult
`Logs/UnityCli/test-results.json`. No claim of a fully green project is made.

This remains prototype animation. Live checks are required for walking foot contact,
slopes, joint intersections, warning-to-hit agreement, host/client synchronization,
cleanup and encounter balance. Boss-specific audio is pending. Follow the manual
night 3 checklist in the production plan. No Editor UI control, Play Mode or commit
was performed.

