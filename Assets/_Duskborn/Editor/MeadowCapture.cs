using System;
using System.IO;
using Duskborn.Gameplay.World;
using Duskborn.Gameplay.World.Foliage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Duskborn.Editor
{
    /// <summary>Static production grass/terrain inspection without entering Play Mode.</summary>
    public static class MeadowCapture
    {
        public static void Benchmark()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var config = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LowPolyTerrainConfig>("Assets/_Duskborn/ScriptableObjects/World/TerrainConfig_RollingPlains.asset"));
            var times = new double[3];
            int vertexCount = 0;
            try
            {
                config.chunkSize = 128; config.chunksX = config.chunksZ = 4;
                config.waterLevel = -10f; config.centralSanctuaryRadius = 0f;
                for (int run = -1; run < times.Length; run++)
                {
                    var go = new GameObject("Foliage benchmark");
                    var chunk = go.AddComponent<TerrainChunk>();
                    chunk.Initialize(Vector2Int.zero, config, 4242);
                    var placer = go.AddComponent<ChunkFoliagePlacer>();
                    placer.SetDensityPreset(FoliageDensityPreset.High);
                    var occupancy = new SpatialOccupancyMap(4f);
                    try
                    {
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        placer.GenerateFoliage(config, 4242, occupancy);
                        timer.Stop();
                        if (run >= 0)
                        {
                            times[run] = timer.Elapsed.TotalMilliseconds;
                        }
                        vertexCount = 0;
                        foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
                            if (filter.gameObject != go && filter.sharedMesh != null) vertexCount += filter.sharedMesh.vertexCount;
                    }
                    finally
                    {
                        placer.ClearFoliage();
                        var terrainMesh = go.GetComponent<MeshFilter>().sharedMesh;
                        UnityEngine.Object.DestroyImmediate(go);
                        UnityEngine.Object.DestroyImmediate(terrainMesh);
                    }
                }
                // Simulate 60 Hz resumes without entering Play Mode to expose coroutine wait inflation.
                config.chunkSize = 32;
                var asyncGO = new GameObject("Foliage coroutine benchmark");
                var asyncChunk = asyncGO.AddComponent<TerrainChunk>();
                asyncChunk.Initialize(Vector2Int.zero, config, 4242);
                var asyncPlacer = asyncGO.AddComponent<ChunkFoliagePlacer>();
                asyncPlacer.SetDensityPreset(FoliageDensityPreset.High);
                int waits = 0;
                var simulation = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var stack = new System.Collections.Generic.Stack<System.Collections.IEnumerator>();
                    stack.Push(asyncPlacer.GenerateFoliageAsync(config, 4242, new SpatialOccupancyMap(4f), new GenerationBudget(2f)));
                    while (stack.Count > 0)
                    {
                        var next = stack.Peek();
                        if (!next.MoveNext()) { stack.Pop(); (next as IDisposable)?.Dispose(); continue; }
                        if (next.Current is System.Collections.IEnumerator nested) stack.Push(nested);
                        else { waits++; System.Threading.Thread.Sleep(16); }
                    }
                }
                finally
                {
                    simulation.Stop(); asyncPlacer.ClearFoliage();
                    var mesh = asyncGO.GetComponent<MeshFilter>().sharedMesh;
                    UnityEngine.Object.DestroyImmediate(asyncGO); UnityEngine.Object.DestroyImmediate(mesh);
                }
                Array.Sort(times);
                string result = "{\"chunkSize\":128,\"seed\":4242,\"medianMilliseconds\":" + times[1].ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"vertices\":" + vertexCount + ",\"simulationChunkSize\":32,\"simulatedFrameYields\":" + waits +
                    ",\"simulationMilliseconds\":" + simulation.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "}";
                Directory.CreateDirectory("Artifacts/Meadow");
                File.WriteAllText("Artifacts/Meadow/foliage-benchmark.json", result);
                Debug.Log("[MeadowBenchmark] " + result);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        public static void Run()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use capture-meadow with graphics enabled.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var oldPipeline = GraphicsSettings.defaultRenderPipeline;
            var oldQuality = QualitySettings.renderPipeline;
            var pipeline = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset"));
            var renderer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));
            var config = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LowPolyTerrainConfig>("Assets/_Duskborn/ScriptableObjects/World/TerrainConfig_RollingPlains.asset"));
            try
            {
                renderer.rendererFeatures.Clear(); renderer.renderingMode = RenderingMode.Forward;
                // Immediate batch captures require the same unbatched setup as the other art captures.
                // Production pipeline settings are restored below; live batched lighting remains a manual check.
                pipeline.useSRPBatcher = false;
                var pipelineData = new SerializedObject(pipeline);
                pipelineData.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                pipelineData.ApplyModifiedPropertiesWithoutUndo();
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                UniversalRenderPipelineDebugDisplaySettings.Instance.Reset();
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.18f, 0.24f, 0.27f);
                RenderSettings.fog = false;
                var sun = new GameObject("Meadow sun", typeof(Light)).GetComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 0.7f;
                sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;

                // A compact fixture retains production art colors while exposing a chunk join and clearing.
                config.chunkSize = 32; config.cellSize = 1f; config.chunksX = 2; config.chunksZ = 1;
                config.heightMultiplier = 8f; config.noiseScale = 0.015f; config.waterLevel = -1f;
                config.boundaryType = LowPolyTerrainConfig.MapBoundaryType.None;
                config.centralSanctuaryRadius = 0f;
                config.terraceStep = 0f;
                var occupancy = new SpatialOccupancyMap(4f);
                occupancy.RegisterClearing(new Vector2(38f, 18f), 4f, OccupancyType.Combat_Clearing);
                var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_TerrainLowPoly.mat");
                var grassMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Grass.mat");
                if (grassMaterial == null) throw new InvalidOperationException("Production grass material could not be loaded.");
                for (int x = 0; x < 2; x++)
                {
                    var go = new GameObject($"Meadow chunk {x}");
                    go.transform.position = new Vector3(x * 32f, 0f, 0f);
                    var chunk = go.AddComponent<TerrainChunk>();
                    chunk.Initialize(new Vector2Int(x, 0), config, 4242);
                    go.GetComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
                    var foliage = go.AddComponent<ChunkFoliagePlacer>();
                    foliage.SetMaterials(grassMaterial, null);
                    foliage.SetDensityPreset(FoliageDensityPreset.High);
                    foliage.GenerateFoliage(config, 4242, occupancy);
                }
                var camera = new GameObject("Meadow camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.fieldOfView = 55f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.70f, 0.78f);
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 200f;
                camera.transform.position = new Vector3(25f, 12f, -4f);
                camera.transform.LookAt(new Vector3(32f, 3.5f, 18f));
                Capture(camera, "meadow-overview.png");
                camera.transform.position = new Vector3(25f, 5.5f, 4f);
                camera.transform.LookAt(new Vector3(33f, 3.5f, 22f));
                Capture(camera, "meadow-close.png");
                VerifyLegacyUVIsolation(camera, grassMaterial);
                foreach (Shader shader in new[] { terrainMaterial.shader, Shader.Find("Duskborn/StylizedFoliage") })
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                            throw new InvalidOperationException(message.message);
                Debug.Log("[MeadowCapture] Capture succeeded: " + Path.GetFullPath("Artifacts/Meadow"));
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = oldPipeline;
                QualitySettings.renderPipeline = oldQuality;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(pipeline);
                UnityEngine.Object.DestroyImmediate(renderer);
            }
        }

        // GPU regression: old meshes must render identically with missing, lightmap, or arbitrary UV1.
        // The fixture sits beyond the meadow fade distance from its bogus root at the origin.
        private static void VerifyLegacyUVIsolation(Camera camera, Material material)
        {
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = "Legacy foliage UV regression"; prop.layer = 31;
            var mesh = UnityEngine.Object.Instantiate(prop.GetComponent<MeshFilter>().sharedMesh);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] += new Vector3(100f, 1f, 0f);
            mesh.vertices = vertices; mesh.RecalculateBounds();
            prop.GetComponent<MeshFilter>().sharedMesh = mesh;
            prop.GetComponent<MeshRenderer>().sharedMaterial = material;
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0.3f, 0.5f, 0.2f, 0f);
            mesh.colors = colors;
            camera.cullingMask = 1 << 31;
            camera.orthographic = true; camera.orthographicSize = 1.5f;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(100f, 1f, -5f);
            camera.transform.rotation = Quaternion.identity;
            try
            {
                mesh.uv2 = null;
                byte[] withoutUV = RenderLegacyProbe(camera);
                var uv2 = new Vector2[mesh.vertexCount];
                for (int i = 0; i < uv2.Length; i++) uv2[i] = new Vector2(0.2f, 0.8f);
                mesh.uv2 = uv2;
                byte[] lightmapUV = RenderLegacyProbe(camera);
                var roots = new System.Collections.Generic.List<Vector4>();
                for (int i = 0; i < mesh.vertexCount; i++) roots.Add(new Vector4(0f, 0f, 0f, 1f));
                mesh.SetUVs(1, roots);
                byte[] markedUV = RenderLegacyProbe(camera);
                int visible = 0;
                for (int i = 0; i < withoutUV.Length; i++)
                {
                    if (withoutUV[i] > 5) visible++;
                    if (Math.Abs(withoutUV[i] - lightmapUV[i]) > 1 || Math.Abs(withoutUV[i] - markedUV[i]) > 1)
                        throw new InvalidOperationException("Legacy foliage changed geometry/lighting when UV1 changed.");
                }
                if (visible < 100) throw new InvalidOperationException("Legacy UV regression fixture was not visible.");
                // Positive control recreates the former misclassification and must visibly collapse.
                var control = new Material(material);
                try
                {
                    control.SetFloat("_MeadowEnabled", 1f);
                    prop.GetComponent<MeshRenderer>().sharedMaterial = control;
                    byte[] collapsed = RenderLegacyProbe(camera);
                    int changed = 0;
                    for (int i = 0; i < withoutUV.Length; i++)
                        if (Math.Abs(withoutUV[i] - collapsed[i]) > 5) changed++;
                    if (changed < 100) throw new InvalidOperationException("Legacy UV probe failed to reproduce the original collapse with meadow deformation enabled.");
                }
                finally { UnityEngine.Object.DestroyImmediate(control); }
                Debug.Log("[MeadowCapture] Legacy UV isolation passed: missing, 2D and marked UV1 render identically beyond meadow fade distance.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prop);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static byte[] RenderLegacyProbe(Camera camera)
        {
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(128, 128, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); texture.Apply();
                return texture.GetRawTextureData<byte>().ToArray();
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Capture(Camera camera, string name)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                string path = Path.GetFullPath("Artifacts/Meadow/" + name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
