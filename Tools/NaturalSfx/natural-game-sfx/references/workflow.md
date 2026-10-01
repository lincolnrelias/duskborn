# Local setup and use

The sibling scripts/sfx.py uses the Python standard library, FFmpeg and FFprobe. There are no synthesis libraries or pip dependencies. FFmpeg/FFprobe were already available on this Windows machine at setup time. Discover Python using the workspace dependency tool if it is not on PATH; do not hardcode the bundled runtime version into future sessions.

This machine's starter library is at C:/Users/linco/Mugg/Artifacts/NaturalSfx/Library/. Its sources.json records pack URLs, included license files, and individual source hashes. It contains 130 Impact Sounds and 52 RPG Audio files. Inspect this local collection before downloading it again. The project launcher is C:/Users/linco/Mugg/Tools/NaturalSfx/sfx.ps1.

Run with the actual Python executable and absolute helper path:

```text
python sfx.py doctor
python sfx.py inspect INPUT.wav
python sfx.py export INPUT.wav OUTPUT.wav --channels 1 --gain-db -3
python sfx.py export INPUT.wav OUTPUT.wav --start 0.1 --duration 0.8 --channels 1
python sfx.py mix layers.json OUTPUT.wav --channels 1
```

Export uses short boundary fades for one-shots; use --fade-ms 0 for loops and separately verify or construct their seam. Start/duration are seconds. All output commands refuse existing destination/receipt files. Mix and export record source hashes and settings but cannot infer licensing; retain a source manifest beside the originals.

Mix JSON paths are relative to the JSON file:

```json
{"layers":[{"path":"body.wav","gain_db":-3,"delay_ms":0},{"path":"debris.wav","gain_db":-12,"delay_ms":25}]}
```

Mix does not normalize or limit automatically. Inspect peaks and reduce layer gains when necessary. Preserve a quiet source's signal/noise balance.

## Free source route

Use the user's own recordings or owned libraries first. For a phone recording, capture multiple isolated actions in a quiet space with several seconds of room tone, consistent distance, and no automatic enhancement when controllable. Different physical takes are more valuable than dozens of processed copies.

[Freesound licensing FAQ](https://freesound.org/help/faq/) explains its per-file licenses. Search for concrete materials/actions with CC0 filtering; verify the actual file's license rather than a search snippet. Record source URL, creator, license URL, download date, and required credit. CC BY can be used when attribution obligations are met. Do not bypass login, purchase a pack, or assume a site-wide commercial license. If downloads need user login, prepare a shortlist and use sources the user supplies locally.

For Duskborn, useful starting categories are dry wood/stone/metal impacts, leather and cloth handling, dirt/grass/gravel steps, plant debris, fire, water, wind, animal breath and growls. Work in a dedicated Artifacts/NaturalSfx/<sound-name>/ folder with source/, candidates/, and a manifest; keep bulky auditions out of Assets until selected.

## Starter libraries

- [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds): CC0 material impacts and footsteps. A useful small starter library; audition for the desired realism.
- [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio): CC0 inventory and equipment sources. Inspect the included license before use.
- [Freesound](https://freesound.org): broader real-world foley and field recordings. Filter to CC0, or CC BY with complete attribution. Some downloads require the user's login.

Keep each pack's original archive/license and a source manifest. Avoid bulk downloading multi-gigabyte libraries when a small material collection suffices. Never treat royalty-free as public domain, or a library license as permission to redistribute standalone samples.

## Recording recipes

- Footsteps: record multiple actual steps on soil, grass, gravel, and wood; capture heel/body and surface detail. Separate walk/run performances.
- Weapons: use safe prop movement, cloth/leather motion, and wood/stone/metal contact recordings. Layer contact with the material's short resonance and debris.
- Creatures: combine owned breath/growl sources with subtle animal/foliage/wood textures; small pitch changes may help scale, but extreme shifts often expose artifacts.
- Magic: start with natural water, fire, air, ice, or resonant metal and process to suit the action. Keep an identifiable attack and restrained tail for combat.

Capture several strengths and perspectives. Avoid clipping at recording time, leave headroom, and preserve original recordings. No script can restore missing physical texture to a basic sine/noise effect.
