# Crystal destruction identities

The 32 redesigned masters are installed with fresh identities in Resources/SFX/CrystalBreaksV2 and referenced explicitly by AudioDatabase.resources.crystalBreakBanks. installed-banks.json records each GUID, installed path and matching master hash. Historical Harvesting clips retain their GUIDs but are no longer selected for crystal destruction.

ResourceNode.ResolveDepletedClip chooses the explicit elemental bank before legacy per-node overrides. Element resolution supports child visual components and cached generated Node_<element>_Crystal scene nodes. Actual playback logs [CrystalAudio] with the node, element, selected live_v2 clip and duration; missing crystal banks report an error rather than playing generic ore destruction.

The previous destruction banks shared a dominant heavy glass source. This pass uses different leading materials, rhythms and decays:

- Flame: burst of recorded fire crackle and sizzling release, with a restrained woody pop.
- Nature: plant splinter and uneven leafy scattering.
- Storm: four recorded thunderclaps edited into compact cracks and rolling tails, with tiny metallic crackles.
- Earth: dense rock fracture and staggered gravel chunks.
- Frost: rapid tightly clustered brittle fractures and small glass chips, with a dry stop.
- Blood: wet splitting splash, fleshy contact and thick drops.
- Dark: reversed fabric rush into muffled collapse and dark rustling.
- Light: bright plate fracture, staggered ringing fragments and a longer decay.

Four variations per element use distinct physical takes or separate recorded events. No oscillators or generated noise. Source originals, CC0 license evidence, hashes, crop/gain/filter/rate/reverse settings and master measurements are in source/, recipes/ and manifest.json. Kenney OGGs retain their original lossy fidelity; Joseph Sardin/Axeline T. field/foley recordings retain original 48 kHz mono PCM24 WAVs.

audition.wav plays two variants each: Flame, Nature, Storm, Earth, Frost, Blood, Dark, Light. Perceptual quality is unverified by the agent. Compare these at matched playback volume, then start a fresh game session and deplete every crystal type alongside combat.

The prior Harvesting/v002/build.py is historical and regenerates the old breaks. Reapply this pass afterward if rebuilding earlier assets.
