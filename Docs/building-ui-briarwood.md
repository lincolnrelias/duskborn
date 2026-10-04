# Briarwood & Iron building catalog

Implemented 2026-10-04. Catalog-only visual skin; crafting, station processors,
placement controls, resource transactions, save/load and building models retain
their existing behavior.

## Installed assets

`Assets/_Duskborn/Resources/UI/Briarwood` contains the timber/iron outer frame,
brass/leather recessed socket, carved wooden button, and centered hammer crest.
The skin uses these reusable sliced sprites, warm ivory text, restrained brass
selection, and a larger selected-station preview. Headings use Alegreya SC Bold;
its unmodified font and SIL OFL license are bundled beside the sprites. Font
source: https://github.com/google/fonts/tree/main/ofl/alegreyasc.

The three transparent 512 px station icons are actual offscreen Unity renders of
the prefabs referenced by Build_workbench, Build_forge, and Build_arcane_table.
Their geometry and materials are preserved. The Forge is viewed from its hearth
side. Cauldron and Material Chest deliberately use initials inside the same
socket until their final visuals are requested. Gameplay costs come from the
existing definitions, including any additional Arcane Table requirements.

Generated skin art used built-in ImageGen. Exact prompts and original generator
output provenance are recorded in `Artifacts/BriarwoodUI/sprite-prompts.json`.
No text is baked into these images. The button PNG includes transparent padding;
the cached runtime sprite rect trims that padding without rewriting the source.
Do not replace the button source without updating its rect in
`BriarwoodCatalogTheme.ButtonSprite()`.

## Reproduce and verify

Use `Tools/unity.ps1 capture-building-icons` to import the skin settings and render
the prefab icons. Use `capture-building-ui` to capture the production catalog at
1920 x 1080 and 1280 x 720. Commands run a hidden graphics process in edit mode.
If the main project is open, run them in an isolated copied project and copy back
only the relevant images, import metadata, and captures. Do not copy scenes,
project settings, material changes, or FishNet generated catalogs from that copy.

Artifacts under `Artifacts/BriarwoodUI` show Workbench, Forge, Arcane Table, and a
placeholder selected at both resolutions. These prove static rendering; hover,
keyboard/cursor behavior, terrain contrast, live wallet refresh and multiplayer
placement still need a manual in-game check.

Validation: icon capture and final production UI captures completed successfully
in `Temp/BriarwoodUIProject`; all 24 existing CLI test suites passed with zero
failures. `validation.json` records installed asset/capture hashes and these results.

Manual check: open B, inspect all five entries and scroll to the chest, select each
station, verify missing materials disable placement, hover/click the controls,
then choose a location and cancel. Confirm the prior station/crafting flows still
look and behave as before.

Checkpoint controls now live in the Escape pause menu as Save Buildings and Load Buildings, with result/error feedback beneath them. The solo-host and fresh-session load restrictions remain in place. The building catalog uses clamped scrolling with inertia disabled. CLI tests exercise repeated large wheel inputs at both list boundaries. Earlier static captures show the previous checkpoint footer; the new pause-menu layout and live wheel/drag feel require a manual check.
