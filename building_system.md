# Functional building and processing

Press **B** to choose a station, aim at terrain, **R** to rotate, left click to place, and **Esc/B** to cancel. **F** uses the existing nearby-object interaction to open a placed station. Build and rotation keys are registered in `HotkeyManager` (R still performs skill 3 outside building mode).

The catalog contains the existing workbench, forge, cauldron and arcane table models, plus the existing chest model for material storage. The station art currently shares the project's workbench model; preview and placed geometry match those assets. Construction is unlocked by affordable materials; optional recipe-discovery requirements are supported per definition. Specializations stay useful instead of replacing earlier stations with numbered tiers.

- Workbench: 10 wood + 5 stone.
- Forge: 15 stone + 8 wood.
- Cauldron: 4 iron bars + 8 stone.
- Arcane table: 10 wood + 4 iron bars + 2 arcane crystals.
- Material chest: 8 wood + 3 fiber; 200 units.

All costs use the existing material IDs and ResourceInventory. Stations can be moved with their contents for free. Dismantling returns 100% of construction costs, but requires an empty output/storage buffer and no queued work. An individual queued job cannot be canceled, so fuel/inputs cannot be recovered twice.

Iron smelting, steel plates, leather tanning and crystal grinding use the existing recipes with 12-second processing. The forge has persistent 64-unit input and fuel slots: right-clicking a compatible inventory stack loads it, one input/fuel batch is consumed atomically when processing begins, and clicking a slot returns anything not yet consumed. Its result slot uses the existing 200-unit output buffer and pauses processing when full. Other processing stations retain the queue workflow. Processing continues on the server regardless of UI or player distance, and pauses outside the running session. Outputs are collected into the material wallet so they can feed further construction/crafting. All stations enforce their recipe capability; the ordinary workbench does not bypass forge/arcane/alchemy requirements.

## Architecture

`BuildableDefinition` assets in `Resources/Building` hold identity, costs, optional unlock recipe, footprint, clearance, slope/contact tolerance, reach, rotation, prefab/icon, station capability, storage/queue capacity and movement/dismantling permissions. `PlacementRule` ScriptableObjects provide additional validators. No indoor/outdoor or architectural restrictions are imposed in this first version.

`PlacementValidator` tests the support at the center and four footprint corners, ground angle, distance and rotated collision volume. Ground and blocking masks are configurable. Preview creation copies only mesh/render components, never prefab behaviours or network identities. Preview and real objects share the same visual factory and authored footprint.

`PlayerInteractor.Building` adds FishNet RPCs to the existing player component, requiring no prefab or scene changes. World state and final placement validation belong to the server; clients receive a full snapshot once per second and after edits, including on joining. Resource ownership remains client-authoritative, matching the existing pickup/crafting system. Paid operations use validation, wallet payment, then revalidation/commit, refunding rejected commits. This does not harden the pre-existing client inventory against modified clients. `BuildingController` is attached only to the owned player and uses uGUI with the existing camera/menu/combat input guards.

## Infrastructure checkpoint

Use **B → Save infrastructure** on a solo host. The JSON checkpoint is stored under Unity's `Application.persistentDataPath`, keyed by scene and terrain seed. It includes placement IDs, transforms, upgrade-level state, queued recipe IDs and remaining time, stored outputs/materials, the host material wallet and recipe discovery. Writes use a temporary file and replace the previous file with a backup.

To restore, enter a fresh session using the same scene/terrain seed and choose **B → Load infrastructure before gathering/spending anything**. Loading after material activity, after construction, or more than once in the session is rejected. This avoids rolling back only one portion of a live economy. The project has no whole-game save or stable multiplayer player identity: equipment, dropped loot and the rest of the world are not checkpointed here, and multiplayer checkpoint loading is deliberately unavailable. There is no automatic load or offline time advancement. A future whole-game save coordinator should invoke the snapshot model alongside the rest of the player's state and replace this restricted checkpoint UI.

## Validation

`Tools/BuildingTests/Run.ps1` compiles the actual inventory/cost/recipe/processing source against small engine shims, then runs 23 deterministic contract checks. This covers atomic costs, repeated ingredients, invalid amounts, processing time, full outputs, slotted input/fuel consumption, fuel starvation, dismantling eligibility and resumed work. It does not execute Unity physics, rendering or RPC weaving.

`Duskborn → Tests → Run Building Physics and Asset Tests` in Unity covers support, reach, obstacles, invalid positions, assets, inert previews and JSON roundtrips. These checks and Play Mode verification require Editor access. Manual checks: place/cancel/rotate near obstacles and slopes; move a running forge; collect outputs and craft; reject dismantling nonempty stations; save/restart/load; join a second client and compete for the same output/placement location.
