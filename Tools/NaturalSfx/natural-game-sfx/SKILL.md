---
name: natural-game-sfx
description: Create natural game sound effects using recorded foley, free commercially usable samples, and source-based layering. Use for impacts, footsteps, weapons, creatures, ambience, and organic fantasy effects.
---

# Natural game sound effects

The user dislikes generic synthetic sounds. Start physical sounds from real recordings or free commercially usable samples. This user selected a free-only workflow; do not route to paid audio models or introduce credentials. Python and FFmpeg are useful for editing and validation; do not default to oscillator/noise synthesis for physical or creature sounds. Use synthesis when the desired sound is explicitly electronic or as a restrained supporting layer.

## Source and design

- Infer the material, action, size, force, perspective, duration, environment, and gameplay role. Resolve only consequential missing choices. For Duskborn, favor tactile medieval-fantasy materials and organic creatures; preserve readability during crowded combat.
- Prefer existing usable sources before downloading. For physical realism, use recorded foley; for unusual creatures or magic, combine natural layers. If a source route is unavailable, report it and continue useful preparation; do not silently deliver synthesized substitutes.
- Read [references/workflow.md](references/workflow.md) for commands, free libraries, and recording guidance. Run the helper's doctor first. No Unity MCP or direct Editor control.
- Describe the sound brief with audible physical details rather than only names such as "epic hit". Example: "One heavy stone axe strikes a dry oak trunk. Sharp woody crack, dense dull body, a few loose bark fragments falling, short natural decay. Close dry foley recording, no music, speech, electronic tone, or exaggerated reverb."
- Record or source distinct takes, varying performance, force, and material. For repeated actions, normally offer 4-8 candidates when the task allows; pitch-shifted duplicates are not distinct performances. Start with a small audition batch before scaling.
- Keep sources intact. Layer only what helps: contact/transient, material/body, debris/tail; movement/cloth if relevant. Creature sounds should have breath, effort, and anatomy cues. Magic can start from fire, wind, water, ice, metal, or animal texture. Avoid making every sound a bass boom.

## Execution and delivery

- Stage source, candidate, and receipt files outside Assets. Use a new output per take. A sound request authorizes local source-based editing. Keep this workflow free-only.
- Track original URL/file, creator, exact license and attribution, hashes, sound brief, settings, and transformations. Prefer CC0 sources; CC BY needs attribution; do not use NC/unknown-license assets for a commercial game.
- Export editable WAV masters, usually 48 kHz PCM 24-bit. Mono normally suits positional one-shots; stereo normally suits ambience. Converting lossy source to WAV does not recover lost detail. Retain source fidelity details.
- Check decoding, duration, sample rate, channels, peaks, clipping, excess silence, onset, tail, and looping. Preserve attacks and breathing texture; avoid aggressive denoising, crushing compression, or normalization that raises room noise. Choose gain in relation to existing game sounds rather than forcing every clip to the same loudness.
- Audition at matched levels. Check repeated playback, variation, distance, and the combat mix. Numeric checks do not prove naturalness. If audio perception is unavailable, provide playable candidates and say perceptual quality is unverified; never claim to have listened. Loop metadata does not prove seamlessness: audition several repetitions.
- Deliver candidates for listening with absolute-path Markdown audio embeds, brief differences, and provenance. Import or replace game assets only within the user's requested scope; preserve existing .meta GUIDs on replacement. Inspect AudioDatabase and WeaponAudioProfile before wiring Duskborn variants. Read the project's CLI rules before Unity validation and do not launch the CLI while the project is open in Unity. State any unverified game playback checks concisely.
