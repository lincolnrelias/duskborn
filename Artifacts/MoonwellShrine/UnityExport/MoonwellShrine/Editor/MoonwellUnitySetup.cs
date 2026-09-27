using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Duskborn.Moonwell.Editor
{
    /// <summary>Explicit, one-click setup. Never runs automatically on import.</summary>
    public static class MoonwellUnitySetup
    {
        [MenuItem("Duskborn/Art/Moonwell Shrine/Create or Rebuild Prefab")]
        public static void Build()
        {
            string root = FindPackageRoot();
            string modelPath = root + "/Models/Moonwell_Shrine.fbx";
            string texturePath = root + "/Textures/Moonwell_PaintedAtlas.png";
            string generated = root + "/Generated";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Moonwell Shrine needs the Universal Render Pipeline/Lit shader. Install and enable URP first.");
            if (!AssetDatabase.IsValidFolder(generated))
                AssetDatabase.CreateFolder(root, "Generated");

            var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter == null) throw new FileNotFoundException("Moonwell atlas is missing.", texturePath);
            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.sRGBTexture = true;
            textureImporter.alphaSource = TextureImporterAlphaSource.None;
            textureImporter.mipmapEnabled = true;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.filterMode = FilterMode.Bilinear;
            textureImporter.anisoLevel = 4;
            textureImporter.maxTextureSize = 1024;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            var painted = LoadOrCreateMaterial(generated + "/Moonwell_Painted.mat", shader, out bool newPainted);
            if (newPainted)
            {
                painted.SetTexture("_BaseMap", texture);
                painted.SetColor("_BaseColor", Color.white);
                painted.SetFloat("_Metallic", 0f);
                painted.SetFloat("_Smoothness", 0.18f);
                EditorUtility.SetDirty(painted);
            }
            var crystal = LoadOrCreateMaterial(generated + "/Moonwell_Crystal.mat", shader, out bool newCrystal);
            if (newCrystal)
            {
                // Blender shader constants are linear; Unity material colors are sRGB.
                crystal.SetColor("_BaseColor", new Color(0.30f, 0.09f, 0.56f, 1f).gamma);
                crystal.SetFloat("_Metallic", 0f);
                crystal.SetFloat("_Smoothness", 0.62f);
                crystal.EnableKeyword("_EMISSION");
                crystal.SetColor("_EmissionColor", new Color(0.35f, 0.075f, 0.8f, 1f) * 0.85f);
                crystal.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(crystal);
            }

            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new FileNotFoundException("Moonwell FBX is missing.", modelPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapes = true;
            importer.importBlendShapeNormals = ModelImporterNormals.Calculate;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.optimizeGameObjects = false;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Moonwell_Painted_Atlas"), painted);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Moonwell_Crystal_Emission"), crystal);
            importer.SaveAndReimport();

            var takes = importer.defaultClipAnimations;
            if (takes.Length != 1)
                throw new InvalidOperationException("Expected one four-second Moonwell take, found " + takes.Length + ". Check the model import warnings.");
            var take = takes[0];
            take.name = "Moonwell_Idle";
            take.loopTime = true;
            take.loopPose = false; // The authored endpoints already match exactly.
            take.keepOriginalOrientation = true;
            take.keepOriginalPositionY = true;
            take.keepOriginalPositionXZ = true;
            take.lockRootRotation = true;
            take.lockRootHeightY = true;
            take.lockRootPositionXZ = true;
            importer.clipAnimations = new[] { take };
            importer.SaveAndReimport();

            var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .SingleOrDefault(a => a.name == "Moonwell_Idle");
            if (clip == null || Mathf.Abs(clip.length - 4f) > 0.05f)
                throw new InvalidOperationException("Moonwell_Idle must be a four-second clip. Check the FBX animation import.");
            var curves = AnimationUtility.GetCurveBindings(clip);
            if (!curves.Any(b => b.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)))
                throw new InvalidOperationException("Page blend-shape animation was not imported; prefab creation stopped to avoid a static book.");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var holder = new GameObject("Moonwell_Shrine");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(holder.transform, false);
                visual.name = "Visual";
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var material = renderer.name.StartsWith("Moonwell_Crystals", StringComparison.Ordinal) ? crystal : painted;
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                }
                var page = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .SingleOrDefault(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0);
                if (page == null || page.sharedMesh.blendShapeCount != 63)
                    throw new InvalidOperationException("Expected all 63 page blend shapes. Prefab creation stopped.");
                ExpandPageBounds(page);
                foreach (var binding in curves.Where(b => b.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)))
                {
                    var target = visual.transform.Find(binding.path);
                    var renderer = target == null ? null : target.GetComponent<SkinnedMeshRenderer>();
                    string shapeName = binding.propertyName.Substring("blendShape.".Length);
                    if (renderer == null || renderer.sharedMesh.GetBlendShapeIndex(shapeName) < 0)
                        throw new InvalidOperationException("Animation binding does not resolve: " + binding.path + "/" + shapeName);
                }

                string controllerPath = generated + "/Moonwell_Idle.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Moonwell_Idle")
                    ?? machine.AddState("Moonwell_Idle");
                state.motion = clip;
                state.speed = 1f;
                machine.defaultState = state;
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var collider = holder.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.575f, 0f);
                collider.size = new Vector3(1.67f, 1.15f, 1.67f);
                string prefabPath = generated + "/Moonwell_Shrine.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(holder, prefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save Moonwell prefab.");
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("Moonwell Shrine ready: " + prefabPath + ". Drag it into the scene; its book animation plays automatically in Play Mode. Existing crafting station prefabs were not modified.");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }

        private static string FindPackageRoot()
        {
            var candidates = AssetDatabase.FindAssets("MoonwellUnitySetup t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("/Editor/MoonwellUnitySetup.cs", StringComparison.Ordinal))
                .Select(p => Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/'))
                .Where(p => File.Exists(p + "/Models/Moonwell_Shrine.fbx")).ToArray();
            if (candidates.Length != 1)
                throw new FileNotFoundException("Place exactly one complete MoonwellShrine folder inside Assets before running setup.");
            return candidates[0];
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader, out bool created)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            created = material == null;
            if (created)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static void ExpandPageBounds(SkinnedMeshRenderer renderer)
        {
            // Editor mesh access works even with runtime Read/Write disabled.
            var mesh = renderer.sharedMesh;
            var vertices = mesh.vertices;
            var deltas = new Vector3[vertices.Length];
            Bounds bounds = mesh.bounds;
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                mesh.GetBlendShapeFrameVertices(shape, 0, deltas, null, null);
                for (int i = 0; i < vertices.Length; i++) bounds.Encapsulate(vertices[i] + deltas[i]);
            }
            bounds.Expand(0.04f);
            renderer.localBounds = bounds;
            renderer.updateWhenOffscreen = false;
        }
    }
}
