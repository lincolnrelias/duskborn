# Briarback

Original low-poly bark-armored charging boar, integrated as a regular Duskborn
enemy. See [design and integration notes](../../Docs/briarback.md).

`v003` is the cloven-hoof production source/export; v001 and v002 preserve earlier passes.
The source includes seven Generic clips, a compact palette, 17 bones and a
1,842-triangle mesh. All versions include locally synthesized original sounds.
`v003/preview.png` and `v003/hooves.png` are inspected Blender renders, not Unity
gameplay captures. `v003/animation-validation.json` records 60Hz source sampling.

Reproduce with `Tools/Briarback/build_model.py` followed by
`Tools/Briarback/ground_animations.py`, then `Tools/Briarback/cloven_hooves.py`
through the official Blender MCP. With
Unity closed, `Tools/unity.ps1 build-briarback` creates/imports the game assets and
runs focused checks; `Tools/unity.ps1 all` runs the project regression checks.
`Tools/Briarback/Test.ps1` tests the independent attack clock without starting Unity.
The Unity builder adds three headbutt animation assets to the controller, alongside
the seven FBX clips. Close-range headbutt/charge selection and independent cooldowns
are covered by 125 standalone clock checks and the Unity integration suite.

Source geometry, palette and synthesized audio were authored locally for this
project. No provider charges or third-party art dependencies. Live animation,
slope contact, balance and host/client behavior still require manual playtesting.
