# Gathering audio pass

Harvest contacts have up to 5.5 dB more source gain, capped at -1.2 dBFS. AudioManager now applies master gain once through AudioListener, leaving source gain controlled by its category. At master 50%, central sounds now receive 50% master gain instead of 25%. Saved volume preferences remain respected.

Tree depletion replaces the door-like creak with recorded breaking twigs, a dense wood contact, and leafy ground debris. Existing tree_fall WAV GUIDs are preserved.

Each crystal element uses four distinct glass contact takes and four destruction takes, with material texture: flame/fire crackle, nature/foliage, storm/tin chatter, earth/mining grit, frost/dry glass fragments, blood/water, dark/damped glass and soft body, light/bright plate contact. Element comes from ElementalCrystalNodeVisual and travels with confirmed-hit audio feedback; particle surface tags retain their original mapping.

Material collection uses four wood, stone/ore, crystal, or soft-material takes, each no longer than 250 ms. Rounded material contact and cloth rustle replace rarity chimes. Local collection is nonspatial and a 90 ms interval limits overlapping confirmations; every material still enters the inventory before audio throttling.

All 98 candidate masters decode as 48 kHz mono PCM24 with peaks below -1 dBFS. No synthetic generators were used. Sources, licenses, hashes, exact crop/gain/filter/playback-rate recipes, and source fidelity are in manifest.json and source/. Kenney packs and the Joseph Sardin/Axeline T. recordings are CC0; their original license evidence is retained.

audition.wav order: tree break; flame, nature, storm, earth, frost, blood, dark and light (one hit then one break each); wood, stone, crystal and soft pickups. Agent perceptual audition is unavailable.

Validation: project's compile, validate and test commands succeeded in an isolated project copy. New regression checks resolve each element's correct bank despite ore flags, prevent adjacent repeated takes, and check pickup durations and material routing.

Manual check: start a fresh game session, hit and deplete a tree and each of the eight crystal nodes, then collect a pile of materials. Compare clarity at your usual Master/SFX settings and check repeated harvesting alongside combat. Live playback and perceived naturalness remain unverified.
