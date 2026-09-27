# Moonwell Shrine — Unity 6 / URP

## Setup

1. Keep this entire `MoonwellShrine` folder together inside your project's `Assets` folder. A copy is already installed at `Assets/_Duskborn/Art/Models/MoonwellShrine` in the current project. Do not import a second copy there.
2. Let Unity finish importing and compiling. Choose **Duskborn > Art > Moonwell Shrine > Create or Rebuild Prefab**.
3. Drag **Generated/Moonwell_Shrine.prefab** into your scene. Its book hover and self-turning page play automatically in Play Mode.

The menu creates two URP materials, a looping Animator controller and the prefab with a simple table-base BoxCollider. It configures the FBX's scale, axes, blend shapes and animation import for you. Existing materials are preserved when you rerun setup; the generated prefab is rebuilt. Existing station prefabs, recipes and scenes are not changed.

The prefab is a visual prop with animation and collision. To use it for crafting, replace the visual child of your existing arcane-table station with this prefab and retain that station's gameplay/network components. This package does not add networking or crafting logic.

## Included

- `Models/Moonwell_Shrine.fbx`: four meshes, 3,126 triangles, separate animated book/page, 63 page blend shapes, baked 30 fps four-second take.
- `Textures/Moonwell_PaintedAtlas.png`: 1024 x 1024 sRGB atlas for stone, wood, iron, leather, parchment and painted markings. Keep it with the FBX; the FBX also references it using a relative path.
- `Editor/MoonwellUnitySetup.cs`: explicit setup menu; no automatic scene changes and no runtime script dependency.

The crystal/well material uses a violet base color and emission, so it needs no separate texture. Bloom is optional and follows your project's existing URP volume settings; the installer does not change lighting or post-processing.

## Import details

Scale: meters, roughly 1.67 m wide/deep and 2.32 m high; tabletop about 1.14 m high. Floor-center pivot. Generic animation with no Avatar, no root motion, no curve compression, and an exact looping endpoint. The `Moonwell_Idle` state is the controller's default. Materials use URP/Lit. Only the page needs a SkinnedMeshRenderer; its local bounds are expanded to contain every turn pose. Static body, crystal well and book covers are consolidated into three other meshes.

Do not enable FBX export-time triangulation on a re-export from Blender 5.1: it caused the animated shape-key curves to become constant in the tested exporter. The delivered FBX preserves polygons and Unity triangulates them on import. All static bevels are already baked; no Blender installation is needed to use the FBX.

## Verification

The final FBX was reimported into Blender and checked against the saved source across all 121 frames, including page reset. Scale, UVs, four meshes, 63 blend shapes, hover positions and page weights were checked, and a render of the reimported geometry was inspected. The setup script compiled offline against Unity 6000.4.1f1's assemblies.

Unity Editor import/prefab execution was not run because this project was open in Unity. After setup, check that the prefab has its atlas and violet emission, the page turns smoothly without clipping, and the prop's size/collider fits your scene. The setup script stops with a descriptive error if it cannot find the expected clip, page shapes or animation bindings.

Relevant Unity APIs: [ModelImporter.clipAnimations](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/modelimporter/clipanimations), [ModelImporterAvatarSetup](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/modelimporteravatarsetup).
