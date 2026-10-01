# Natural sound setup

Installed skill: `natural-game-sfx` in the personal Codex skills folder. Invoke `$natural-game-sfx` in a new chat, or ask for natural game sounds; automatic discovery remains enabled. The maintainable source copy lives in `Tools/NaturalSfx/natural-game-sfx/`. If the source copy changes, update the installed copy too.

The workflow is free-only: recorded foley, free commercially usable libraries, and source-based layering. It uses FFmpeg/FFprobe and the bundled Python runtime without synthesis libraries, paid services, API keys, or pip dependencies. Natural texture comes from the original recording; a script can trim, layer, and shape it but cannot manufacture the missing detail of an actual physical performance.

Starter sources are in `Artifacts/NaturalSfx/Library/`: Kenney Impact Sounds (130 files) and RPG Audio (52 files). Each original ZIP and included CC0 license is retained; `sources.json` inventories original files and SHA-256 hashes. These are lossy OGG sources, so exported WAVs are editable masters, not recovered lossless recordings. Future source searches can use Freesound CC0 recordings or attributed CC BY recordings, with each file's terms checked. Keep audition files outside `Assets` until selected.

From the project directory:

```powershell
.\Tools\NaturalSfx\sfx.ps1 doctor
.\Tools\NaturalSfx\sfx.ps1 inspect 'path\source.ogg'
.\Tools\NaturalSfx\sfx.ps1 export 'path\source.ogg' 'path\candidate.wav' --channels 1 --gain-db -3
.\Tools\NaturalSfx\sfx.ps1 mix 'path\layers.json' 'path\candidate.wav' --channels 1
```

Exports are 48 kHz PCM 24-bit WAV. The helper refuses overwrites, keeps original bytes, and writes source hashes and edit settings to a neighboring receipt. For loop sources use `--fade-ms 0` and audition the seam. Mixing preserves layer gains and does not automatically limit peaks; inspect and reduce gains if necessary. See the skill's reference for recipe format and recording guidance.

Example request: "Use $natural-game-sfx to make six dry stone-axe hits on wood for Duskborn, with light and heavy variants, using free recorded sources. Give me previews before replacing game sounds."

Audition candidates at comparable volume for convincing material, clear attack, clean tail, and variation during repeated playback. After selecting and importing, manually check timing, distance, levels, and repeated actions in the game. This setup does not verify perceptual quality or Unity playback and does not alter game assets.
