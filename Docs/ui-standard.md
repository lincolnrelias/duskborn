# Duskborn UI standard

Status: proposed baseline, with interactive design prototypes. 2026-10-04.
Scope: crafting, functional buildings, station processing, and placement. This document establishes a proposed design contract. The Briarwood building catalog skin is implemented separately; see `building-ui-briarwood.md`. Flow and layout proposals here remain exploratory.

User direction following initial review: keep the current menu structure and flow. The desired change is stronger visual personality fitting the game. Layout alternatives below are exploratory only, not an implementation recommendation. Use `ui-art-direction.md` for the revised visual scope.

## Player promise

Every task menu answers, in this order:

1. What will I get? Name, output count, and one useful benefit.
2. Can I do it now? Station, discovery, materials, fuel, and destination capacity.
3. What do I do next? One primary action, or a precise instruction that resolves the blocker.

Use the same selection, cost rows, status language, and action placement across menus. Preserve station-specific mechanics. Building is infrastructure, not architectural construction.

## Visual language

- Restrained charcoal panels, warm ivory text, brass selection/action accent, moss success, rust warning. Keep fantasy character in frames and item art; task text stays plain.
- Opaque task panels. Do not place essential text directly over changing terrain. Reserve translucent overlays for placement previews.
- One strong accent per panel: its primary action. Selection uses a narrow edge marker and a subtle fill; readiness uses an icon and a word.
- Item art explains identity; labels explain meaning. Never depend on an icon, rarity color, or material silhouette alone. Missing icons use a consistent labeled fallback.
- No category rainbow. Rarity and quality belong beside the item name and must not compete with task availability.
- Flat surfaces, quiet separators, limited ornament, no glow behind body copy. Use sentence case.

## Tokens for Unity implementation

At the project's 1920 x 1080 reference canvas: title 28, section label 20, body/cost/action 18, secondary 16; avoid essential text below 16. These are proposed Unity reference sizes, not browser pixels. Retain existing scaler policy and validate effective size at each target resolution before adopting.

Spacing scale: 4, 8, 12, 16, 24, 32. Panel padding 24; cost row minimum 40; primary action height 48; pointer/focus targets minimum 44. Radius: 4 for controls, 8 for panels. Use one font family with regular and semibold weights. Keep long names wrapping; never shrink text to fit.

Initial dark palette: surface #14191c, raised #1e2529, text #ede8dc, secondary #b9b8af, structural line #485052, accent #dab47a, accent text #21190e, success #a9c797, warning #e5a88c. Treat these as candidate tokens; verify contrast in the actual Unity renderer. Body text contrast target 4.5:1; large text and focus/control boundaries 3:1.

## Shared anatomy

Header: task or station name, station context, close control with the current bound key.

Selection region: item name, meaningful category, and one availability phrase. Keep unavailable entries readable and selectable so the player can inspect the reason. Distinguish an undiscovered recipe from one with missing resources; do not reveal undiscovered recipe details unless the discovery design permits it.

Detail region: icon, name, output count, one benefit, station requirement, material rows, status, primary action. Optional extended stats follow behind an explicit Details disclosure. Keep the action and its reason visible while the list or ingredients scroll.

Requirements: label the numeric convention **Have / Need**. Show each material's full name, available count, required count, and `Enough` or `Need N more`. Repeat neither full ingredient lists nor long explanations on every catalog entry. Group duplicate costs, including material also consumed as fuel, before judging affordability.

Immediate crafting: `Craft Stone Axe`. Building catalog: `Choose location`. Placement: `Place Workbench`. Timed non-forge processing: `Queue Iron Bar`. Forge: `Load Iron Ore`, `Load Wood`, `Ignite`, then `Collect Iron Bar`. Never label a queued result as already crafted.

## Availability and feedback

Choose the primary blocker by validity: invalid/missing content; locked discovery; unavailable station; full output/inventory or full queue; missing inputs/fuel. Material deficits remain visible even when another blocker has priority. Gameplay validation stays authoritative.

- Ready: `Ready to craft` / `Ready to choose a location`.
- Resources: `Need 2 more Stone.` Each deficient row also displays its deficit.
- Station: `Use a Forge to craft this item.` A link to the build catalog may be added later, but never pretend the station is already accessible.
- Discovery: use the real discovery tracker condition. If it is only “discover an ingredient,” say that; do not invent a specific quest or source.
- Inventory: `Make room in your inventory.` Keep materials readable.
- Queue: `Queue full. Wait for a job to finish.`
- Output: `Output full. Collect materials to resume.` Make collection the next action.
- Pending: `Crafting…` / `Placing…`; disable repeat submission. Do not show success until confirmed.
- Success: `Stone Axe added to inventory.` / `Workbench placed.` Update costs immediately and retain selection.
- Rejected transaction: show the returned reason next to the action; preserve selection and retry context. Refunds and multiplayer authority remain in gameplay code.

