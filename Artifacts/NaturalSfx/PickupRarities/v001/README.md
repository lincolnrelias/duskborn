# Item pickup rarities

Every rarity uses four distinct recorded foley performances. Common is a short wood/cloth pack contact. Uncommon adds a small clasp tick. Rare adds clear glass contact. Epic adds a brief plate resonance. Legendary adds staggered glass/plate closure. Cursed uses a darker wood/soft-body closure. No oscillator, generated noise, or electronic jingles are used.

There are 24 general pickup masters and 96 material/rarity combinations (wood, stone/ore, crystal and soft material). WorldItemPickup retains material identity and now passes rarity into collection audio. AudioDatabase.GetPickupClip resolves the corresponding general rarity bank without immediately repeating a take. Existing pickup_common through pickup_legendary and item_pickup WAV replacements preserve their meta GUIDs, so legacy references use the new foley too.

All masters are mono 48 kHz PCM24. Source hash/license evidence and exact crops, gains, filters, layer offsets and playback-rate edits are recorded in manifest.json and source/. Kenney source packs are CC0. Lossy OGG conversion does not restore fidelity. Perceptual audition is unavailable to the agent.

audition.wav plays two takes each in this order: Common, Uncommon, Rare, Epic, Legendary, Cursed.

Related motion fix: pickup flight runs in LateUpdate after player movement. DroppedItemVisuals hands off from interpolated Rigidbody physics to a kinematic body with interpolation disabled, preserves the visible transition pose, and prevents idle hover or delayed rarity sync from overwriting collection. Drop physics is restored if the target disappears. VFX follow after pickup movement. Coin magnet flight uses the same handoff.

Manual check: start a fresh session, harvest and collect while walking and turning the camera at a render rate above the physics rate. Look for continuous item and coin motion during attraction. Pick up each rarity repeatedly and compare cues at usual audio settings, including a pile of common materials. Live render smoothness and perceived audio balance remain unverified.
