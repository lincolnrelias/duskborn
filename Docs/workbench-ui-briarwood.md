# Workbench crafting — Briarwood & Iron

The production `CraftingUIManager` now uses the existing Briarwood frame, iron corners,
hammer crest, trimmed brass button sprite, leather colors and licensed Alegreya SC
headings. Recipe/category navigation, station filtering, discovery, inventory pairing,
dragging, keyboard shortcuts and gameplay validation retain their existing flow.
The panel is taller to make room for material requirements and a fixed action footer.

Requirements use material names and a Have / Need column. The action names the first
actual material deficit, discovery requirement or inventory blocker. Unaffordable and
undiscovered recipes remain selectable. Empty categories clear old details and costs.
Confirmed immediate crafting retains the updated blocker beneath its success message.
Recipe and ingredient scrolling is clamped without inertia; immediate crafting guards
against reentrant submissions during resource callbacks.

## Reproduce validation

Follow `.cursor/rules/unity-cli-workflow.mdc` and `Docs/unity-cli.md`.
Use an isolated project copy when the live checkout is already open:

```powershell
.\Tools\unity.ps1 capture-crafting-ui
.\Tools\unity.ps1 test
```

The capture renders the actual production hierarchy at 1280×720 and 1920×1080 using
the Stone Axe recipe, a real resource inventory and an empty inventory service.
`Artifacts/BriarwoodUI/crafting-{missing,ready,empty}-{1280,1920}.png` covers material
deficits, readiness, and an empty category. The station panel is centered for these
captures; the companion inventory is not rendered. No new raster art was generated.

`CraftingTests` checks deficit/action feedback, updated affordability, retained blockers,
empty category cleanup, and repeated large wheel input at both list boundaries.
The existing resolution suite checks the taller panel's screen margins.

Manual checks still required: open a Workbench with the inventory at 720p and 1080p;
drag the paired panels, change categories, scroll rapidly in both directions, inspect an
unavailable recipe, craft until resources run out, and verify Escape/close restores the
inventory and cursor. Check hover/focus and contrast over bright and dark terrain.
Static captures and edit-mode tests do not prove those live interactions.
