# Duskborn handout: building, stations, processing, and UI continuation

> **Audience:** The next implementation agent working in this Unity project.  
> **Immediate focus:** Stabilize the current building flow in Play Mode, then improve the player-facing UI for building placement, crafting stations, processing, storage, relocation, and dismantling.  
> **Project:** Unity 6000.4.1f1, FishNet multiplayer, legacy `Input` plus the Input System, runtime uGUI conventions already used elsewhere in the project.  
> **Working directory:** `C:\Users\linco\Mugg`

---

## 1. Product intent

Building is infrastructure for the crafting progression. It is intentionally focused on functional objects rather than architectural base construction.

```text
Explore → gather resources → build stations → process materials
        → craft better equipment → unlock more resources/stations
        → expand infrastructure
```

The current buildables are:

- Workbench: general equipment and basic material preparation.
- Forge: smelting, metal processing, and heavy equipment.
- Cauldron: alchemy, oils, and organic processing.
- Arcane table: crystal processing and arcane equipment.
- Material chest: storage for material stacks.

Keep the experience practical and legible. Do not expand into walls, foundations, structural integrity, power, or plumbing unless the user requests it.

---

## 2. What has been implemented

### Data and placement

`Assets/_Duskborn/Gameplay/Building/BuildableDefinition.cs`

- Data-driven `BuildableDefinition` ScriptableObject.
- Identity, copy, category, prefab, icon, station type, costs, optional recipe unlock, footprint, clearance, slope, ground tolerance, reach, rotation step, layer masks, multiplicity, movement/dismantling, storage capacity, queue capacity, and processing speed.
- Extensible `PlacementRule` ScriptableObject base for future special-case validators.
- `MaterialCosts` aggregates repeated ingredients and performs atomic batch spending through `ResourceInventory`.

`Assets/_Duskborn/Gameplay/Building/PlacementValidator.cs`

- Checks finite coordinates, player distance, center/corner ground support, uneven terrain, slope, overlapping geometry, existing stations, and optional custom rules.
- Returns a localized reason string instead of a boolean, so the UI can explain invalid placement.

`Assets/_Duskborn/Resources/Building/`

- Contains five buildable assets: workbench, forge, cauldron, arcane table, and storage.
- Current costs:
  - Workbench: 10 wood, 5 stone.
  - Forge: 15 stone, 8 wood.
  - Cauldron: 4 iron bars, 8 stone.
  - Arcane table: 10 wood, 4 iron bars, 2 arcane crystals.
  - Material chest: 8 wood, 3 fiber.

### Player flow and controls

`Assets/_Duskborn/Gameplay/Building/BuildingController.cs`

- Added dynamically to the locally owned player from `PlayerInteractor.Building`.
- `B` opens/closes the building catalog.
- Selecting a buildable starts placement mode.
- A translucent preview follows the center-screen terrain raycast.
- Green/red preview communicates validity.
- Mouse wheel or `R` rotates by the buildable's authored rotation step.
- Left click confirms; `Esc` or `B` cancels.
- Action-bar scrolling, attacks, skills, interaction, and Alt-wheel camera zoom are blocked while placing.
- Placement errors returned by the server remain visible briefly.
- `F` uses the existing interaction architecture to open placed stations.

Recent placement fixes:

- HUD raycasts no longer block a locked-cursor placement click.
- `PlayerCameraController` no longer marks every locked-cursor click as a focus-restoring click.
- Opening the building catalog or an interacted station now unlocks the cursor through `PlayerCameraController.SetCursorLocked(false)`, keeping the camera's internal cursor state synchronized so its next update does not hide the pointer again.
- Mouse wheel rotates the preview while action-bar scrolling is suppressed.
- Invalid/duplicated station prefab GUIDs were replaced. All five buildable definitions now resolve their referenced prefab and root object.
- `Begin` rejects missing prefab references before entering placement, and server errors now distinguish a missing catalog definition from a missing prefab.

These fixes compile, but the latest asset/GUID repair still needs a fresh Unity reimport and Play Mode confirmation.

2026-09-20 continuation note: the previous invisible Unity process was found stale after its disconnected-session shutdown and was restarted. A clean reimport/domain reload completed without C# errors, the Unity menu suite passed 35 checks, and the catalog was rendered at 1920×1080 for visual QA. The Editor window was not exposed to computer-use on this host, so the interactive Play Mode placement path remains unverified.

### Server authority and transactions

`Assets/_Duskborn/Gameplay/Player/PlayerInteractor.Building.cs`

