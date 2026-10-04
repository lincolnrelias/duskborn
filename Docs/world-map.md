# Map and minimap

The north-up minimap stays in the top-right corner, inside an engraved brass compass
frame with a region plaque, clock and coordinates. It follows the local player and
starts at a 55 metre radius. The +/- buttons and mouse wheel adjust its range between
20 and 160 metres. Use Alt or Escape to release the cursor for minimap controls. Zoom buttons remain active while pause is open; the map button transitions from pause to the world chart.

M (the remappable Map action) or the minimap's M button opens a separate parchment
world-map window. The minimap remains visible. Opening the chart releases the cursor
and reserves camera input. M or X closes it. Escape opens pause and dismisses the chart.

- Drag the chart to pan; wheel or +/- buttons zoom independently from the minimap.
- Reset restores the full world extent.
- Players and Structures buttons toggle those marker categories.
- Hover a marker for its player identifier or building name.
- Shift-click either map to place a personal waypoint; right-click clears it.
- Ivory arrow: your position and facing. Teal dots: teammates. Grey: downed players.
- Distant teammates appear as directional arrows along the minimap rim.
- Gold diamonds: placed structures, including restored and replicated buildings.

The seeded terrain is sampled into 1024px terrain and parchment atlases over multiple
frames, with shoreline outlines, height contours and relief shading. The map uses the
same chunk extents, seed and water height as the world. Markers refresh at 20 Hz from
PlayerRegistry and BuildingWorld, without scene searches or extra network messages.
The HUD persists across scenes, hides when no local player or terrain manager exists,
and initializes from both runtime startup and local-player/HUD registration paths.
Natural props and enemies are not tracked. Waypoints are local to this client.

Validation: WorldMapTests checks projection, chunk extents, circular/rectangular meshes,
canvas isolation, height-based scaling, input setup, button callbacks, paused zoom, click deduplication, panning, reset and pause transitions.
Tools/unity.ps1 capture-map renders the production canvases in a hidden edit-mode process
with real seeded terrain and player/building fixtures. Output: Artifacts/WorldMap.

Manual checks still required:

1. Join a world; move, turn, press M, pan/zoom the chart, close it and verify aiming resumes.
2. Release the cursor with Alt; click minimap +/- and M. Minimap wheel should not zoom the camera.
3. Use two clients; move apart, disconnect, place/remove structures and save/load buildings.
4. Check hover names, marker filters and Shift-click waypoints.
5. Return to the menu and join another seed; verify the HUD hides and terrain refreshes.

The map owns its 1920x1080 canvas and uses screen-height scaling, matching GameHUD
and the pause menu. Crafting and character panels exclude it when selecting their
shared inventory canvas. Paused minimap controls also accept native IMGUI pointer
events through the pause interface, with a per-button frame guard against duplicate
UI/input events.

Pause now initializes when launching through MainMenu and is also ensured when a
local player registers. Camera focus recovery tests the map's current screen bounds
before EventSystem processing, so clicking M/+/- cannot consume the pointer first.
WorldMapTests covers inactive pause recovery and camera cursor ownership over the
actual minimap button bounds. Runtime input still requires a manual check: start
through MainMenu, join a world, press Escape, click +/- and M, then Escape again.

