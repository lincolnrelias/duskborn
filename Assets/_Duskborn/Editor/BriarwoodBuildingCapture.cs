using System;
using System.IO;
using System.Linq;
using Duskborn.Gameplay.Building;
using Duskborn.Gameplay.Loot;
using Duskborn.UI.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Duskborn.Editor
{
    public static class BriarwoodBuildingCapture
    {
        private const string Folder = "Assets/_Duskborn/Resources/UI/Briarwood/";
        public static void Icons()
        {
            RequireGraphics();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureSprites();
            var previousPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQuality = QualitySettings.renderPipeline;
            var pipeline = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset"));
            var renderer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));
            renderer.rendererFeatures.Clear();
            renderer.renderingMode = RenderingMode.Forward;
            pipeline.supportsHDR = false;
            var data = new SerializedObject(pipeline);
            data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            data.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var camera = new GameObject("Station icon camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear; camera.nearClipPlane = .01f; camera.farClipPlane = 1000f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var key = new GameObject("Warm key", typeof(Light)).GetComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.35f;
            key.color = new Color(1f, .91f, .79f); key.transform.rotation = Quaternion.Euler(40, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.52f, .55f, .58f);
            var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32); target.Create();
            try
            {
                foreach (var id in new[] { "workbench", "forge", "arcane_table" })
                {
                    var definition = Resources.Load<BuildableDefinition>("Building/Build_" + id);
                    if (definition == null || definition.prefab == null) throw new InvalidOperationException("Missing station prefab: " + id);
                    var model = UnityEngine.Object.Instantiate(definition.prefab);
                    model.transform.position = Vector3.zero;
                    foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
                    foreach (var canvas in model.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
                    var meshes = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
                    if (meshes.Length == 0) throw new InvalidOperationException("Station has no meshes: " + id);
                    var bounds = meshes[0].bounds; foreach (var mesh in meshes.Skip(1)) bounds.Encapsulate(mesh.bounds);
                    Debug.Log("[Briarwood] Model " + id + ": " + string.Join("; ", meshes.Select(r => r.name + " materials=" + string.Join(",", r.sharedMaterials.Where(m => m != null).Select(m => m.name + ":" + m.shader.name)))));
                    camera.transform.position = bounds.center + new Vector3(-.8f, .65f, id == "forge" ? 1f : -1f).normalized * bounds.size.magnitude * 3;
                    camera.transform.LookAt(bounds.center);
                    float extent = 0f;
                    for (int n = 0; n < 8; n++)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((n & 1) == 0 ? -1 : 1, (n & 2) == 0 ? -1 : 1, (n & 4) == 0 ? -1 : 1));
                        var projected = camera.transform.InverseTransformPoint(corner);
                        extent = Mathf.Max(extent, Mathf.Abs(projected.x), Mathf.Abs(projected.y));
                    }
                    camera.orthographicSize = extent * 1.08f;
                    // Warm the newly-created SRP camera before reading its first model.
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    Write(target, Folder + "Icon_" + id + ".png", true);
                    UnityEngine.Object.DestroyImmediate(model);
                    AssetDatabase.ImportAsset(Folder + "Icon_" + id + ".png", ImportAssetOptions.ForceSynchronousImport);
                    ConfigureTexture(Folder + "Icon_" + id + ".png", false, Vector4.zero);
                    Debug.Log("[Briarwood] Prefab icon captured: " + id);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("[Briarwood] Icons succeeded.");
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = previousPipeline; QualitySettings.renderPipeline = previousQuality;
                RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(key.gameObject);
                UnityEngine.Object.DestroyImmediate(renderer); UnityEngine.Object.DestroyImmediate(pipeline);
            }
        }

        public static void Preview()
        {
            RequireGraphics(); ConfigureSprites();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var previousPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQuality = QualitySettings.renderPipeline;
            // UI/Default is the production catalog shader; no scene renderer features are needed.
            GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
            var owner = new GameObject("Building catalog capture");
            var inventory = owner.AddComponent<ResourceInventory>();
            var definitions = Resources.LoadAll<BuildableDefinition>("Building").OrderBy(d => Array.IndexOf(new[] { "workbench", "forge", "arcane_table", "cauldron", "storage" }, d.id)).ToArray();
            foreach (var material in definitions.SelectMany(d => d.costs).Where(c => c.material != null).Select(c => c.material).Distinct()) inventory.Add(material.Id, 30);
            var manager = BuildingUIManager.Create(owner.transform);
            manager.ShowCatalog(definitions, d => BuildingPresentation.Create(d, inventory, null, false), _ => { }, () => { });
            var camera = new GameObject("Catalog capture camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .065f, .06f);
            var canvas = owner.GetComponentInChildren<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            try
            {
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                {
                    var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32); target.Create();
                    camera.targetTexture = target;
                    foreach (var id in new[] { "workbench", "forge", "arcane_table", "cauldron" })
                    {
                        owner.transform.Find("BuildingUI/BuildingCatalog/CatalogList/Cards/Content/Buildable_" + id).GetComponent<Button>().onClick.Invoke();
                        Canvas.ForceUpdateCanvases(); camera.Render();
                        Write(target, "Artifacts/BriarwoodUI/building-" + id + "-" + size.x + ".png", false);
                    }
                    camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(target);
                }
                Debug.Log("[Briarwood] UI capture succeeded.");
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = previousPipeline; QualitySettings.renderPipeline = previousQuality;
                RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void ConfigureSprites()
        {
            ConfigureTexture(Folder + "frame.png", true, new Vector4(180, 180, 180, 180));
            ConfigureTexture(Folder + "socket.png", true, new Vector4(100, 100, 100, 100));
            ConfigureTexture(Folder + "button.png", true, new Vector4(140, 48, 140, 48));
            ConfigureTexture(Folder + "crest.png", true, Vector4.zero);
        }
        private static void ConfigureTexture(string path, bool sprite, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing Briarwood texture: " + path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.Single; importer.spriteBorder = border;
            importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = path.EndsWith("crest.png") ? 512 : 1024;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        private static void Write(RenderTexture target, string path, bool alpha)
        {
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(texture);
        }
        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Briarwood captures need the graphics-enabled CLI command.");
        }
    }
}
