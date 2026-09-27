# Moonwell Shrine

Editable Blender model based on the selected Moonwell Shrine concept. Built through the official Blender MCP connection in Blender 5.1.1.

## Files

- `Moonwell_Shrine.blend`: updated scene, open in Blender at frame 57.
- `Moonwell_PaintedAtlas.png`: shared 1024 x 1024 painted-style atlas. Keep beside the blend file; it uses a relative texture path.
- `Moonwell_Preview.png`, `Moonwell_Front_Open.png`, `Moonwell_Rear.png`: inspected renders.
- `Moonwell_Motion_Smooth.mp4`: updated four-second animation preview at 30 fps.
- `Moonwell_Motion_Smooth.gif`: animated preview of the updated motion.
- `Moonwell_Motion.gif`: original five-fps preview, retained for comparison.
- `Moonwell_Shrine_before_animation_smoothing.blend`: original model backup.
- `smooth_animation.py`: continuous page-turn and sinusoidal hover update, also called by the build script.
- `build_moonwell.py`: additive construction script. Run only in a scene without the `Moonwell_Shrine` collection.
- `validation.json`: geometry and animation check results.

## Model

Approximately 1.67 m wide/deep and 2.35 m tall; tabletop approximately 1.14 m above floor. Front faces Blender -Y. `Moonwell_ROOT` stays at the floor-center origin. The asset has 3,126 evaluated triangles, 76 editable mesh objects, two materials, and one texture atlas. Bevel modifiers remain editable. The many separate objects preserve modeling flexibility; consolidate static pieces during game export if appropriate.

`Moonwell_Shrine` contains the asset. `Moonwell_Preview_Studio` contains only presentation ground, camera and lights. Exclude the studio when exporting.

## Animation

Timeline frames 1-120 at 30 fps form a four-second loop, with the closing key at frame 121. Play the timeline to see the book hover and its page turn. The book's cover and page blocks are separate from `Spellbook_Turning_Page`; relative shape keys drive the flexible leaf. The updated motion uses 65 sampled poses along one continuous angular curve, with linear weight crossfades so intermediate poses do not introduce stops. The page eases only during lift and settling. At frames 113-121 the leaf briefly folds into the spine below the printed surfaces before restarting. Book hover follows a sampled sinusoid. No external animation dependencies or drivers are required.

The updated animation passed 241 half-frame collision samples against the crescent and crystals, with no collisions and an exact loop seam. No stationary steps occurred during the visible turn. See `animation_smoothing_validation.json` for the results. Engine playback remains unverified.

## Validation and remaining integration

Inspected front, three-quarter and rear renders and sampled animation poses. All asset meshes have UVs and unit scales; no negative-volume solids or zero-area base faces were found. Three deliberately open printed/decal surfaces are named in `validation.json`. No missing external files were reported after saving. The turning page clears the crescent and crystals at all 121 integer frame samples; world-space vertices match exactly at the loop seam. These checks do not prove every possible collision at arbitrary subframes.

Unity was not opened or controlled. The export is now installed at `Assets/_Duskborn/Art/Models/MoonwellShrine`, with a portable copy in `MoonwellShrine_Unity.zip`. Choose **Duskborn > Art > Moonwell Shrine > Create or Rebuild Prefab** in Unity to generate the URP materials, controller and prefab. No existing game prefabs, scenes or settings were changed. Verify import scale, atlas and crystal emission, gameplay-camera readability, and full page motion beside the existing forge and furnace.

The export uses four meshes while the source remains fully editable. Its page curves and hover were compared across all 121 frames after FBX reimport, and the setup script compiled offline against Unity 6000.4.1f1. See `unity_export_validation.json` and `UnityExport/MoonwellShrine/README.md` for details. The live unsaved Blender scene was preserved; the saved smooth-animation source was used as requested.