- Extends the existing FishNet player component with building RPCs.
- A newly initialized local player starts with 50 stone, 50 wood, and 50 iron ore. The grant establishes an idempotent inventory baseline without incrementing `ResourceInventory.Revision`, so it does not block a valid fresh-session infrastructure checkpoint load.
- Server validates the command, asks the owning client to pay through the existing client-authoritative material wallet, then revalidates and commits.
- Rejected commits refund paid costs.
- Full building snapshots are sent after mutations and periodically for joining/synchronizing clients.

`Assets/_Duskborn/Gameplay/Building/BuildingWorld.cs`

- Server-owned registry of placed buildings.
- Creates real world objects from the same visuals used by previews.
- Supports place, move, dismantle, queue, collect, and deposit commands.
- Dismantling refunds 100% but is blocked while the station contains materials or queued work.
- Movement preserves contents and queued work.

The existing `ResourceInventory` is client-authoritative throughout the project. The building implementation follows that convention; it is not hardened against a modified client.

### Crafting and processing integration

`Assets/_Duskborn/Gameplay/Crafting/CraftingRecipe.cs`

- Recipes have `processingSeconds`.
- Ingredient spending uses the shared atomic `MaterialCosts` path.

`Assets/_Duskborn/UI/CraftingUIManager.cs`

- A station now exposes only recipes matching its actual `CraftingStationType`.
- Recipes are checked for discovery and correct station proximity.
- Ordinary crafted materials enter `ResourceInventory` instead of consuming an inventory equipment slot.
- Processing recipes are sent to a placed station queue instead of completing instantly.

Current asynchronous recipes are set to 12 seconds:

- Iron bar smelting.
- Steel plate forging.
- Leather tanning.
- Crystal powder grinding.

`Assets/_Duskborn/Gameplay/Building/PlacedBuilding.cs`

- Stores queued jobs, remaining time, material contents, upgrade level placeholder, and identity/transform state.
- Processing continues with the UI closed and while players are distant.
- A full output buffer pauses work without consuming additional progress.
- No offline elapsed-time simulation is implemented.

### Persistence

- Solo-host manual checkpoint from the building catalog.
- JSON is keyed by scene and terrain seed under `Application.persistentDataPath`.
- Persists buildings, transforms, queues/progress, contents, material wallet, and crafting discovery.
- Loading is intentionally restricted to the start of a fresh solo session before material activity to prevent partial-state duplication.
- This is infrastructure persistence, not a whole-game save system.

### Tests and documentation

- `Tools/BuildingTests/Run.ps1`: engine-free contract runner using the real inventory/cost/recipe/processing source. It currently passes 23 checks, including atomic forge input/fuel consumption, fuel affordability, fuel starvation, and output-full behavior.
- `Assets/_Duskborn/Editor/BuildingTests.cs`: Unity Editor tests for placement physics, buildable assets, inert previews, and serialization.
- `building_system.md`: implementation behavior, controls, architecture, persistence limitations, and manual QA notes.

### UI continuation (2026-09-20)

`Assets/_Duskborn/UI/Building/`

- Introduces ordinary C# `BuildablePresentation` and `CostPresentation` models. Affordability, shortages, unlock state, prefab availability, and single-copy restrictions no longer have to be inferred from button text.
- Adds a reusable `BuildingUIManager` shell with separate catalog, placement HUD, station, confirmation, and toast views. Gameplay/network commands remain in `BuildingController`.
- The catalog now has selectable building cards, icon placeholders, category/role copy, owned/required costs, exact shortages, a persistent details selection, disabled placement states, and a smaller checkpoint section.
- Catalog state refreshes from `ResourceInventory.ResourceChanged` and `RecipeDiscoveryTracker.OnRecipeDiscovered`.
- Placement now uses a compact non-raycastable bottom HUD with building identity, valid/invalid/pending state, exact reason, horizontal costs, rotation, controls, and post-transaction toasts.
- Station actions use the shared shell, disabled states, and an explicit dismantle confirmation with the full refund cost.
- Processing stations now show a selected recipe, input/output/duration details, queue eligibility, current-job progress and remaining time, ordered waiting jobs, output capacity/contents, collection, and a clear output-full paused state.
- Material chests now expose two-way per-material rows with player/chest counts plus deposit/withdraw-one and deposit/withdraw-stack actions. The new `withdraw` command validates quantity again on the server and removes stacks atomically before crediting the player.
- Runtime and Editor compilation, all 23 engine-free building contract checks, and 41 Unity asset/physics/presentation/UI/storage/starter-wallet checks pass after this milestone.
- `Artifacts/BuildingUI/` contains deterministic 1920×1080 Unity-rendered catalog, processing, and storage captures. They were inspected and used to correct card width, cost-chip readability, long-label wrapping, progress hierarchy, and transfer-row layout.
- Manual Play Mode placement and multiplayer verification are still required because this host did not expose the running Editor window to computer-use.

