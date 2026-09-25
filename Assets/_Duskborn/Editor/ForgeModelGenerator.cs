using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Duskborn.Gameplay.Crafting;

namespace Duskborn.Editor
{
    /// <summary>
    /// Assigns the Fresco Furnace model and atlas material to both runtime copies
    /// of the forge prefab.
    /// </summary>
    [InitializeOnLoad]
    public static class ForgeModelGenerator
    {
        private const string ModelDirectory = "Assets/_Duskborn/Art/Models/Forge";
        private const string MeshPath = ModelDirectory + "/Forge_Stylized.asset";
        private const string ModelPath = ModelDirectory + "/Fresco_Furnace.fbx";
        private const string TexturePath = ModelDirectory + "/Furnace_Fresco.png";
        private const string MaterialPath = ModelDirectory + "/MAT_Fresco_Furnace.mat";
        private const string WorldPrefabPath = "Assets/_Duskborn/Prefabs/World/Station_Forge.prefab";
        private const string ResourcePrefabPath = "Assets/_Duskborn/Resources/Stations/Station_Forge.prefab";
        private const string PreviewPath = "Artifacts/ForgeModel/forge-model-preview.png";
        private const string RequestPath = "Assets/_Duskborn/Editor/.generate-forge";

        static ForgeModelGenerator()
        {
            if (File.Exists(RequestPath)) EditorApplication.delayCall += GenerateFromRequest;
        }

        private enum Surface
        {
            Stone,
            Iron,
            Wood,
            Soot,
            Ember,
            Count
        }

        [MenuItem("Duskborn/Art/Generate Stylized Forge")]
        public static void Generate()
        {
            EnsureFolder("Assets/_Duskborn/Art/Models", "Forge");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (model == null || texture == null)
                throw new FileNotFoundException($"Fresco Furnace model or texture is missing: {ModelPath}, {TexturePath}");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = "MAT_Fresco_Furnace" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);

