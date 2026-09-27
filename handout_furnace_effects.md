# Handoff: Fresco Furnace — Fire and Chimney Smoke

## Task for the next agent

Implement and visually polish two coordinated effects on the existing furnace: **fire inside the firebox** and **smoke rising from the chimney while the furnace is functioning**. Complete the Unity integration and verify it in gameplay. The user wants beautiful effects that belong to this particular hand-painted, low-poly model. A technically working pair of generic particle emitters is not sufficient.

Keep the furnace model, fresco artwork, silhouette, crafting rules, and placement behavior intact. Supporting ember glow and a restrained warm light belong to the fire effect; avoid expanding scope into sound, explosions, damage, or an unrelated VFX system.

## Verified starting point

- Workspace: `C:/Users/linco/Mugg`.
- Unity version recorded in `ProjectSettings/ProjectVersion.txt`: `6000.4.1f1`. Manifest lists URP `17.4.0` and the Particle System module. Inspect the active renderer and quality settings before choosing shaders.
- The model is already integrated in `Assets/_Duskborn/Art/Models/Forge/Fresco_Furnace.fbx`, with `Furnace_Fresco.png` and `MAT_Fresco_Furnace.mat`. Do not repeat the import or overwrite this artwork from the older source files.
- Both `Assets/_Duskborn/Prefabs/World/Station_Forge.prefab` and `Assets/_Duskborn/Resources/Stations/Station_Forge.prefab` reference the model. Verify which definitions and runtime paths use each.
- Editable source and original beauty preview: `Artifacts/FrescoFurnace/Fresco_Furnace.blend` and `Artifacts/FrescoFurnace/Furnace_Preview.png`. These have evolved since initial creation; inspect the current imported mesh rather than assuming original object names or counts survive export.
- Original authored scale was 1.54 m wide and 2.30 m tall, with a ground pivot, Blender Z-up and front -Y. Check Unity import orientation and actual bounds before placing effects.
- The source originally included static `Furnace_Flames`, `Furnace_Coals`, and Blender preview lights. Inspect for surviving static flames/emissive geometry. Replace or disable static flame visuals when adding animated fire; retain useful coals with controlled emission. Never allow the idle furnace to look permanently lit.

Read applicable project instructions first. Treat the original reference images as visual references. If changing the Blender asset, use the `blender-lowpoly-fantasy` skill and the official Blender connection. Prefer solving this task in Unity without remodeling.

## Art direction

This is an ancient, handmade kiln: muted sage/soot plaster, faded ochre and terracotta frescoes, chunky cool stone, and a warm arched mouth. Effects should feel painted, tactile, and slightly irregular. Preserve the sun motif, human figure, decorative neck bands, and open chimney as recognizable features.

The visual hierarchy is **firebox glow first, fresco silhouette second, quiet smoke third**. Fire should feel hot and alive without whitening the entire opening. Smoke should give the furnace life without covering the painting or becoming the largest opaque shape on screen. Judge this from the actual gameplay camera, not just a close-up Scene view.

### Effect 1 — contained, layered fire

- Create a small bed of deeper orange/red heat and a few overlapping tapered flame tongues, with amber bodies and small pale-gold hot centers. Use a deliberately painted flame texture/flipbook or a simple stylized animated mesh/material that holds up from oblique views. Avoid photographic fire, obvious rectangular cards, perfectly repeated triangles, and a uniform white additive blob.
- Use two or three visually distinct tongue sizes with independent timing. Let the flame silhouette stretch, curl gently, break, and regrow. Keep the base anchored to the coals; avoid a bouncing emitter or all flames breathing in lockstep.
- Fit the brightest region behind the lower half of the arch. Most tips should stay below its crown. Do not emit through the shell, stone jambs, or chimney wall. Start with a footprint around 0.25–0.40 m across and tongue heights around 0.20–0.40 m, then fit to the imported model.
- Use restrained emissive color so the orange-to-gold shape remains legible in daylight and at night. Bloom is a finishing accent, not a prerequisite for seeing fire. Do not globally raise exposure or bloom to make the effect work.
- Add a subtle, smoothly varying warm light inside the mouth if the renderer budget allows. It should illuminate the inner arch and immediate hearth, not shine through the opaque body or wash out the fresco. Use localized emissive/painted warmth instead if a real-time light leaks badly. Limit to one light per nearby active furnace and fade/cull at distance.
- Optional very sparse rising ember flecks can be a child of this fire effect only if they improve the composition. No mandatory spark shower or third hero effect.