---

## 3. Current UI state

The building UI now uses reusable runtime uGUI views under `Assets/_Duskborn/UI/Building/`. `BuildingController` owns player intent and commands; the view layer renders explicit presentation state and raises callbacks.

Current screens:

- Building catalog: selectable cards plus a details pane, icon placeholders, descriptions/capabilities, owned/required costs, shortage and lock messaging, disabled place action, and compact checkpoint controls.
- Placement HUD: compact bottom-center panel showing identity, validity plus reason, pending state, costs, rotation, controls, and outcome toast. Its graphics do not receive raycasts.
- Station panel: dedicated processing progress, queue, output-capacity, selected-recipe and action sections, plus collect/move controls and confirmed dismantling.
- Storage panel: two-way material rows with player/chest counts, capacity progress, and exact one/stack transfer actions.
- Equipment crafting: reuses the existing `CraftingUIManager`.
- Catalog and station panels explicitly release the gameplay cursor through the camera controller; closing them returns cursor ownership to the existing camera menu-state loop.

Remaining UI weaknesses:

- Buildable icons are displayed when authored, but the assets still have no populated icons and therefore use initials placeholders.
- Forge fuel is now a separate recipe field and persistent station slot. Right-clicking compatible inventory stacks fills input/fuel up to 64 units, processing starts automatically when one complete batch is present, and clicking either slot returns unconsumed contents.
- Queue entries are read-only and cannot be canceled or reordered, matching backend rules.
- Storage does not yet provide a typed arbitrary quantity or search because the current material set is small; one and full-stack transfers are supported.
- The views are reusable but still built at runtime; an authored prefab may be preferable once the layout settles.
- Runtime UI text is partly Portuguese while this handout is in English. Match the language already used by the surrounding game UI; do not mix languages within one screen.

---

## 4. Immediate first task: verify the construction pipeline

Before redesigning UI, confirm the latest placement fix in a clean Editor state:

1. Exit Play Mode.
2. Allow Unity to reimport the repaired `.meta`, prefab, and building asset references.
3. Confirm each asset in `Resources/Building` shows a non-null prefab.
4. Enter host/single-player Play Mode.
5. Give the player the required materials using the existing debug/test path.
6. Open `B`, choose the workbench, aim at flat ground, and place it.
7. Confirm materials are deducted once, the preview disappears, and a real station remains.
8. Repeat with forge, cauldron, arcane table, and storage.
9. Interact using `F`, move each station, and dismantle an empty one.

If “construction unavailable” still appears, inspect both sides of the command:

- Client: `BuildingController.Send` and `PlayerInteractor.RequestBuilding`.
- Server: `PlayerInteractor.RequestBuildingRpc` → `BuildingWorld.Validate`.
- Payment: `PayBuildingRpc` → `CompleteBuildingRpc`.
- Commit: `BuildingWorld.Commit` → `BuildingWorld.Create`.

Keep explicit failure reasons visible in the placement HUD and add temporary structured logs around this pipeline if needed. Remove noisy logs after the failure is resolved.

The Unity MCP connection was revoked during the previous session, so Play Mode and Editor console verification could not be performed through tooling. Local C# runtime and Editor compilation both passed.

---

## 5. UI-first implementation backlog

Work in this order. Keep each stage usable before moving to the next.

### Phase A — Establish a reusable UI shell

Replace the ad-hoc `BuildUI` hierarchy with either an authored prefab or reusable view components under `Assets/_Duskborn/UI/Building/`.

Recommended view split:

```text
BuildingUIManager
├── BuildingCatalogView
│   ├── CategoryTabs
│   ├── BuildableCardView[]
│   └── BuildableDetailsView
├── PlacementHUDView
├── StationView
│   ├── CraftingStationView
│   ├── ProcessingStationView
│   └── StorageStationView
└── ConfirmationDialogView
```

Keep gameplay/network commands in `BuildingController` and `PlayerInteractor.Building`. Views should render state and raise intent events. Do not move server validation or spending into UI classes.