            UpdatePrefab(WorldPrefabPath, model, material);
            UpdatePrefab(ResourcePrefabPath, model, material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ForgeModelGenerator] Updated both forge prefabs to use Fresco_Furnace.fbx and Furnace_Fresco.png.");
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
                File.Delete(RequestPath);
                AssetDatabase.Refresh();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void BuildMesh(Mesh mesh)
        {
            var builder = new LowPolyMeshBuilder((int)Surface.Count);

            // Broad, readable base. All geometry stays inside the authored 1.74 x 1.07 footprint.
            builder.AddBox(new Vector3(0f, 0.07f, 0f), new Vector3(1.72f, 0.14f, 1.04f),
                Quaternion.identity, Surface.Stone);
            builder.AddBox(new Vector3(0.23f, 0.17f, -0.38f), new Vector3(1.04f, 0.18f, 0.30f),
                Quaternion.identity, Surface.Stone);

            // Masonry firebox: a deep back, two jambs and a heavy lintel frame the opening.
            builder.AddBox(new Vector3(0.23f, 0.49f, 0.31f), new Vector3(1.06f, 0.76f, 0.34f),
                Quaternion.identity, Surface.Stone);
            builder.AddBox(new Vector3(-0.20f, 0.48f, -0.34f), new Vector3(0.20f, 0.64f, 0.22f),
                Quaternion.Euler(0f, -2f, 1f), Surface.Stone);
            builder.AddBox(new Vector3(0.66f, 0.48f, -0.34f), new Vector3(0.20f, 0.64f, 0.22f),
                Quaternion.Euler(0f, 2f, -1f), Surface.Stone);
            builder.AddBox(new Vector3(0.23f, 0.77f, -0.34f), new Vector3(1.02f, 0.20f, 0.24f),
                Quaternion.identity, Surface.Stone);

            // Oversized block courses create a hand-built rhythm without relying on textures.
            for (var row = 0; row < 3; row++)
            {
                var y = 0.27f + row * 0.20f;
                builder.AddBox(new Vector3(-0.29f, y, 0.08f), new Vector3(0.10f, 0.15f, 0.30f),
                    Quaternion.Euler(0f, row % 2 == 0 ? -3f : 2f, 0f), Surface.Stone);
                builder.AddBox(new Vector3(0.75f, y, 0.08f), new Vector3(0.10f, 0.15f, 0.30f),
                    Quaternion.Euler(0f, row % 2 == 0 ? 3f : -2f, 0f), Surface.Stone);
            }

            // Black firebox inset and a bed of stylized, emissive coals.
            builder.AddBox(new Vector3(0.23f, 0.49f, -0.465f), new Vector3(0.66f, 0.45f, 0.035f),
                Quaternion.identity, Surface.Soot);
            builder.AddBox(new Vector3(0.23f, 0.27f, -0.49f), new Vector3(0.72f, 0.06f, 0.19f),
                Quaternion.identity, Surface.Soot);
            builder.AddLowPolySphere(new Vector3(0.04f, 0.31f, -0.52f), 0.11f, Surface.Ember);
            builder.AddLowPolySphere(new Vector3(0.25f, 0.32f, -0.54f), 0.14f, Surface.Ember);
            builder.AddLowPolySphere(new Vector3(0.47f, 0.31f, -0.51f), 0.10f, Surface.Ember);
            builder.AddCylinder(new Vector3(0.12f, 0.38f, -0.55f), 0.045f, 0.43f,
                Quaternion.Euler(78f, 10f, 78f), 6, Surface.Wood);
            builder.AddCylinder(new Vector3(0.39f, 0.38f, -0.54f), 0.045f, 0.39f,
                Quaternion.Euler(75f, -8f, 101f), 6, Surface.Wood);

            // Hammered iron hood and chimney establish the forge silhouette at a distance.
            builder.AddFrustum(new Vector3(0.23f, 1.00f, 0.02f),
                new Vector2(1.16f, 0.82f), new Vector2(0.48f, 0.42f), 0.52f, Surface.Iron);
            builder.AddCylinder(new Vector3(0.23f, 1.54f, 0.04f), 0.225f, 0.78f,
                Quaternion.identity, 8, Surface.Iron);
            builder.AddCylinder(new Vector3(0.23f, 1.94f, 0.04f), 0.27f, 0.09f,
                Quaternion.identity, 8, Surface.Iron);
            builder.AddCylinder(new Vector3(0.23f, 1.995f, 0.04f), 0.19f, 0.015f,
                Quaternion.identity, 8, Surface.Soot);

            // Side anvil: chunky base, narrow waist, slab and a wedge-shaped horn.
            builder.AddBox(new Vector3(-0.56f, 0.18f, 0.02f), new Vector3(0.42f, 0.16f, 0.43f),
                Quaternion.identity, Surface.Iron);
            builder.AddFrustum(new Vector3(-0.56f, 0.36f, 0.02f),
                new Vector2(0.34f, 0.31f), new Vector2(0.23f, 0.22f), 0.30f, Surface.Iron);
            builder.AddBox(new Vector3(-0.56f, 0.56f, 0.02f), new Vector3(0.42f, 0.15f, 0.32f),
                Quaternion.identity, Surface.Iron);
            builder.AddWedge(new Vector3(-0.75f, 0.57f, 0.02f), new Vector3(0.20f, 0.14f, 0.28f),
                Quaternion.identity, Surface.Iron);

            // Leather-and-wood bellows tucked behind the anvil, with an iron nozzle.
            builder.AddWedge(new Vector3(-0.60f, 0.37f, 0.36f), new Vector3(0.42f, 0.19f, 0.35f),
                Quaternion.Euler(0f, 180f, 0f), Surface.Wood);
            builder.AddBox(new Vector3(-0.60f, 0.47f, 0.44f), new Vector3(0.10f, 0.07f, 0.40f),
                Quaternion.Euler(-12f, 0f, 0f), Surface.Wood);
            builder.AddCylinder(new Vector3(-0.37f, 0.39f, 0.15f), 0.045f, 0.38f,
                Quaternion.Euler(0f, 0f, 90f), 6, Surface.Iron);

            builder.ApplyTo(mesh);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
        }

