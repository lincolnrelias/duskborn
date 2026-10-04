using System;
using System.Linq;
using System.Reflection;
using Duskborn.Effects;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.World;
using FishNet.Managing.Object;
using FishNet.Object;
using InventorySystem.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Duskborn.Editor
{
    public static class ElementalCrystalTests
    {
        [MenuItem("Duskborn/Tests/Run Elemental Crystal Tests")]
        public static void RunAllTests()
        {
            int triangleCount = 0;
            foreach (RuneKind kind in Enum.GetValues(typeof(RuneKind)))
            {
                if (kind == RuneKind.None) continue;
                var item = Resources.Load<MaterialDefinition>("ElementalCrystals/Items/" + ElementalCrystalCatalog.Id(kind));
                Check(item != null && item.Icon != null && item.dropPrefab != null, "Build-safe crystal definition/icon/drop missing: " + kind);
                var node = AssetDatabase.LoadAssetAtPath<GameObject>(ElementalCrystalBuilder.NodePath(kind));
                Check(node != null && node.GetComponent<NetworkObject>() != null, "Missing network node: " + kind);
                var resource = node.GetComponent<ResourceNode>();
                Check(resource != null && (resource.Types & TargetType.MiningNode) != 0, "Must use existing pickaxe mining targeting: " + kind);
                var loot = node.GetComponent<LootDropper>().LootTable;
                Check(loot != null && loot.entries.Length == 1 && loot.entries[0].itemDefinition == item &&
                    loot.entries[0].baseChance == 1 && loot.entries[0].minAmount == 3 && loot.entries[0].maxAmount == 5 &&
                    loot.goldMin == 0 && loot.goldMax == 0, "Guaranteed correct crystal loot: " + kind);
                Check(node.GetComponent<ElementalCrystalNodeVisual>().Element == kind, "Wrong node particles: " + kind);
                foreach (var renderer in node.GetComponentsInChildren<MeshRenderer>())
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null && (material.name.Contains("_Crystal") || material.name.Contains("_Glow")))
                            Check(material.IsKeywordEnabled("_EMISSION") && material.GetColor("_EmissionColor").maxColorComponent > .2f, "Imported emissive crystal materials: " + kind);
                int triangles = node.GetComponentsInChildren<MeshFilter>().Sum(filter => filter.sharedMesh != null ? filter.sharedMesh.triangles.Length / 3 : 0);
                Check(triangles >= 200 && triangles <= 1500, "Authored mesh budget: " + kind); triangleCount += triangles;
                foreach (string registry in new[] { "Assets/DefaultPrefabObjects.asset", "Assets/_Duskborn/Network/DefaultPrefabObjects.asset" })
                {
                    var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(registry);
                    var entries = Enumerable.Range(0, collection.GetObjectCount()).Select(index => collection.GetObject(true, index));
                    Check(entries.Contains(node.GetComponent<NetworkObject>()) && entries.Contains(item.dropPrefab.GetComponent<NetworkObject>()), "Node/drop not registered for remote clients: " + kind);
                }
                for (int level = 1; level <= 3; level++) CheckCrafting(kind, level, item);
                CheckParticles(kind, node);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:WorldPropsConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<WorldPropsConfig>(AssetDatabase.GUIDToAssetPath(guid));
                for (int kind = 1; kind <= 8; kind++)
                {
                    var node = AssetDatabase.LoadAssetAtPath<GameObject>(ElementalCrystalBuilder.NodePath((RuneKind)kind));
                    Check(config.extraProps.Count(prop => prop != null && prop.prefab == node) == 1, "Exactly one spawn definition per element: " + config.name);
                    var prop = config.extraProps.Single(p => p != null && p.prefab == node);
                    Check(prop.minPerChunk == 0 && prop.maxPerChunk == 1 && !prop.clusterSettings.enableClustering && prop.maxRadialDistance == 0, "World-wide sparse deposit settings");
                }
            }
            Check(ElementalCrystalCatalog.Id(RuneKind.Venom) == "material_nature_crystal" && ElementalCrystalCatalog.Id(RuneKind.Hex) == "material_dark_crystal", "Sensible element naming");
            CheckCachedWorldPlacement();
            Debug.Log("[ElementalCrystalTests] Eight mining chains, 24 crystal-gated recipes, native particles, seeded placement and all world/network registrations passed. Model triangles: " + triangleCount);
        }
        private static void CheckCrafting(RuneKind kind, int level, MaterialDefinition crystal)
        {
            var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_" + RuneCatalog.Id(kind, level));
            Check(recipe.Ingredients.Count(entry => entry.material == crystal) == 1 && recipe.Ingredients.Single(entry => entry.material == crystal).amount == 3 * level, "Crystal cost must scale 3/6/9");
            var go = new GameObject("Crystal cost transaction fixture");
            try
            {
                var wallet = go.AddComponent<ResourceInventory>();
                foreach (var ingredient in recipe.Ingredients) if (ingredient.material != crystal) wallet.Add(ingredient.material.Id, ingredient.amount);
                Check(!recipe.CanCraft(wallet), "Runestone must require mined crystals");
                string wrong = ElementalCrystalCatalog.Id(kind == RuneKind.Flame ? RuneKind.Frost : RuneKind.Flame);
                wallet.Add(wrong, 100); Check(!recipe.CanCraft(wallet), "Another element cannot pay the crystal requirement");
                wallet.Add(crystal.Id, 3 * level - 1); Check(!recipe.TrySpendIngredients(wallet) && wallet.GetCount(crystal.Id) == 3 * level - 1, "Shortfall must leave wallet unchanged");
                wallet.Add(crystal.Id, 1); Check(recipe.CanCraft(wallet) && recipe.TrySpendIngredients(wallet), "Collected matching crystals unlock crafting");
                Check(wallet.GetCount(crystal.Id) == 0 && wallet.GetCount(wrong) == 100, "Consume matching crystals exactly");
            }
            finally { Object.DestroyImmediate(go); }
        }
        private static void CheckParticles(RuneKind kind, GameObject prefab)
        {
            var go = Object.Instantiate(prefab);
            try
            {
                var visual = go.GetComponent<ElementalCrystalNodeVisual>(); visual.Initialize();
                var particles = go.GetComponentInChildren<ParticleSystem>(); Check(particles != null, "Bright node particles must initialize");
                particles.Simulate(.6f, true, true); Check(particles.particleCount > 0 && particles.main.maxParticles <= 80, "Bounded native particle emission: " + kind);
                var material = particles.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Check(material != null && material.shader != null && material.shader.name == "Duskborn/ElementalCrystalAura", "Build-safe bright additive shader");
                typeof(ElementalCrystalNodeVisual).GetMethod("OnHealth", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, new object[] { 0f, 100f });
                Check(particles.particleCount == 0 && !visual.enabled, "Depleted node stops particles on clients");
            }
            finally { Object.DestroyImmediate(go); }
        }
        private static void CheckCachedWorldPlacement()
        {
            var terrain = ScriptableObject.CreateInstance<LowPolyTerrainConfig>();
            var config = ScriptableObject.CreateInstance<WorldPropsConfig>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Crystal placement terrain fixture";
            ground.transform.position = new Vector3(0, 1.5f, 0); ground.transform.localScale = new Vector3(180, 1, 180);
            var host = new GameObject("Crystal cached world fixture");
            try
            {
                terrain.chunksX = terrain.chunksZ = 4; terrain.chunkSize = 40; terrain.cellSize = 1;
                terrain.waterLevel = 0; terrain.boundaryType = LowPolyTerrainConfig.MapBoundaryType.None;
                config.extraProps = Enumerable.Range(1, 8).Select(index => Resources.Load<PropDefinition>("ElementalCrystals/Props/Prop_" + ElementalCrystalCatalog.Element((RuneKind)index) + "_Crystal")).ToArray();
                var placer = host.AddComponent<WorldPropsPlacer>(); Physics.SyncTransforms();
                int first = placer.EnsureElementalDeposits(terrain, config, 80913);
                Check(first > 0, "Cached terrain receives new mineable deposits");
                var nodes = placer.PropsContainer.GetComponentsInChildren<ElementalCrystalNodeVisual>(true);
                Check(nodes.Select(node => node.Element).Distinct().Count() == 8, "All elements distributed on viable seeded terrain");
                foreach (var node in nodes) Check(new Vector2(node.transform.position.x, node.transform.position.z).magnitude >= 18, "Protect sanctuary");
                var positions = nodes.Select(node => node.transform.position).ToArray();
                for (int i = 0; i < positions.Length; i++) for (int j = i + 1; j < positions.Length; j++)
                    Check(Vector3.Distance(positions[i], positions[j]) >= 3.2f, "Deposit exclusion spacing");
                Check(placer.EnsureElementalDeposits(terrain, config, 80913) == 0 && placer.PropsCount == first, "Repeated cached-world checks do not duplicate nodes");
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(ground); Object.DestroyImmediate(config); Object.DestroyImmediate(terrain); }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