Reuse the colors, fonts, frames, button behavior, audio feedback, scale conventions, and draggable-panel patterns from `CraftingUIManager`, `InventoryUIManager`, and `CharacterUIManager`. Prefer shared helpers/components where practical instead of cloning another large self-building manager.

### Phase B — Building catalog

Each buildable card should show:

- Icon or a safe placeholder.
- Display name and short role.
- Category/station type.
- Compact cost chips: material icon/name, owned count, required count.
- Affordable state.
- Missing-material state with exact shortage.
- Locked state and understandable unlock reason.
- Multiple-copy restriction when applicable.

The details panel should show description, footprint/placement notes only when relevant to the player, functional capability, costs, and a prominent “Place” action. Avoid exposing implementation fields such as collision masks or raw IDs.

Catalog behavior:

- Preserve selection while material counts refresh.
- Disable the place action when locked, unaffordable, or missing a prefab.
- Refresh immediately on `ResourceInventory.ResourceChanged` and recipe discovery events.
- Add category filtering only if it improves scanning with the current five entries; keep the structure ready for more entries.
- Move save/load out of the main catalog flow, preferably to a small infrastructure/options section.

### Phase C — Placement HUD

Replace the current multiline text with a compact bottom-center HUD:

- Buildable name and small icon.
- Valid/invalid state with color plus text/icon; never rely on color alone.
- Exact invalid-placement reason.
- Horizontal material-cost summary.
- Controls: confirm, rotate, cancel.
- Rotation feedback, such as current degrees or a brief rotate animation.
- A short “Confirming…” state while the RPC transaction is pending.
- Success and failure toast that remains readable after the preview closes.

Do not let the HUD intercept placement clicks while the cursor is locked. The recent bug came from UI/cursor focus logic swallowing confirmation input.

### Phase D — Processing station UI

Create a dedicated processing layout instead of representing everything as generic buttons:

- Recipe list with discovery/affordability state.
- Selected recipe inputs, optional fuel, processing duration, and output.
- “Queue” button with clear missing-material feedback.
- Current job with progress bar and remaining time.
- Queued jobs listed in order.
- Output buffer with capacity display and collect action.
- Clear “output full; processing paused” state.
- Processing speed and queue capacity when they differ from defaults.

Keep queue cancellation unavailable until a refund rule is explicitly designed. Do not imply that a queued job can be canceled if the backend does not support it.

The forge is the best first vertical slice because iron smelting already represents input + fuel + time + output.

### Phase E — Storage UI

Replace fixed “deposit 10” buttons with a two-pane material transfer interface:

- Player materials on the left.
- Chest materials on the right.
- Capacity bar and `stored / capacity` count.
- Deposit/withdraw one, stack, or chosen quantity.
- Disabled states when empty/full.
- Search/filter only when the number of materials justifies it.

Backend commands currently support deposit and collect-all. Add precise withdrawal/deposit commands atomically before exposing finer UI controls. Validate quantities and capacity again on the server.

### Phase F — Relocation and dismantling UX

- “Move” should close the station panel and enter placement mode while preserving contents/jobs.
- Clearly label relocation as free.
- Dismantle should show the refund contents and require confirmation.
- If blocked, state exactly what must be removed or completed.
- Disable dismantle while a transaction is pending.
- Keep 100% refunds unless balance direction changes.

---

## 6. UI state model to introduce

Avoid having view code infer state from button text. Give the UI explicit, testable presentation state. A useful starting point:

```csharp
public sealed class BuildablePresentation
{
    public BuildableDefinition Definition;
    public bool IsUnlocked;
    public bool IsAffordable;
    public bool IsAvailable;
    public IReadOnlyList<CostPresentation> Costs;
    public string DisabledReason;
}

public sealed class CostPresentation
{
    public string MaterialId;
    public string DisplayName;
    public Texture2D Icon;
    public int Owned;
    public int Required;
    public int Missing;
}
```

Do the same for processing jobs and storage rows. Keep these as ordinary C# presentation models so state formatting can be tested without rendering a Canvas.

---

## 7. Important technical guardrails

