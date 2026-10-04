# Elemental crystals

All eight runestone families require mined elemental crystals at the Arcane Table.
Flame uses Flame Crystals, Venom uses Nature Crystals, Storm uses Storm Crystals,
Stone uses Earth Crystals, Frost uses Frost Crystals, Blood uses Blood Crystals,
Hex uses Dark Crystals, and Radiance uses Light Crystals.

Each recipe consumes 3/6/9 matching crystals for tiers I/II/III, alongside its
previous gathering and enemy ingredients. Weapon etching still consumes one
crafted runestone. Other elements cannot substitute for the required crystal.

Nodes use ResourceNode, MiningNode/Ore targeting, the existing harvest tier,
mining bonuses, LootDropper and FishNet authority. Each depleted node drops 3–5
matching crystals, without gold. Drops use the usual scatter, collection and
flight behavior. Resource definitions, icons and pickup prefabs resolve from
Resources in player builds, including remote clients.

Every world props configuration includes the eight sparse deposits, using the
same terrain, water, slope, boundary and spacing checks as other ores. The host
also adds missing crystal families when loading older pregenerated scene props;
existing deposits are retained and repeated checks do not duplicate them.

The eight original Blender models have UVs, faceted surfaces and 322–406
triangles each. Meshes are combined by material into 3–4 draws per node. Each
element has its own silhouette and particle motif. Crystal surface emission
and additive node particles are independent of combat debuff effect intensity.
Particles are capped at 80 per node, thinned past 35 m and stopped past 70 m;
shadowless glow lights are enabled only within 22 m of the camera. Depletion
stops the ambient node effects on clients through synchronized health changes.

Use `Tools/unity.ps1 build-crystals` to rebuild prefabs, materials, item definitions,
recipes, spawn definitions and both FishNet collections from the original FBX
and PNG files. The builder is idempotent and runs focused crystal tests.
`Tools/unity.ps1 all` includes the crystal suite. `capture-crystals` renders the
production prefabs and simulated particles through a transient neutral forward
URP renderer, with SRP batching disabled for immediate batch captures; production
renderer settings are preserved.

Validation covers all eight mining/drop chains, 24 real recipe transactions,
wrong-element and shortfall rejection, native particle simulation and depletion,
emissive materials, player-build resource loading, both prefab collections,
world configuration membership and seeded placement on a physical terrain
fixture. The full isolated project passed 23 suites, compilation and asset
validation. A static GPU preview was visually inspected. Live harvesting,
pickup flight and a second network client have not been observed.

Manual check: start a fresh session, find a crystal node, mine it with an ore-capable
pickaxe, collect its drops, then verify the corresponding Arcane Table recipe
consumes its crystals. Repeat with a remote client to check synchronized drops
and depletion effects.
