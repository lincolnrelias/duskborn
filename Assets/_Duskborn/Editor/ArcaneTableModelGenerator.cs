using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Duskborn.Gameplay.Crafting;
using Duskborn.Moonwell.Editor;

namespace Duskborn.Editor
{
    /// <summary>
    /// Updates the Arcane Table station prefabs to use the Moonwell Shrine model,
    /// materials, and its looping idle book animation.
    /// </summary>
    [InitializeOnLoad]
    public static class ArcaneTableModelGenerator
    {
        private const string RequestPath = "Assets/_Duskborn/Editor/.generate-arcane-table";
        private const string WorldPrefabPath = "Assets/_Duskborn/Prefabs/World/Station_ArcaneTable.prefab";
        private const string ResourcePrefabPath = "Assets/_Duskborn/Resources/Stations/Station_ArcaneTable.prefab";

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            if (File.Exists(RequestPath))
                EditorApplication.delayCall += GenerateFromRequest;
        }

        static ArcaneTableModelGenerator()
        {
            if (File.Exists(RequestPath))
                EditorApplication.delayCall += GenerateFromRequest;
        }

        private static void GenerateFromRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += GenerateFromRequest;
                return;
            }

            try
            {
                Generate();
                if (File.Exists(RequestPath))
                    File.Delete(RequestPath);
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [MenuItem("Duskborn/Art/Apply Moonwell To Arcane Table")]
        public static void Generate()
        {
            // 1. Build or rebuild Moonwell shrine assets (materials, animator controller, clip setup)
            MoonwellUnitySetup.Build();

            string root = "Assets/_Duskborn/Art/Models/MoonwellShrine";
            string modelPath = root + "/Models/Moonwell_Shrine.fbx";
            string generated = root + "/Generated";
            string controllerPath = generated + "/Moonwell_Idle.controller";
            string paintedMatPath = generated + "/Moonwell_Painted.mat";
            string crystalMatPath = generated + "/Moonwell_Crystal.mat";

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            var paintedMat = AssetDatabase.LoadAssetAtPath<Material>(paintedMatPath);
            var crystalMat = AssetDatabase.LoadAssetAtPath<Material>(crystalMatPath);

            if (model == null) throw new FileNotFoundException("Moonwell FBX not found at " + modelPath);
            if (controller == null) throw new FileNotFoundException("Moonwell controller not found at " + controllerPath);
            if (paintedMat == null) throw new FileNotFoundException("Moonwell painted material not found at " + paintedMatPath);
            if (crystalMat == null) throw new FileNotFoundException("Moonwell crystal material not found at " + crystalMatPath);

            // 2. Update both arcane table station prefabs
            UpdateArcaneTablePrefab(WorldPrefabPath, model, controller, paintedMat, crystalMat);
            UpdateArcaneTablePrefab(ResourcePrefabPath, model, controller, paintedMat, crystalMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArcaneTableModelGenerator] Successfully updated Arcane Table stations to use Moonwell model and idle animation!");
        }

        private static void UpdateArcaneTablePrefab(string path, GameObject model, RuntimeAnimatorController controller, Material paintedMat, Material crystalMat)
        {
            if (!File.Exists(path)) return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Remove old visuals or duplicate children
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var child = root.transform.GetChild(i);
                    if (child.name == "Visual" || child.name == "Moonwell_Shrine" || child.name.StartsWith("Moonwell"))
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }

                // Remove legacy root MeshFilter & MeshRenderer if present
                var rootMf = root.GetComponent<MeshFilter>();
                if (rootMf != null) UnityEngine.Object.DestroyImmediate(rootMf);
                var rootMr = root.GetComponent<MeshRenderer>();
                if (rootMr != null) UnityEngine.Object.DestroyImmediate(rootMr);

                // Instantiate model as child Visual
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                // Remove any collider that might be on the visual child or its descendants
                foreach (var c in visual.GetComponentsInChildren<Collider>(true))
                {
                    UnityEngine.Object.DestroyImmediate(c);
                }

                // Assign materials & shadows to all child renderers
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    var mat = renderer.name.StartsWith("Moonwell_Crystals", StringComparison.Ordinal) ? crystalMat : paintedMat;
                    renderer.sharedMaterials = Enumerable.Repeat(mat, renderer.sharedMaterials.Length).ToArray();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                // Expand bounds for skinned mesh (book page) so it never gets prematurely culled
                var page = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0);
                if (page != null)
                {
                    ExpandPageBounds(page);
                }

                // Setup Animator on visual to play the looping Moonwell_Idle animation
                var animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var stationAnim = visual.GetComponent<MoonwellStationAnimator>();
                if (stationAnim == null) visual.AddComponent<MoonwellStationAnimator>();

                // Configure root BoxCollider to match Moonwell footprint
                var collider = root.GetComponent<BoxCollider>();
                if (collider == null) collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.575f, 0f);
                collider.size = new Vector3(1.67f, 1.15f, 1.67f);

                // Configure root Rigidbody
                var rb = root.GetComponent<Rigidbody>();
                if (rb == null) rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                // Wire Workbench component
                var station = root.GetComponent<Workbench>();
                if (station != null)
                {
                    var serializedStation = new SerializedObject(station);
                    var outline = serializedStation.FindProperty("outlineRenderers");
                    if (outline != null)
                    {
                        outline.arraySize = renderers.Length;
                        for (int i = 0; i < renderers.Length; i++)
                            outline.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                    }
                    var stationTypeProp = serializedStation.FindProperty("stationType");
                    if (stationTypeProp != null)
                        stationTypeProp.intValue = (int)CraftingStationType.MesaArcana;

                    var displayNameProp = serializedStation.FindProperty("stationDisplayName");
                    if (displayNameProp != null)
                        displayNameProp.stringValue = "Mesa Arcana";

                    serializedStation.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ExpandPageBounds(SkinnedMeshRenderer renderer)
        {
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
            renderer.updateWhenOffscreen = true;
        }
    }
}
