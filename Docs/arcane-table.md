# Arcane Table and runestones

The Arcane Table has a dedicated UI with Forge Runestones, Etch Weapon, and Arcane Crafts views. Existing station interaction opens it automatically. Escape and leaving the station close it. Existing arcane processing recipes retain their station queue path.

Eight rune families each have three craftable tiers (24 items). Tiers I, II and III apply 1, 2 and 3 stacks per damaging weapon hit. Enchantments belong to individual runtime weapons and survive backpack/action-bar moves. Weapon definitions and same-ID sibling weapons are never modified. Armor and accessories are reserved for a later implementation.

One rune is consumed per etching. Each weapon has one etching. Replacing one requires a second confirmation click and destroys the previous enchantment. Same-family duplicates and downgrades are rejected without consuming a rune. If an inventory callback removes the target during spending, the stone is refunded.

## Effects

- Flame: Ablaze, 2 damage per stack each second.
- Venom: Poisoned, 1.5 damage per stack each second; lasts 9 seconds.
- Storm: Charged, discharges at 6 stacks for 18 damage and a 0.4-second interrupt; consumes charge.
- Stone: Burdened, 4% movement reduction per stack.
- Frost: Chilled, 5% movement reduction per stack; freezes at 8 stacks for 1.2 seconds (0.35 seconds on the Hollow Warden); consumes chill.
- Blood: Bleeding, 1 damage per stack each second.
- Hex: Cursed, 3% increased incoming damage per stack.
- Radiance: Seared, 1 damage per stack each second and 2% reduced enemy damage per stack.

Each effect has a 12-stack cap. Hits refresh its duration; effects other than Venom last 6 seconds. Stone and Frost reductions combine with a 25% movement floor. Enemy death and pool reset clear stacks, source attribution, timers, and control effects. Periodic kills retain loot attribution to the player who applied the effect.

Weapon auras, projectile auras, debuff particles, discharge bursts, and overhead rune icons/counts communicate effects. Fire uses rising wisps; Venom bubbles; Storm jagged arcs; Stone falling chips; Frost snow crystals; Blood droplets; Hex broken rings; Radiance rays. Consumed charge/freeze displays one control-effect stack with its own short countdown. The dedicated URP shader is in Resources so it is included in builds.

## Crafting and item assets

Recipes combine gathered stone and elemental ingredients with leather drops. Higher tiers also require bone and crystals; tier III requires a Thornbark Core. Existing enemy drop tables already provide leather, bone, and the core. No changes to their drop rates were needed.

- Definitions: `Assets/_Duskborn/Resources/Runestones/`
- Recipes: `Assets/_Duskborn/Resources/Crafting/Recipe_runestone_*.asset`
- Sprites: `Assets/_Duskborn/Resources/Textures/Runestones/` (24 transparent 256×256 PNGs)
- Gameplay: `Gameplay/Enchanting/Runestone.cs`, `Gameplay/Enemies/EnemyRuneEffects.cs`, `Gameplay/Player/PlayerRuneEquipment.cs`
- UI: `UI/ArcaneTableUI.cs`
- Visuals: `Effects/RuneAura.cs`, `EnemyRuneVisuals.cs`, `RuneStatusBar.cs`, `RunestonePickupVisual.cs`

Inventory definitions and world-drop registration include runestones in player builds. Dropped stones reuse the existing registered stone pickup prefab, with the proper rune sprite and aura applied from the synchronized item identity.

The project currently uses a client-authoritative resource wallet and local inventory selection. Rune equipment follows that existing contract; clients publish a catalog-checked rune selection, and enemy damage, stacks, expiration, and control effects are calculated on the server. This does not introduce authoritative inventory ownership verification or equipment save/load; those require a broader inventory system. Ranged attacks snapshot their etching at draw acceptance so changing equipment cannot retroactively change a projectile's effects.

## Validation and manual visual check

`Tools/unity.ps1 all` runs compile, validation, and the project's static Editor suites. ArcaneTableTests covers all 24 asset references/recipes/sprites/drop prefabs; tier scaling, caps and proc thresholds; per-instance etching; exact consumption and replacement; stale-target refunds; reentrant callbacks; identifier validation; shader inclusion; UI population and reopening.

Visible rendering and multiplayer runtime behavior still require a manual check:

1. Open the Arcane Table. Craft a tier I rune using stone, leather, and its elemental ingredient. Check the sprite/name/count in inventory.
2. Etch a backpack or action-bar weapon. Check that exactly one stone disappears, the weapon tooltip updates, and a same-ID second weapon remains unetched.
3. Equip it and strike an enemy: confirm 1/2/3 stacks per tier, the weapon aura, enemy aura, and overhead stack count. Stop attacking and check expiration; confirm DOT deaths award loot.
4. Check Storm discharge at 6 and Frost freeze at 8 stacks. Replace an etching, confirm the destructive replacement, and test a ranged weapon and equipment swaps while arrows are in flight.
5. Drop and recover a stone. Check its world sprite, aura, and recovered tier. Check pooled enemies start without debuffs, and repeat on a second network client.

## Sprite generation

Generated with the built-in imagegen tool. One transparent three-tier strip per family was generated, then sliced and padded into 24 square inventory sprites without repainting their content.

Shared prompt: "Game inventory sprite asset: [family] runestone tier progression strip. Exactly THREE separate stones in one horizontal row, equal spaced centers at one sixth, one half and five sixths of width. Transparent background. Hand-painted low-poly medieval fantasy inventory icons. Each dark slate hexagonal stone bears a deeply etched glowing [symbol] rune. Tier 1 simple chipped stone; tier 2 silver bevel two inset gems; tier 3 ornate gold bevel three gems. All stones same scale occupying 70% of each square cell, centered with generous empty transparent margins, fully separate, no overlap, no text, no letters, no labels, no shadows outside stone. Wide image aspect ratio 3:1. Crisp silhouette readable at 64px."

The family/symbol pairs were Flame/orange flame, Venom/green serpent fang, Storm/yellow lightning bolt, Stone/ochre mountain, Frost/cyan snowflake, Blood/crimson blood drop, Hex/violet broken eye, and Radiance/gold white sun.

## Health and debuff presentation

Enemies and players share an iron-framed health track with numeric HP, quarter markers,
a delayed amber damage trail, and a debuff row above the name. Enemy vitality is crimson;
player vitality is teal. Enemy bars remain visible while debuffs are active. Player bars
start hidden, show after damage, and fade after the existing recent-hit delay. Existing gathering-node bars retain their style.

Each icon has a dim base, a clockwise radial image starting at the top for remaining time,
and a stack badge. Rune timing is replicated using FishNet network time, including refreshes
at the stack cap and the short Storm/Frost control durations. Late observers receive the
current deadline. PlayerStats exposes the same presentation source and forwards existing
Burn, Slow, Stun and Bleed statuses (Flame, Stone, Storm and Blood icons); invulnerability
is a buff and is excluded. These legacy statuses accept an optional stack amount.

A single composite surface shader chooses distinct rune patterns in actor space; it does
not add all colors into white or replace authored materials. Extra mesh passes follow
skinning and blend shapes, and do not participate in hit flashes or invulnerability material
swaps. Particles use different silhouettes and vertical bands, share an emission budget,
and preserve their colors when overlapping. Cleared effects disable the surface pass.

`RunePresentationTests` checks timing refresh/expiration, eight-icon layout, actor integration,
surface isolation and particle limits. `Tools/unity.ps1 capture-runes` produces a static GPU
preview using the actual actor/weapon prefabs. Combat motion, pooled reuse and multiplayer
clock synchronization still need an in-game check with two clients.
