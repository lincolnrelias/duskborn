# Briarback recorded-foley audition set

12 mono WAV masters, 48 kHz PCM 24-bit. Two takes each for windup, charge,
headbutt, hurt, death and hoof impact. This is the current delivery; v001 is
an earlier design pass and v002 is an incomplete intermediate render.

Play `candidates/Briarback_Audition_Matched.wav` to compare all takes. The preview
uses simple gain matching within each pair, with at least 3 dB peak headroom.
The individual masters retain their intended relative levels. No compression,
limiting, synthesized voice/noise, added reverb or denoising was used.

## Preview order and design

- 0.00 / 1.53 s: **windup 01 / 02**. Nasal grumbling effort, bark creak and a
  planted grass scrape. Each is 0.88 s, within the 0.9 s charge warning.
  Take 01 contains two short vocal efforts; take 02 uses a different excerpt
  and a different creak/ground performance. These long warnings need separate
  shortening for the 0.4 s headbutt warning.
- 3.06 / 4.49 s: **charge 01 / 02**. Different forceful grunts with earth contact,
  wood armor chatter and cloth motion. Each is 0.78 s. One-shot attack-start cues;
  these are not seamless locomotion loops or contact-confirmation sounds.
- 5.92 / 6.95 s: **headbutt 01 / 02**. Short effort and dense contact, with the
  impact 60 ms after the start. Each is 0.38 s; its short tail extends beyond
  the 0.18 s hit window into recovery. Different vocal and material takes.
- 7.98 / 8.97 s: **hurt 01 / 02**. Interrupted grunt and quiet bark contact.
  Each is 0.34 s. Take 01 uses a different original performance file from take 02.
- 9.96 / 12.06 s: **death 01 / 02**. Grumbling exhale and body landing at 520 ms,
  followed by settling wood, grass and cloth. Each is 1.45 s. Different excerpts
  and physical layers; the landing delay is an audition design, not physics tracking.
- 14.16 / 15.09 s: **hoof 01 / 02**. Compact grass/earth plant with body weight
  and a subtle hard bark knock. Each is 0.28 s, from different material takes.

## Sources and licenses

All source assets are CC0 1.0. Attribution is optional. Suggested credit:
"Briarback vocal foley: Joseph Sardin / BigSoundBank. Material sounds: Kenney.
Edited and layered for Duskborn."

- Joseph Sardin, [Grumpy pig, with the mouth #1](https://bigsoundbank.com/grumpy-pig-1-s1658.html)
  and [#2](https://bigsoundbank.com/cochon-qui-grogne-2-s1659.html).
  These are human mouth performances imitating pigs, not live boar recordings.
  Original mono WAVs are 48 kHz / 24-bit.
- Kenney, [Impact Sounds](https://kenney.nl/assets/impact-sounds) and
  [RPG Audio](https://kenney.nl/assets/rpg-audio), from the existing local library.
  Original material files are lossy Ogg; exporting to WAV does not recover detail.
- License: https://creativecommons.org/publicdomain/zero/1.0/

`source/` preserves originals, downloaded license pages and pack license files.
`layers/` contains editable processed layers. `manifest.json` and adjacent
`.receipt.json` files record source hashes, URLs, license, trims, rates, filters,
delays, gains, commands and output checks. The build script is
`Tools/Briarback/build_natural_sfx.py`; choose a fresh version folder before rerendering.

## Validation and integration

All 12 masters and both previews decode successfully, with finite samples,
zero full-scale output samples and no output clipping. Individual master peaks
are between -16.07 and -7.33 dBFS. Onsets are within 16 ms. Original vocal files
contain clipped peaks: the selected unprocessed vocal excerpts were checked
to exclude all full-scale samples. This does not prove all source distortion absent.

The agent has not listened to these files. Naturalness, tail cuts, repeated playback,
distance and crowded combat readability remain unverified. Listen to the matched
preview, then check selected takes at their delivery gains alongside other enemies.

The initial audition delivery changed no Assets. The subsequent authorized integration
installed all 12 takes plus two shortened headbutt warnings and wired variant pairs in
BriarbackPresentation and the prefab. `unity-integration.json` records installed hashes
and preserved/generated GUIDs; `unity/` supplies the builder's canonical audio files.
Hurt and hunting hoof cues now use separate spatial sources, and headbutt has its own
warning and attack cues. See `Docs/briarback.md` for timing and validation status.
Do not assign headbutt audio on confirmed damage: it is intended to cue the strike
once on clients even when the player dodges. Keep source/receipt files outside Assets.

Manual gameplay checks after integration: warning timing on both attacks, one sound
per attack on host/client, damage feedback during a charge, death sound through
ragdoll/despawn, hoof cadence on slopes, pooling reset and several enemies in combat.
Unity was not launched or controlled; gameplay and visual checks remain unverified.