        private static Material CreateOrUpdateMaterial(string name, Color color, float metallic, float smoothness,
            Color? emission = null)
        {
            var path = ModelDirectory + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void UpdatePrefab(string path, GameObject model, Material material)
        {
            if (!File.Exists(path)) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var oldModel = root.transform.Find("Fresco_Furnace");
                if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Fresco_Furnace";
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (var modelRenderer in renderers) modelRenderer.sharedMaterial = material;
                var renderer = root.GetComponent<MeshRenderer>();
                var collider = root.GetComponent<BoxCollider>();
                if (renderers.Length == 0)
                    throw new MissingComponentException($"Fresco Furnace model has no renderers: {ModelPath}");
                if (renderer != null) renderer.enabled = false;
                foreach (var modelRenderer in renderers)
                {
                    modelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    modelRenderer.receiveShadows = true;
                }
                var station = root.GetComponent<Workbench>();
                if (station != null)
                {
                    var serializedStation = new SerializedObject(station);
                    var outline = serializedStation.FindProperty("outlineRenderers");
                    if (outline != null)
                    {
                        outline.arraySize = renderers.Length;
                        for (var i = 0; i < renderers.Length; i++)
                            outline.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                    }
                    serializedStation.ApplyModifiedPropertiesWithoutUndo();
                }
                if (collider != null)
                {
                    collider.center = new Vector3(0f, 0.85f, 0f);
                    collider.size = new Vector3(1.65f, 1.7f, 1.65f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RenderPreview(Mesh mesh, Material[] materials)
        {
            var absolutePath = Path.GetFullPath(PreviewPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

            var model = new GameObject("ForgePreviewModel");
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = materials;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "PreviewGround";
            ground.transform.localScale = new Vector3(0.45f, 1f, 0.45f);
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundMaterial.color = new Color(0.075f, 0.085f, 0.075f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            var cameraObject = new GameObject("PreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(-3.15f, 2.35f, -3.45f);
            camera.transform.LookAt(new Vector3(0f, 0.86f, 0f));
            camera.fieldOfView = 31f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.055f);
            camera.allowHDR = true;

            var keyObject = new GameObject("PreviewKey");
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 2.2f;
            key.color = new Color(1f, 0.80f, 0.62f);
            key.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

            var fillObject = new GameObject("PreviewFill");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 5f;
            fill.intensity = 5f;
            fill.color = new Color(0.32f, 0.48f, 1f);
            fill.transform.position = new Vector3(-1.7f, 1.8f, -0.5f);

            var fireObject = new GameObject("PreviewFire");
            var fire = fireObject.AddComponent<Light>();
            fire.type = LightType.Point;
            fire.range = 2.3f;
            fire.intensity = 8f;
            fire.color = new Color(1f, 0.20f, 0.025f);
            fire.transform.position = new Vector3(0.23f, 0.42f, -0.72f);

            var renderTexture = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                var image = new Texture2D(800, 800, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                image.Apply();
                File.WriteAllBytes(absolutePath, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(groundMaterial);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(keyObject);
                Object.DestroyImmediate(fillObject);
                Object.DestroyImmediate(fireObject);
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private sealed class LowPolyMeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int>[] triangles;

            public LowPolyMeshBuilder(int subMeshCount)
            {
                triangles = new List<int>[subMeshCount];
                for (var i = 0; i < subMeshCount; i++) triangles[i] = new List<int>();
            }

            public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, Surface surface)
            {
                var h = size * 0.5f;
                var corners = new[]
                {
                    new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                    new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                    new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z),
                    new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
                };
                for (var i = 0; i < corners.Length; i++) corners[i] = center + rotation * corners[i];
                AddQuad(corners[0], corners[3], corners[2], corners[1], surface);
                AddQuad(corners[5], corners[6], corners[7], corners[4], surface);
                AddQuad(corners[4], corners[7], corners[3], corners[0], surface);
                AddQuad(corners[1], corners[2], corners[6], corners[5], surface);
                AddQuad(corners[3], corners[7], corners[6], corners[2], surface);
                AddQuad(corners[4], corners[0], corners[1], corners[5], surface);
            }

            public void AddFrustum(Vector3 center, Vector2 bottomSize, Vector2 topSize, float height, Surface surface)
            {
                var y0 = center.y - height * 0.5f;
                var y1 = center.y + height * 0.5f;
                var b = new[]
                {
                    new Vector3(center.x - bottomSize.x * .5f, y0, center.z - bottomSize.y * .5f),
                    new Vector3(center.x + bottomSize.x * .5f, y0, center.z - bottomSize.y * .5f),
                    new Vector3(center.x + bottomSize.x * .5f, y0, center.z + bottomSize.y * .5f),
                    new Vector3(center.x - bottomSize.x * .5f, y0, center.z + bottomSize.y * .5f)
                };
                var t = new[]
                {
                    new Vector3(center.x - topSize.x * .5f, y1, center.z - topSize.y * .5f),
                    new Vector3(center.x + topSize.x * .5f, y1, center.z - topSize.y * .5f),
                    new Vector3(center.x + topSize.x * .5f, y1, center.z + topSize.y * .5f),
                    new Vector3(center.x - topSize.x * .5f, y1, center.z + topSize.y * .5f)
                };
                AddQuad(b[0], t[0], t[1], b[1], surface);
                AddQuad(b[1], t[1], t[2], b[2], surface);
                AddQuad(b[2], t[2], t[3], b[3], surface);
                AddQuad(b[3], t[3], t[0], b[0], surface);
                AddQuad(t[0], t[3], t[2], t[1], surface);
                AddQuad(b[3], b[0], b[1], b[2], surface);
            }

            public void AddCylinder(Vector3 center, float radius, float height, Quaternion rotation, int sides, Surface surface)
            {
                var bottom = new Vector3[sides];
                var top = new Vector3[sides];
                for (var i = 0; i < sides; i++)
                {
                    var angle = Mathf.PI * 2f * i / sides;
                    var radial = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    bottom[i] = center + rotation * (radial + Vector3.down * height * .5f);
                    top[i] = center + rotation * (radial + Vector3.up * height * .5f);
                }
                for (var i = 0; i < sides; i++)
                {
                    var next = (i + 1) % sides;
                    AddQuad(bottom[i], top[i], top[next], bottom[next], surface);
                    AddTriangle(center + rotation * Vector3.down * height * .5f, bottom[next], bottom[i], surface);
                    AddTriangle(center + rotation * Vector3.up * height * .5f, top[i], top[next], surface);
                }
            }

            public void AddLowPolySphere(Vector3 center, float radius, Surface surface)
            {
                var top = center + Vector3.up * radius;
                var bottom = center + Vector3.down * radius * .65f;
                var ring = new Vector3[6];
                for (var i = 0; i < ring.Length; i++)
                {
                    var angle = Mathf.PI * 2f * i / ring.Length;
                    ring[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }
                for (var i = 0; i < ring.Length; i++)
                {
                    var next = (i + 1) % ring.Length;
                    AddTriangle(top, ring[i], ring[next], surface);
                    AddTriangle(bottom, ring[next], ring[i], surface);
                }
            }

            public void AddWedge(Vector3 center, Vector3 size, Quaternion rotation, Surface surface)
            {
                var h = size * .5f;
                var p = new[]
                {
                    new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                    new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z),
                    new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z)
                };
                for (var i = 0; i < p.Length; i++) p[i] = center + rotation * p[i];
                AddQuad(p[0], p[4], p[5], p[3], surface);
                AddQuad(p[0], p[1], p[2], p[3], surface);
                AddQuad(p[1], p[4], p[0], p[2], surface);
                AddQuad(p[2], p[5], p[4], p[1], surface);
                AddQuad(p[3], p[5], p[2], p[0], surface);
            }

            public void ApplyTo(Mesh mesh)
            {
                mesh.Clear();
                // Match the authored placement collider centered at z = -0.10 with a 1.07 m depth.
                // The mild depth compression also keeps the silhouette chunky at the game's camera distance.
                var fittedVertices = new List<Vector3>(vertices.Count);
                for (var i = 0; i < vertices.Count; i++)
                {
                    var vertex = vertices[i];
                    fittedVertices.Add(new Vector3(vertex.x, vertex.y, vertex.z * 0.78f - 0.10f));
                }
                mesh.SetVertices(fittedVertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = triangles.Length;
                for (var i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
            }

            private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Surface surface)
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                triangles[(int)surface].Add(start); triangles[(int)surface].Add(start + 1); triangles[(int)surface].Add(start + 2);
                triangles[(int)surface].Add(start); triangles[(int)surface].Add(start + 2); triangles[(int)surface].Add(start + 3);
            }

            private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Surface surface)
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uvs.Add(new Vector2(.5f, 1f)); uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                triangles[(int)surface].Add(start); triangles[(int)surface].Add(start + 1); triangles[(int)surface].Add(start + 2);
            }
        }
    }
}
