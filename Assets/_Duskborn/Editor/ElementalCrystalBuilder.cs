using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Duskborn.Effects;
using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.World;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using InventorySystem.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Duskborn.Editor
{
    public static class ElementalCrystalBuilder
    {
        public const string Root = "Assets/_Duskborn/Resources/ElementalCrystals";
        public static string NodePath(RuneKind kind) => "Assets/_Duskborn/Prefabs/World/Node_" + ElementalCrystalCatalog.Element(kind) + "_Crystal.prefab";
        public static string DropPath(RuneKind kind) => "Assets/_Duskborn/Prefabs/DroppableItems/droppable_" + ElementalCrystalCatalog.Element(kind) + "_crystal.prefab";
        [MenuItem("Duskborn/Build Elemental Crystals")]
        public static void Build()
        {
            foreach (string directory in new[] { Root + "/Items", Root + "/Props", Root + "/Loot", Root + "/Materials", Root + "/Meshes" }) Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
            var props = new List<PropDefinition>();
            for (int i = 1; i <= 8; i++)
            {
                RuneKind kind = (RuneKind)i; string element = ElementalCrystalCatalog.Element(kind);
                string modelPath = "Assets/_Duskborn/Art/Models/ElementalCrystals/" + element + "_crystal_node.fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
                importer.importAnimation = false; importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                var materials = new Dictionary<string, Material>();
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                Require(model != null, "Missing original crystal model " + element);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    foreach (var source in renderer.sharedMaterials)
                    {
                        if (source == null || materials.ContainsKey(source.name)) continue;
                        string role = source.name.Contains("Bedrock") ? "Bedrock" : source.name.Contains("Accent") ? "Accent" : source.name.Contains("Glow") ? "Glow" : "Crystal";
                        var material = LoadOrCreate<Material>(Root + "/Materials/" + element + "_" + role + ".mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
                        material.color = source.color; material.SetFloat("_Smoothness", role == "Bedrock" ? .1f : .38f);
                        if (role == "Crystal" || role == "Glow")
                        { material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive; material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", RuneCatalog.Color(kind) * (role == "Glow" ? 1.3f : .35f)); }
                        else material.SetColor("_EmissionColor", Color.black);
                        EditorUtility.SetDirty(material); materials[source.name] = material;
                    }
                var drop = BuildDrop(kind, materials.First(pair => pair.Key.Contains("_Crystal")).Value);
                var item = LoadOrCreate<MaterialDefinition>(Root + "/Items/" + ElementalCrystalCatalog.Id(kind) + ".asset", () => ScriptableObject.CreateInstance<MaterialDefinition>());
                string title = char.ToUpperInvariant(element[0]) + element.Substring(1) + " Crystal";
                var itemData = new SerializedObject(item);
                itemData.FindProperty("id").stringValue = ElementalCrystalCatalog.Id(kind);
                itemData.FindProperty("displayName").stringValue = title;
                itemData.FindProperty("description").stringValue = "Mined from magical " + element + " crystal nodes. Used at the Arcane Table to craft " + kind + " runestones.";
                itemData.FindProperty("rarity").enumValueIndex = (int)ItemRarity.Uncommon;
                itemData.FindProperty("category").stringValue = "crystal";
                string iconPath = "Assets/_Duskborn/Resources/Textures/ElementalCrystals/" + element + "_crystal.png";
                var iconImporter = (TextureImporter)AssetImporter.GetAtPath(iconPath);
                iconImporter.textureShape = TextureImporterShape.Texture2D; iconImporter.textureType = TextureImporterType.Default;
                iconImporter.alphaIsTransparency = true; iconImporter.mipmapEnabled = false; iconImporter.maxTextureSize = 256;
                iconImporter.wrapMode = TextureWrapMode.Clamp; iconImporter.SaveAndReimport();
                itemData.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                itemData.FindProperty("dropPrefab").objectReferenceValue = drop; itemData.ApplyModifiedPropertiesWithoutUndo();
                var loot = LoadOrCreate<DropLootTable>(Root + "/Loot/" + element + "_crystal_loot.asset", () => ScriptableObject.CreateInstance<DropLootTable>());
                loot.entries = new[] { new DropEntry { itemDefinition = item, baseChance = 1, scalingBonus = 0, minAmount = 3, maxAmount = 5 } };
                loot.goldMin = loot.goldMax = 0; EditorUtility.SetDirty(loot);
                var node = BuildNode(kind, model, materials, loot);
                var prop = LoadOrCreate<PropDefinition>(Root + "/Props/Prop_" + element + "_Crystal.asset", () => ScriptableObject.CreateInstance<PropDefinition>());
                prop.propName = title + " Node"; prop.prefab = node; prop.prefabVariations = Array.Empty<GameObject>();
                // Sparse individual deposits distributed through every chunk, outside protected clearings.
                prop.clusterSettings.enableClustering = false; prop.minPerChunk = 0; prop.maxPerChunk = 1;
                prop.useClustering = false; prop.minRadialDistance = 18; prop.maxRadialDistance = 0;
                prop.minHeight = 1; prop.maxHeight = 120; prop.maxSlopeAngle = 35;
                prop.scaleRange = new Vector2(.85f, 1.15f); prop.heightScaleMultiplier = Vector2.one;
                prop.solidRadius = 1; prop.canopyRadius = 0; prop.exclusionRadius = 3.2f;
                prop.alignToNormal = true; prop.randomYRotation = true; EditorUtility.SetDirty(prop); props.Add(prop);
                for (int level = 1; level <= 3; level++)
                {
                    var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_" + RuneCatalog.Id(kind, level)); Require(recipe != null, "Missing runestone recipe");
                    var recipeData = new SerializedObject(recipe); var ingredients = recipeData.FindProperty("ingredients");
                    int slot = -1;
                    for (int n = 0; n < ingredients.arraySize; n++)
                        if (ingredients.GetArrayElementAtIndex(n).FindPropertyRelative("material").objectReferenceValue == item) slot = n;
                    if (slot < 0) { slot = ingredients.arraySize; ingredients.arraySize++; }
                    var entry = ingredients.GetArrayElementAtIndex(slot); entry.FindPropertyRelative("material").objectReferenceValue = item;
                    entry.FindPropertyRelative("amount").intValue = ElementalCrystalCatalog.CraftingCost(level);
                    recipeData.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (string registry in new[] { "Assets/DefaultPrefabObjects.asset", "Assets/_Duskborn/Network/DefaultPrefabObjects.asset" })
                {
                    var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(registry); Require(collection != null, "Missing FishNet collection");
                    collection.AddObject(node.GetComponent<NetworkObject>(), true, false); collection.AddObject(drop.GetComponent<NetworkObject>(), true, false); EditorUtility.SetDirty(collection);
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:WorldPropsConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<WorldPropsConfig>(AssetDatabase.GUIDToAssetPath(guid));
                var extras = new List<PropDefinition>(config.extraProps ?? Array.Empty<PropDefinition>());
                foreach (var prop in props) if (!extras.Contains(prop)) extras.Add(prop);
                config.extraProps = extras.ToArray(); EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets(); ElementalCrystalTests.RunAllTests(); Debug.Log("[ElementalCrystalBuilder] Build succeeded.");
        }
        private static GameObject BuildNode(RuneKind kind, GameObject model, Dictionary<string, Material> materials, DropLootTable loot)
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Duskborn/Prefabs/World/Node_Iron.prefab");
            try
            {
                root.name = "Node_" + ElementalCrystalCatalog.Element(kind) + "_Crystal"; root.transform.position = Vector3.zero;
                Object.DestroyImmediate(root.GetComponent<MeshRenderer>()); Object.DestroyImmediate(root.GetComponent<MeshFilter>());
                var visual = Object.Instantiate(model, root.transform); visual.name = "Authored " + ElementalCrystalCatalog.Element(kind) + " crystal model";
                visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material => materials[material.name]).ToArray();
                var node = new SerializedObject(root.GetComponent<ResourceNode>());
                node.FindProperty("maxHP").floatValue = 100;
                node.FindProperty("outlineRenderer").objectReferenceValue = visual.GetComponentsInChildren<MeshRenderer>().First(renderer => renderer.sharedMaterial.name.Contains("Crystal"));
                node.ApplyModifiedPropertiesWithoutUndo();
                var collider = root.GetComponent<CapsuleCollider>(); collider.radius = .8f; collider.height = 2.1f; collider.center = new Vector3(0, .9f, 0);
                root.GetComponent<LootDropper>().SetLootTable(loot);
                root.AddComponent<ElementalCrystalNodeVisual>().Configure(kind);
                PrepareNetwork(root, NodePath(kind)); return PrefabUtility.SaveAsPrefabAsset(root, NodePath(kind));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static GameObject BuildDrop(RuneKind kind, Material material)
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Duskborn/Prefabs/DroppableItems/droppable_iron.prefab");
            try
            {
                root.name = "droppable_" + ElementalCrystalCatalog.Element(kind) + "_crystal";
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) Object.DestroyImmediate(renderer);
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>()) Object.DestroyImmediate(filter);
                var mesh = LoadOrCreate<Mesh>(Root + "/Meshes/CrystalPickup.asset", () => new Mesh());
                if (mesh.vertexCount == 0)
                {
                    var vertices = new List<Vector3>(); for (int i = 0; i < 6; i++) vertices.Add(new Vector3(Mathf.Cos(i * Mathf.PI / 3) * .14f, 0, Mathf.Sin(i * Mathf.PI / 3) * .14f));
                    vertices.Add(Vector3.up * .35f); vertices.Add(Vector3.down * .1f);
                    var triangles = new List<int>(); for (int i = 0; i < 6; i++) { int n = (i + 1) % 6; triangles.AddRange(new[] { i, 6, n, i, n, 7 }); }
                    mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                }
                var shard = new GameObject("Elemental crystal shard", typeof(MeshFilter), typeof(MeshRenderer)); shard.transform.SetParent(root.transform, false);
                shard.GetComponent<MeshFilter>().sharedMesh = mesh; shard.GetComponent<MeshRenderer>().sharedMaterial = material;
                var pickup = new SerializedObject(root.GetComponent<WorldItemPickup>()); pickup.FindProperty("outlineRenderer").objectReferenceValue = shard.GetComponent<MeshRenderer>(); pickup.ApplyModifiedPropertiesWithoutUndo();
                root.transform.position = Vector3.zero; root.transform.localScale = Vector3.one;
                var collider = root.GetComponent<MeshCollider>();
                if (collider != null) { collider.sharedMesh = mesh; collider.convex = true; }
                PrepareNetwork(root, DropPath(kind)); return PrefabUtility.SaveAsPrefabAsset(root, DropPath(kind));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void PrepareNetwork(GameObject root, string path)
        {
            var network = root.GetComponent<NetworkObject>(); network.NetworkBehaviours.Clear();
            var behaviours = root.GetComponents<NetworkBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                network.NetworkBehaviours.Add(behaviours[i]); var data = new SerializedObject(behaviours[i]);
                data.FindProperty("_componentIndexCache").intValue = i; data.FindProperty("_networkObjectCache").objectReferenceValue = network;
                data.FindProperty("_addedNetworkObject").objectReferenceValue = network; data.ApplyModifiedPropertiesWithoutUndo();
            }
            string clean = new string((path + root.name).Trim().ToLowerInvariant().Where(c => char.IsLetterOrDigit(c)).ToArray());
            var net = new SerializedObject(network); net.FindProperty("<AssetPathHash>k__BackingField").ulongValue = clean.GetStableHashU64(); net.ApplyModifiedPropertiesWithoutUndo();
        }
        private static T LoadOrCreate<T>(string path, Func<T> factory) where T : Object
        { var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset == null) { asset = factory(); AssetDatabase.CreateAsset(asset, path); } return asset; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
