# Duskborn UI art direction

2026-10-04. Visual exploration. Briarwood & Iron is now implemented for the building catalog; see `building-ui-briarwood.md` for assets and validation.

## Scope

Keep the existing crafting and building menu structure and interactions. Improve their visual identity through frame art, surfaces, typography, icon treatment, ornament, and button states. The earlier layout prototypes do not define the requested change.

## Grounding

The current frame_classic.png already uses chunky timber, faceted iron corner brackets, and a brass center fitting. Ironroot uses simplified low-poly shapes and desaturated cloth/leather. The Moonwell Shrine combines angular pale stone, olive fabric, golden moon/rune marks, and violet crystals. These are the starting vocabulary, rather than generic black rectangles or elaborate gothic filigree.

Reference files: Assets/_Duskborn/Resources/Textures/frame_classic.png; Artifacts/BuildingUI/building-ui-catalog.png; Artifacts/Ironroot/v002/preview.png; Artifacts/MoonwellShrine/Unity_Export_Roundtrip.png.

## Briarwood & Iron

Recommended initial direction. Evolve the existing wood frame into an authored family: chunky bevels, uneven hand-painted grain, cold iron corners, small bronze fasteners, and restrained thorn-shaped carvings at the header and outer corners. Keep the ornament silhouette broad enough to read at game scale.

Panel bodies use quiet charcoal-brown leather or stained board. Text is warm ivory. Selection uses a brass inset edge; buttons resemble substantial carved plates with a clear pressed bevel. Use full-color painted item art, consistently padded inside dark recessed sockets. No initials as final building art.

Headings use a sturdy readable fantasy serif; body labels and numbers use a clear companion face. Fantasy styling belongs primarily in headings, frame silhouettes, and material treatment. Body text must stay readable.

For stations, swap a small crest/inset detail rather than the whole design: hammer for Workbench, ember/iron for Forge, herb/bronze for Cauldron, moon/violet crystal for Arcane Table, latch for Material Chest. Avoid permanent glow on every border.

## Moonstone & Parchment

Alternative if a more mystical identity is preferred. Faceted stone corners, leather-bound dark frames, aged muted parchment detail surfaces, moon seals, and sparse rune marks. Use olive, bone, dark slate, aged gold, and a small violet crystal accent. Keep parchment flat and low-noise under text. Avoid ornate scrollwork, glowing neon glyphs, and thin fragile borders.

Use the same layout and information hierarchy as Briarwood & Iron so feedback compares visual treatments directly. This direction borrows from the existing Moonwell Shrine; it is an exploration, not an assertion that the whole game should adopt an arcane identity.

## Shared visual rules

- Decoration stays on the perimeter, headers, dividers, and control edges. Text regions stay quiet.
- Design one outer frame and lighter inner sockets; do not repeat the heaviest wooden frame around every row.
- Maintain authored item/building icons, coherent perspective, painted shading, and consistent icon scale. No stock outline icons in the finished game skin.
- Keep selection, readiness, and missing resources distinct. Selection gets the strongest brass accent; deficits pair a restrained rust mark with readable text.
- Use material depth through broad painted bevels, not noisy photoreal textures or repeated bright outlines.
- Supply normal, hover/focus, pressed, selected, disabled, and pending states from the same material family.
- Panels remain opaque enough for text over bright terrain. Avoid pure-black interiors that swallow frame detail.

## Production handoff after direction selection

Create reusable nine-slice outer panels, header plates, recessed item sockets, buttons, selection strips, and a compact divider/crest set. Separate scalable edges from non-stretching corners and crests. Use transparent PNGs with safe padding and consistent authored scale; validate slicing and atlases in Unity before replacement. Choose a font with the needed localization glyphs and verified licensing. Replace the skin incrementally while preserving the existing menu logic.

Image mockups are art-direction previews, not implementation-ready assets or verified gameplay screenshots. Final in-game checks: legibility at 720p/1080p and UI scaling, edge slicing, long labels, icon recognition, selection contrast, and bright/dark terrain backgrounds.