### Effect 2 — soft painted chimney smoke

- Emit from the center of the actual top opening, slightly inside the rim. Begin narrow, then rise and gently widen into irregular overlapping wisps. A small amount of directional drift should imply warm air, with slow curl rather than frantic noise.
- Use a compact atlas of hand-painted, asymmetric smoke silhouettes with soft feathered edges and broad internal value variation. Avoid a stack of identical gray circles, hard sprite rectangles, bright white steam, dense black industrial exhaust, and high-frequency photorealistic turbulence.
- Color: warm soot gray near the mouth, fading into a cooler, desaturated gray as it disperses. Smoke uses translucent alpha/premultiplied blending appropriate to the selected shader, not additive glow. Keep opacity low enough to see the environment through overlapping puffs.
- Suggested starting envelope, to be tuned visually: emission radius 0.10–0.17 m; 4–8 particles/sec; 2–4 sec lifetime; upward speed 0.35–0.65 m/sec; size grows from roughly 0.15–0.25 m to 0.45–0.70 m. Aim for a readable plume about 0.8–1.5 m above the rim, fading before it fills the screen.
- Fade particles in quickly and out gradually. Randomize atlas frame, size, rotation, and lifetime modestly. Use smooth low-frequency lateral motion. Keep the initial trajectory above the chimney so the plume does not pour across the front mural.
- Prefer world-space smoke for natural detached drift. Clear old particles on teleport/reposition or pooling reuse to prevent ghost trails. Check transparency sorting from front, sides, and elevated gameplay angles. Use soft particles only if the current renderer supports the required depth setup; inspect any added rendering cost.

These numbers are art-tuning starting points, not verified settings or a substitute for visual iteration.

## Critical runtime integration details

Read these files before implementation:

- `Assets/_Duskborn/Gameplay/Building/PlacedBuilding.cs`
- `Assets/_Duskborn/Gameplay/Building/BuildingWorld.cs`
- `Assets/_Duskborn/Editor/ForgeModelGenerator.cs`
- Building definition, bounds, placement-preview, and snapshot code reached from those files.

**Prefab-only VFX will not reach placed buildings.** `BuildingWorld.Create()` calls `CreateVisual()`, whose `CopyVisual()` copies transforms, MeshFilters, and MeshRenderers only. Particles, scripts, and lights are not copied. This path also serves placement previews deliberately. Add a focused runtime VFX attachment/initialization step for actual placed forge instances after their `PlacedBuilding` is initialized. Keep preview copying inert. Use an explicit forge effect prefab/profile reference through an appropriate existing data path; avoid broadly copying arbitrary scripts and network objects. Ensure restored and late-joining instances take the same attachment path, exactly once.

`ForgeModelGenerator.UpdatePrefab()` recreates the `Fresco_Furnace` child and assigns the furnace material to model renderers. Put VFX where regeneration cannot silently delete it or replace its materials, or update the generator narrowly to preserve that separation. Check both station prefab variants. Avoid modifying the furnace's shared atlas material to animate individual instances; use per-renderer property overrides or a dedicated VFX material.

Do not let flame/smoke renderer bounds enlarge placement footprints, interaction outlines, collision bounds, or placement-validation volumes. This is especially relevant if using mesh-based flame tongues. Exclude VFX explicitly where needed.

## Define “functioning” from real processing state

`PlacedBuilding.Tick()` runs on the server through `BuildingWorld.Update()`. `BuildingWorld` broadcasts snapshots roughly once per second; clients apply them through `PlacedBuilding.Apply()` or create a new building. Integrate with this existing authority and snapshot model instead of sending particle events every frame.

The observed processing rules matter:

- A queued job can exist while processing is blocked by output capacity. `jobs.Count > 0` alone is not a reliable active flag.
- Fuel/inputs are consumed when a slotted burn starts. A job already in progress must keep burning even when stored fuel becomes zero. `fuelCharges > 0` or fuel inventory alone is also not an active flag.
- Valid recipe/output, positive effective processing speed, and available output capacity gate progress. Handle invalid or uninitialized state safely.