- Do not create a second inventory, crafting, interaction, input, or networking system.
- Use `ResourceInventory`, `RecipeDiscoveryTracker`, `CraftingRecipe`, `CraftingStationType`, `Workbench`, `PlayerInteractor`, and `HotkeyManager`.
- The server remains authoritative over placement geometry, building state, queues, contents, and final commit.
- Resources are currently client-authoritative by existing project design. Preserve the transaction/refund handshake unless intentionally migrating the whole resource system.
- Client-side green placement is advisory; server validation is final. Show the returned server reason.
- Do not consume resources when entering placement or canceling.
- Never allow a failed or duplicate RPC to spend twice.
- Keep preview objects inert: mesh/render data only, no copied gameplay scripts, colliders, particle systems, or network identities.
- Avoid modifying `SampleScene.unity` unless UI integration truly requires it. It already contains unrelated user changes.
- The working tree is heavily modified. Preserve unrelated scene, prefab, and world-generation edits.
- Several station prefabs exist in both `Resources/Stations` and `Prefabs/World`. Their `.meta` GUIDs were repaired to be unique. Do not reintroduce duplicate or non-hex GUIDs.
- If building definitions are regenerated, preserve `processingSeconds` and all prefab references.
- Do not expose save/load as if it were a complete game-save feature.

---

## 8. Verification expected after each UI phase

Compile checks:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetCoreRuntime/dotnet.exe' `
  'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll' `
  '@Temp/BuildingValidation/runtime.rsp'

& 'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/NetCoreRuntime/dotnet.exe' `
  'C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll' `
  '@Temp/BuildingValidation/editor.rsp'

./Tools/BuildingTests/Run.ps1
```

Unity checks:

- Run `Duskborn → Tests → Run Building Physics and Asset Tests`.
- Test at 16:9 and a narrower aspect ratio.
- Test controller/keyboard focus only if the project currently supports it; keyboard and mouse are the established baseline.
- Verify menus correctly lock/unlock cursor and camera.
- Verify the placement HUD never catches the world-confirm click.
- Verify action-bar selection cannot change during placement.
- Verify costs update live after gathering, building, dismantling, depositing, and collecting.
- Verify a joining client sees placed objects and station progress.
- Verify two clients cannot collect the same output or place overlapping stations.
- Verify UI closes or disables actions when the player walks out of interaction range.
- Verify queued processing continues with the station UI closed.

For every UI milestone, include at least one screenshot or short capture at the target game resolution and inspect clipping, hierarchy, contrast, disabled states, and long localized strings.

---

## 9. Known limitations and future work

- Current station assets reuse very similar or identical workbench geometry; UI must carry much of the role distinction until better art exists.
- Buildable icons are not populated yet.
- Upgrade level is persisted but there is no upgrade command, balance, or UI.
- No move/dismantle targeting mode exists outside interacting with a station.
- No processing job cancellation or per-job reorder.
- No typed arbitrary-quantity storage field; exact one/full-stack server commands are implemented.
- The Phase A-C view split is code-authored rather than prefab-authored; keep it unless an authored prefab demonstrably improves iteration.
- No automatic load, offline processing, or whole-game save coordinator.
- No stable multiplayer player identity for persistent ownership/permissions.
- No indoor/outdoor/build-zone validators are configured.
- Full anti-cheat requires migrating material ownership to the server, beyond a UI pass.
- Interactive Play Mode confirmation is still required for the forge cursor transition; batch-mode compilation and the surrounding UI checks pass, but batch mode cannot validate a visible hardware pointer.

Do not solve these opportunistically during the first UI iteration. Record them, keep the UI extensible, and finish the core catalog → placement → station loop first.

---

## 10. Definition of done for the UI-focused continuation

The next UI pass is complete when:

Current status: the Phase A-F core UI implementation is present in code. Catalog, processing, and storage views have deterministic 1920×1080 render captures; compilation, 16 contract checks, and 41 Unity checks pass. Catalog/station cursor release and the 50 stone/wood/iron-ore starter baseline are implemented. Manual Play Mode cursor/placement, host/client agreement, and live interaction-range/input verification remain pending, so the overall continuation is not yet complete.

- All five buildables can be selected, placed, moved, interacted with, and dismantled through clear interfaces.
- The catalog shows icons/placeholders, descriptions, owned/required costs, shortages, lock state, and availability.
- Placement shows readable validity, failure reasons, material costs, controls, pending state, and outcome feedback.
- The forge provides a complete processing vertical slice with recipe selection, queueing, live progress, output capacity, and collection.
- Storage supports understandable two-way material transfer with capacity feedback.
- The existing crafting UI still works for equipment and respects station requirements.
- UI and gameplay input never fire from the same click/scroll event.
- Host and one client agree on building, queue, storage, and collection state.
- Runtime compilation, Editor compilation, contract tests, Unity building tests, and the manual Play Mode path pass.

When reporting work, update this handout's “implemented,” “current UI state,” “known limitations,” and “definition of done” sections so it remains the source of continuity for the following agent.