Disabled controls retain readable labels. Show an explanation beside them, never only in a hover tooltip. Do not render unavailable actions as almost invisible.

## Crafting layout

Preferred starting direction: **Field guide**. A named list on the left and a stable task detail on the right. It scales to many recipes, accommodates localization, and makes readiness scanable.

Alternative: **Workshop tiles**. Larger labeled item tiles above a compact detail area. Useful when art recognition matters more than catalog density. Avoid hiding names or making the catalog so tall that requirements fall below the fold.

Show only inventory context needed for the task by default. Keep an inventory entry point; avoid forcing two dense full menus to compete. Inventory drag/drop remains available for station slots that require it.

## Building and placement

Use the same requirements presentation as crafting. Lead with the building's capability: Workbench prepares tools/light equipment; Forge handles fueled smelting/heavy equipment; Cauldron handles alchemy; Arcane Table handles crystals; Material Chest stores resources.

Selecting `Choose location` transitions to a compact placement overlay, restoring world visibility. Keep the selected building, exact cost, validity reason, and current key bindings visible. Show rotation and place/cancel bindings. Current defaults: R or wheel rotates, left click places, Esc or B cancels; F interacts with a placed station. Render bindings from HotkeyManager, not hardcoded UI text.

Pair preview color with a validity symbol and text. For obstacles: `Move to a clear area.` For slope: `Find flatter ground.` For reach: `Move closer.` For unsupported base: `Place the whole base on the ground.` Preserve detailed server rejection text when available. No resources are shown as consumed before placement succeeds.

## Stations and processing

The Forge has loaded input, loaded fuel, ignition, timed work, and collectible output. Keep **In bag** separate from **Loaded**. Having wood does not mean fuel is loaded. Never replace the actual forge workflow with generic batch crafting.

Show `Iron Ore → Iron Bar`, `2 Iron Ore + 1 Wood`, and `12 s` from recipe data, adjusted for station speed. Label active processing and collection as separate stages. A real queue uses capacity from the station definition, not a fixed assumption. Workbench/other stations keep their own queue behavior; Arcane Table preserves its specialized flow until separately designed.

Move/dismantle actions belong in station management, away from routine production. Dismantling requires a concise confirmation with exact refund and blocking contents; do not add confirmations to normal reversible menu selections.

## Prototype and source grounding

Interactive prototypes compare Field guide and Workshop tiles, each with Crafting, Building, Forge, and Placement. Design controls simulate availability and placement conditions. Costs are taken from Recipe_StoneAxe and Recipe_StonePickaxe (5 Wood, 5 Stone each), Recipe_FiberChest (10 Fiber, 5 Wood), Recipe_SmeltIronBar (2 Iron Ore, 1 Wood fuel, 12 s), and the five Build_*.asset definitions. Inventory, time progression, success, and placement diagrams are local illustrative state; no live game or server connection exists. Benefit copy is proposed copy, not verified item statistics. Forge loading deliberately shows minimum per-job loads for clarity, while current runtime loading can transfer a larger stack.

Source findings: CraftingUIManager uses several 7.5–12 px authored labels; BuildingUIElements uses legacy Text while crafting uses TMP; BuildingPresentation already supplies owned/required/missing costs and disabled reasons. Align these through shared tokens and reusable requirement/action components during implementation. Do not add a competing state machine in UI.

## Acceptance checks

1. A new player can identify the result, deficit, and next action within five seconds without a tooltip.
2. Verify missing materials, wrong station, locked recipe, full bag/output/queue, pending request, rejection, and success. Unavailable entries remain inspectable.
3. At 1280 x 720, 1920 x 1080, and ultrawide, no ingredient, blocker, or primary action clips. Test long translated names and 125–150% UI scale.
4. Verify actual runtime key bindings and keyboard focus; if controller support is introduced, make focus traversal and hints input-aware. Selection survives resource refreshes.
5. Verify placement over bright/dark terrain, blocked ground, steep slope, reach limit, and server rejection. Color-blind reading must still work from symbols/text.
6. Verify Forge loaded vs owned counts, ignition, fuel exhaustion, output full, collection, and multiplayer updates. Confirm no misleading success before server acceptance.

Browser interactions can be checked offline. Unity visuals and gameplay integration remain unverified until the above manual checks are performed under the project's permitted validation workflow.