Derive a stable, side-effect-free processing predicate that matches the authoritative rules, or expose an explicit authoritative processing status if necessary. Do not consume fuel, advance jobs, or simulate crafting from the VFX controller. If introducing replicated status, handle save/load compatibility and late join. Prefer an idempotent local `SetOperating(bool)`-style API; snapshots repeating the same state must not restart particle systems.

Transitions:

- **Idle/new placement:** no flames, smoke, or active light; coals remain dark.
- **Startup:** fire ramps in over roughly 0.4–0.8 sec; smoke follows after a short 0.2–0.5 sec delay.
- **Continuous work:** sustain both effects across consecutive jobs without briefly extinguishing at every recipe completion or snapshot.
- **Stopped/blocked:** stop new flame and smoke emission smoothly; fade heat over about 1–2 sec and let existing smoke finish naturally. A brief cooling tail is intentional; persistent idle smoke is not.
- **Disable/remove/reuse:** stop and clear all systems and light state, unsubscribe callbacks, and leave no detached VFX objects behind.
- **Host and remote clients:** both see operating state; dedicated-server rendering is unnecessary. Do not add a NetworkObject to individual particles or the visual effect prefab.

## Implementation and polish workflow

1. Inspect the actual imported model, active materials, main gameplay camera, and current working tree. Preserve unrelated work. Record a baseline screenshot.
2. Add editable `FireAnchor` and `SmokeAnchor` transforms and measure their positions against the mesh. Do not paste Blender coordinates into Unity without conversion and inspection.
3. Build one reusable furnace VFX assembly, with fire and smoke separately tunable. Prefer Unity Particle Systems and simple URP-compatible materials already supported by this project. A new VFX Graph dependency is unnecessary unless a demonstrated need justifies it.
4. Author textures and curves intentionally. Store source textures, materials, prefabs, and scripts in the project's appropriate art/effects locations with generated `.meta` files. Expose intensity, transition durations, tint, and smoke density to avoid hardcoded tuning scattered across scripts.
5. Integrate with actual placed-building creation, state changes/snapshots, and cleanup. Provide an editor/debug preview of idle/start/run/stop for tuning without changing gameplay resources or networking authority.
6. Inspect animated captures, not only still images. Iterate at normal camera distance and in close-up, in daylight and dark lighting. Review flames without bloom as well as with the project's normal post-processing.
7. Tune for several furnaces visible together: stagger random seeds, share materials, bound particle counts, avoid per-frame allocations/material instantiation, and reduce distant light/particle work. Start around 20–40 live flame particles and 20–40 live smoke particles per close furnace if using particles, then measure; these are provisional budgets, not proven performance limits. Preserve current processing state across culling/resume.

## Completion checks and deliverables

- Demonstrate actual smelting with fire inside the arch and smoke emerging through the top, both fitting the model from multiple angles.
- Exercise empty idle, startup, sustained successive recipes, depleted stored fuel during a paid job, true fuel starvation, output-full blocking, resumed output capacity, and final cooldown.
- Verify save/load and late join into an already operating furnace, host plus remote observer, multiple independently operating furnaces, movement/removal, and placement preview with no live VFX.
- Confirm no permanently lit static flame duplicate, no flicker on snapshot refresh, no particles clipping through masonry, no opaque white fire blob, no smoke-card outlines, no mural-obscuring plume, and no VFX-driven placement-bound changes.
- Run compilation and focused checks for operating-state/lifecycle logic. Exercise the real runtime path; passing a prefab preview is insufficient. Report any unavailable multiplayer or performance validation honestly.
- Save before/after gameplay screenshots and short running/start-stop captures under `Artifacts/FurnaceEffects/`. Include a daylight gameplay view and a darker close-up. Inspect the captures yourself and revise until the artistic criteria are met.
- Deliver the configured VFX prefab/materials/textures, runtime integration, any narrow generator updates, and a short summary of changed paths, state semantics, visual validation, and measured performance or remaining limitations. Finish the implementation rather than returning another plan.

`handout_effects.md` describes an older weapon-hit effect proposal. It is context, not the furnace lifecycle specification: do not reuse its one-shot spawn/destroy or hit-RPC design for this continuous state-driven effect.
