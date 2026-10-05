# Forge UI — Briarwood & Iron

The forge uses a 700×580 reference panel scaled with its canvas, so the frame,
slots and text keep their proportions across screen sizes. Runtime sprites trim
transparent art padding and normalize sliced borders across import resolutions.
The model-rendered forge icon sits centered on the top frame. The flame is a
custom uGUI mesh with curved tongues, layered gradients, animated sway and idle embers;
heat scales its silhouette instead of clipping the tip.

The forge body, header, status recess and inventory picker now use the quiet leather
grain from the existing socket art instead of solid-color surfaces. The runtime surface
sprite crops the 1254 px source to `(132,145,990,962)` with normalized slice borders.
The progress meter uses a brass socket rim, an inset track and a moss/rust fill with
texture grain and painted bevel highlights. Its percentage and warning behavior remain.

Click MATERIAL or FUEL to open a clamped inventory grid with compatible items,
icons and owned counts. Square tiles wrap at four per row and scroll vertically.
Items without an icon use a letter fallback. Recipe locks, discovery, capacity and pending actions are
visible blockers. Selection rechecks current inventory before using the existing
authoritative load command. Right-click inventory loading, slot unloading and output
collection remain available. Other station skins are restored when leaving the forge.

`Tools/unity.ps1 capture-building-ui` now also captures the production forge UI
at 1280×720 and 1920×1080 in empty, processing, blocked, output-ready and both dropdown states.
Fixtures use actual forge recipe names, requirements, icons, and processing duration.
The captures omit the adjacent inventory and simulate station state.

Validation on 2026-10-04 used `Temp/BriarwoodUIProject` without controlling the
Editor or entering Play Mode. Final captures are under `Artifacts/ForgeUI`.
Compilation and project validation passed; all 24 test suites passed, with zero failures.
BuildingTests cover actual recipe material/fuel filtering, slot-role selection,
stale inventory counts, duplicate submission while pending, clamped dropdown scrolling,
loaded-state refresh and right-click collection. Final captures verify static composition.

The background and meter styling was compiled and captured on 2026-10-05 in
`Temp/ForgeSurfaceValidationProject` with a rebuilt asset cache. Static previews
at 720p and 1080p confirm leather grain, the inset brass meter and readable labels
in processing, blocked and inventory-picker states. The prior 24-suite result
above was not rerun for this visual-only change.

Manual checks remaining: open the forge with inventory over bright/dark terrain;
select material and fuel from their dropdowns, check ignition, return loaded stacks, collect output with either
mouse button, and verify status/resource refresh during multiplayer pending actions.
Check live hover, focus, cursor behavior, and switching to another station.
