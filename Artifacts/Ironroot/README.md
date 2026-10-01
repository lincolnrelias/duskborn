# Ironroot Vagabond player

Original low-poly model based on the selected second concept in `Artifacts/PlayerConcepts/02-ironroot-vagabond.png`.

## Current state

Installed on 2026-09-29 in both player prefabs, including the active `Player 1.prefab`. Unity imported a valid Humanoid avatar and URP materials. Existing prefab GUIDs, Animator controllers, gameplay roots and weapon references were preserved. The builder matched the prior visual heights/feet: Player = 2.363 m / -0.720 m; Player 1 = 2.745 m / -0.710 m. Existing scene and unrelated prefab edits present before installation were preserved.

Validation: `build-ironroot` and a fresh-process `test-ironroot` passed, checking all six skins and sampling 24 locomotion clips for each prefab. `all` passed compilation and reference validation; its broader tests reported 4 passing and 10 failing suites. Failures cover Building, SceneFurnace, CharacterPanel, Crafting, CursorAndMenuFocus, DayNightCycle, ItemTierDrop, SpatialOccupancyMap, UIResolutionScaling, and WorldHealthBar. See `Logs/UnityCli/test-results.json`; the entire project is not test-green. The first sandboxed Unity launch exited before producing a log; the authorized non-interactive run outside the sandbox succeeded.

The geometry is original and contains no third-party mesh or skeleton. Created locally through the official Blender MCP; no paid generation provider used. The optional game-dev CLI was unavailable, so this is a project-local source/export bundle, not a verified game-dev canonical package.

## Asset

- `v002/Ironroot.blend`: editable T-pose source, independent scene, materials, lights and camera.
- `v002/Ironroot.fbx`: Unity-targeted skinned export, with no animation clips (existing Humanoid clips will retarget).
- `v002/Ironroot.glb`: portable secondary export.
- `v002/preview.png`, `front.png`, `side.png`, `rear.png`, `pose-check.png`: Blender visual checks, not Unity captures.
- `v002/model-report.json`, `roundtrip-report.json`: measured geometry and Blender FBX reimport checks.
- `v001/`: preserved original export for regression comparison.

Approximately 1.819 m tall (soles at model Z=.015 m), 4,546 triangles, 53 bones including fingers, six modular skinned meshes. At most three influences per vertex. Named head/hair/torso/legs/boots/arms-hands regions support future equipment replacement. The builder measures geometry and aligns feet and height to each existing player visual.

## Hip clipping revision (v002)

The original capped thighs overlapped a rigid pelvis island and a hip-weighted tunic hem. Long strides pushed the trousers through the shirt. The corrected trousers are one closed manifold surface with a shared crotch seam, progressive thigh/hip weights, and symmetric three-bone weights at the crotch. The shirt is tucked above the hip articulation, and its waist and belt share Hips weights. Shirt and belt openings no longer contain internal caps. The belt tail is shortened to stay above the thighs.

`Tools/Ironroot/check_hip_deformation.py` reimports the original and revised FBX files and checks shirt/trouser triangle intersections in 20 deterministic poses (16 stride phases, neutral, high step, crouch and wide stance). It records the baseline and corrected measurements in `v002/hip-deformation-report.json` and renders matching close-ups. These are Blender stress poses, not recordings of Unity gameplay. The standard FBX round-trip check verifies normalized weights, UVs and nondegenerate faces.

The v002 FBX has been copied into `Assets/_Duskborn/Art/Models/Ironroot/Ironroot.fbx`; its existing metadata was preserved and the source/destination SHA256 values match (`23EDA6F85D03BEA6A724677E443B9EC11BBD442D7343D804CE4FAB0FAC3F9138`). All 20 Blender poses report zero shirt/trouser triangle crossings; the baseline reports crossings in all 20. The corrected trousers have one connected island and no non-manifold edges. Offline runtime/editor C# compilation and FBX round-trip validation pass.

The importer now refreshes already-installed Ironroot visuals through the same FBX GUID, preserving prefab overrides and existing sockets instead of recreating the visual hierarchy. The historical Unity pass above describes v001; Unity-backed validation of v002 is pending while the project is open. No Unity CLI or Editor control was used during the v002 correction. After the normal asset refresh, manually check walking, running, jumping and dodging from front and rear views; close the project before running `test-ironroot` for the fresh Unity validation.

This implements one base character and its starter clothing. It does not implement a full character creation menu, alternate body/face/hair presets, networked appearance selection, or new equipable armor assets. `IronrootAppearance.Apply` provides skin/hair/shirt/trouser tint and hair visibility as a local foundation. Armor needs fitted meshes and covered-body hiding rules in a later pass.

## Build and checks

Run `Tools/Ironroot/build_ironroot.py` through Blender MCP to regenerate, then `Tools/Ironroot/validate_ironroot.py` to render review views and assert FBX reimport geometry/skinning. Other Blender scenes are preserved. The saved `.blend` contains only the production scene and its dependencies.

`Tools/Ironroot/Compile.ps1` compiles runtime and editor source offline against the installed Unity assemblies, without starting Unity.

After closing this project in Unity:

```powershell
.\Tools\unity.ps1 build-ironroot
.\Tools\unity.ps1 test-ironroot
.\Tools\unity.ps1 all
```

The build creates URP materials, explicitly maps the Humanoid avatar, and replaces the visual child in both `Player.prefab` and the active `Player 1.prefab`, preserving their asset GUIDs, gameplay roots and existing Animator controllers. Gameplay Animator/bone references are remapped. Hand correction sockets preserve the old equipment profile axes and are used by the weapon handler and fitting studio. The builder validates modular skins, normalized weights, references and sampled Humanoid locomotion clips. It does not establish live gameplay correctness.

Before any future commit, follow AGENTS.md: run `clear-terrain` while Unity is closed and purge remaining `NavMesh-TerrainManager*.asset` files. No commit was made in this task.

## Remaining verification

Live gameplay visuals remain unverified. Manually check idle/run/jump/dodge and melee/ranged attacks from the gameplay camera; confirm feet touch ground, held weapons align, the character panel displays the new model, and host/client characters both use it. Check elbow/shoulder/crotch intersections in extreme animations. Blender renders and offline animation sampling alone cannot prove those outcomes.
