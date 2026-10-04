using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using Duskborn.UI;
using InventorySystem.Core;
using InventorySystem.Data;

namespace Duskborn.Editor
{
    /// <summary>
    /// Automated validation tests for the crafting system, workbench, and movable interface.
    /// </summary>
    public static class CraftingTests
    {
        [MenuItem("Duskborn/Tests/Run Crafting Tests", false, 104)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_StoneAxe_RecipeIntegrity, ref passed, ref total);
            RunTest(Test_StonePickaxe_RecipeIntegrity, ref passed, ref total);
            RunTest(Test_Recipe_CanCraft_Validation, ref passed, ref total);
            RunTest(Test_Recipe_SpendIngredients_Execution, ref passed, ref total);
            RunTest(Test_Recipe_CreateOutputItem, ref passed, ref total);
            RunTest(Test_Workbench_ProximityCalculation, ref passed, ref total);
            RunTest(Test_Workbench_PrefabModelIntegrity, ref passed, ref total);
            RunTest(Test_ArcaneTable_PrefabModelIntegrity, ref passed, ref total);
            RunTest(Test_DraggablePanel_BindingAndCanvasResolution, ref passed, ref total);
            RunTest(Test_DraggablePanel_ScreenClampingMath, ref passed, ref total);
            RunTest(Test_AllFourCraftingStations_Integrity, ref passed, ref total);
            RunTest(Test_MultiTierProgression_Recipes, ref passed, ref total);
            RunTest(Test_ConsumableItems_Functionality, ref passed, ref total);
            RunTest(Test_EquipmentTradeoffs_Calculations, ref passed, ref total);
            RunTest(Test_RecipeDiscoveryTracker_Logic, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[CraftingTests] {passed}/{total} tests passed!</b></color>");
        }

        private static void RunTest(Action testMethod, ref int passed, ref int total)
        {
            total++;
            try
            {
                testMethod();
                passed++;
                Debug.Log($"[PASS] {testMethod.Method.Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FAIL] {testMethod.Method.Name}: {ex.Message}");
            }
        }

        private static void Test_StoneAxe_RecipeIntegrity()
        {
            var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_StoneAxe");
            if (recipe == null)
                throw new Exception("Could not load 'Crafting/Recipe_StoneAxe' through Resources.");

            if (recipe.OutputItem == null)
                throw new Exception("Recipe_StoneAxe has no OutputItem configured.");

            if (recipe.OutputItem.Id != "stone_axe")
                throw new Exception($"Expected OutputItem 'stone_axe', but found '{recipe.OutputItem.Id}'.");

            if (recipe.Ingredients == null || recipe.Ingredients.Count != 2)
                throw new Exception("Recipe_StoneAxe must contain exactly 2 ingredients (Wood and Stone).");

            var woodIng = FindIngredient(recipe, "material_wood");
            var stoneIng = FindIngredient(recipe, "material_stone");

            if (woodIng.amount != 5 || stoneIng.amount != 5)
                throw new Exception($"Invalid ingredient quantities: Wood={woodIng.amount}, Stone={stoneIng.amount}");

            var weaponDef = recipe.OutputItem as WeaponDefinition;
            if (weaponDef == null)
                throw new Exception("stone_axe must be a WeaponDefinition.");

            float mult = TypeDamageModifier.GetBestMultiplier(weaponDef.TypeModifiers, TargetType.Tree);
            if (Mathf.Abs(mult - 4.0f) > 0.05f)
                throw new Exception($"stone_axe should have a damage multiplier of 4.0x (base + 300%) against Tree, but has {mult}x.");
        }

        private static void Test_StonePickaxe_RecipeIntegrity()
        {
            var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_StonePickaxe");
            if (recipe == null)
                throw new Exception("Could not load 'Crafting/Recipe_StonePickaxe' through Resources.");

            if (recipe.OutputItem == null)
                throw new Exception("Recipe_StonePickaxe has no OutputItem configured.");

            if (recipe.OutputItem.Id != "stone_pickaxe")
                throw new Exception($"Expected OutputItem 'stone_pickaxe', but found '{recipe.OutputItem.Id}'.");

            if (recipe.Ingredients == null || recipe.Ingredients.Count != 2)
                throw new Exception("Recipe_StonePickaxe must contain exactly 2 ingredients (Wood and Stone).");

            var woodIng = FindIngredient(recipe, "material_wood");
            var stoneIng = FindIngredient(recipe, "material_stone");

            if (woodIng.amount != 5 || stoneIng.amount != 5)
                throw new Exception($"Invalid ingredient quantities: Wood={woodIng.amount}, Stone={stoneIng.amount}");

            var weaponDef = recipe.OutputItem as WeaponDefinition;
            if (weaponDef == null)
                throw new Exception("stone_pickaxe must be a WeaponDefinition.");

            float mult = TypeDamageModifier.GetBestMultiplier(weaponDef.TypeModifiers, TargetType.MiningNode);
            if (Mathf.Abs(mult - 4.0f) > 0.05f)
                throw new Exception($"stone_pickaxe should have a damage multiplier of 4.0x (base + 300%) against MiningNode, but has {mult}x.");
        }

        private static void Test_Recipe_CanCraft_Validation()
        {
            var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            var woodMat = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(woodMat, "id", "wood");
            var stoneMat = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(stoneMat, "id", "stone");

            var ingList = new System.Collections.Generic.List<CraftingIngredient>
            {
                new CraftingIngredient { material = woodMat, amount = 10 },
                new CraftingIngredient { material = stoneMat, amount = 5 }
            };
            SetPrivateField(recipe, "ingredients", ingList);

            var go = new GameObject("Test_ResInv_Holder");
            var resInv = go.AddComponent<ResourceInventory>();
            resInv.Add("wood", 9);
            resInv.Add("stone", 10);

            if (recipe.CanCraft(resInv))
                throw new Exception("CanCraft should return false when wood is insufficient (9/10).");

            resInv.Add("wood", 1);
            if (!recipe.CanCraft(resInv))
                throw new Exception("CanCraft should return true with 10 wood and 10 stone.");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(woodMat);
            UnityEngine.Object.DestroyImmediate(stoneMat);
        }

        private static void Test_Recipe_SpendIngredients_Execution()
        {
            var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            var woodMat = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(woodMat, "id", "wood");
            var stoneMat = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(stoneMat, "id", "stone");

            var ingList = new System.Collections.Generic.List<CraftingIngredient>
            {
                new CraftingIngredient { material = woodMat, amount = 5 },
                new CraftingIngredient { material = stoneMat, amount = 3 }
            };
            SetPrivateField(recipe, "ingredients", ingList);

            var go = new GameObject("Test_ResInv_Holder_2");
            var resInv = go.AddComponent<ResourceInventory>();
            resInv.Add("wood", 8);
            resInv.Add("stone", 2);

            bool success = recipe.TrySpendIngredients(resInv);
            if (success)
                throw new Exception("TrySpendIngredients should not succeed with insufficient stone.");
            if (resInv.GetCount("wood") != 8)
                throw new Exception("TrySpendIngredients should not deduct resources on failure.");

            resInv.Add("stone", 5);
            success = recipe.TrySpendIngredients(resInv);
            if (!success)
                throw new Exception("TrySpendIngredients failed with sufficient resources.");

            if (resInv.GetCount("wood") != 3)
                throw new Exception($"Expected wood balance: 3, got: {resInv.GetCount("wood")}");
            if (resInv.GetCount("stone") != 4)
                throw new Exception($"Expected stone balance: 4, got: {resInv.GetCount("stone")}");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(woodMat);
            UnityEngine.Object.DestroyImmediate(stoneMat);
        }

        private static void Test_Recipe_CreateOutputItem()
        {
            var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_StoneAxe");
            if (recipe == null)
                throw new Exception("Could not load Recipe_StoneAxe.");

            IInventoryItem item = recipe.CreateOutputItem();
            if (item == null)
                throw new Exception("CreateOutputItem returned null.");

            if (item.Id != "stone_axe")
                throw new Exception($"Incorrect created item ID: {item.Id}");
        }

        private static void Test_Workbench_ProximityCalculation()
        {
            var wbGo = new GameObject("Workbench_Test");
            wbGo.transform.position = new Vector3(10f, 0f, 10f);
            var wb = wbGo.AddComponent<Workbench>();

            Vector3 nearPos = new Vector3(12f, 0f, 10f);
            if (!wb.IsInRange(nearPos, 3.5f))
                throw new Exception("Workbench.IsInRange should return true at a distance of 2.0m.");

            Vector3 farPos = new Vector3(15f, 0f, 10f);
            if (wb.IsInRange(farPos, 3.5f))
                throw new Exception("Workbench.IsInRange should return false at a distance of 5.0m.");

            UnityEngine.Object.DestroyImmediate(wbGo);
        }

        private static void Test_Workbench_PrefabModelIntegrity()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/World/Workbench.prefab");
            if (prefab == null)
                throw new Exception("Could not load 'Assets/_Duskborn/Prefabs/World/Workbench.prefab'.");

            var mf = prefab.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                throw new Exception("Workbench.prefab has no valid MeshFilter or Mesh.");

            if (mf.sharedMesh.name != "Workbench")
                throw new Exception($"Expected mesh 'Workbench', but found '{mf.sharedMesh.name}'.");

            var mr = prefab.GetComponent<MeshRenderer>();
            if (mr == null || mr.sharedMaterials == null || mr.sharedMaterials.Length < 3)
                throw new Exception("Workbench.prefab MeshRenderer must have 3 materials (Wood, Iron, Stone).");

            var col = prefab.GetComponent<BoxCollider>();
            if (col == null)
                throw new Exception("Workbench.prefab must have a BoxCollider.");

            if (col.size.x < 1.0f || col.size.y < 0.8f || col.size.z < 0.8f)
                throw new Exception($"Invalid BoxCollider dimensions: {col.size}");

            var wb = prefab.GetComponent<Workbench>();
            if (wb == null)
                throw new Exception("Workbench.prefab must have the Workbench component.");
        }

        private static void Test_ArcaneTable_PrefabModelIntegrity()
        {
            string[] paths = {
                "Assets/_Duskborn/Prefabs/World/Station_ArcaneTable.prefab",
                "Assets/_Duskborn/Resources/Stations/Station_ArcaneTable.prefab"
            };

            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new Exception($"Could not load '{path}'.");

                var mf = prefab.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name != "Moonwell_Shrine")
                    throw new Exception($"{path} still contains a legacy MeshFilter at the root.");

                var visual = prefab.transform.Find("Visual");
                if (visual == null)
                    throw new Exception($"{path} must have a 'Visual' child containing the Moonwell model.");

                var animator = visual.GetComponent<Animator>();
                if (animator == null)
                    throw new Exception($"{path}/Visual must have an Animator component.");

                if (animator.runtimeAnimatorController == null)
                    throw new Exception($"{path}/Visual Animator must have a RuntimeAnimatorController configured.");

                if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
                    throw new Exception($"{path}/Visual Animator cullingMode must be AlwaysAnimate to prevent animation freezing.");

                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length < 4)
                    throw new Exception($"{path}/Visual must have at least 4 renderers (Table, Crystals, Book, Page).");

                var skinnedPage = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skinnedPage == null || skinnedPage.Length == 0)
                    throw new Exception($"{path}/Visual must contain a SkinnedMeshRenderer for the animated book page.");

                if (!skinnedPage[0].updateWhenOffscreen)
                    throw new Exception($"{path} SkinnedMeshRenderer must have updateWhenOffscreen = true to prevent freezing from frustum culling.");

                var col = prefab.GetComponent<BoxCollider>();
                if (col == null)
                    throw new Exception($"{path} must have a BoxCollider at the root.");

                if (col.size.x < 1.5f || col.size.z < 1.5f)
                    throw new Exception($"{path} BoxCollider must have dimensions compatible with Moonwell (>= 1.5m): {col.size}");

                var wb = prefab.GetComponent<Workbench>();
                if (wb == null)
                    throw new Exception($"{path} must have the Workbench component.");

                if (wb.StationType != CraftingStationType.ArcaneTable)
                    throw new Exception($"{path} Workbench StationType must be ArcaneTable, got: {wb.StationType}");
            }

            // Also validate that BuildingWorld.CreateVisual correctly clones SkinnedMeshRenderer and Animator.
            var buildDef = Resources.Load<Duskborn.Gameplay.Building.BuildableDefinition>("Building/Build_arcane_table");
            if (buildDef != null)
            {
                var visualInstance = Duskborn.Gameplay.Building.BuildingWorld.CreateVisual(buildDef);
                try
                {
                    var anim = visualInstance.GetComponentInChildren<Animator>();
                    if (anim == null)
                        throw new Exception("BuildingWorld.CreateVisual did not copy the Arcane Table Animator.");

                    if (anim.runtimeAnimatorController == null)
                        throw new Exception("BuildingWorld.CreateVisual Animator has no associated controller.");

                    var smr = visualInstance.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr == null)
                        throw new Exception("BuildingWorld.CreateVisual did not copy the Arcane Table SkinnedMeshRenderer.");

                    if (!smr.updateWhenOffscreen)
                        throw new Exception("BuildingWorld.CreateVisual SkinnedMeshRenderer must have updateWhenOffscreen = true.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(visualInstance);
                }
            }
        }

        private static void Test_DraggablePanel_BindingAndCanvasResolution()
        {
            // Create a GameObject with a Canvas.
            var canvasGO = new GameObject("Test_Canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasGO.GetComponent<Canvas>();

            // Create a child panel simulating the Crafting / Inventory window.
            var panelGO = new GameObject("Test_Panel", typeof(RectTransform), typeof(CanvasRenderer));
            var panelRect = panelGO.GetComponent<RectTransform>();

            // Add DraggablePanel BEFORE setting the parent (reproduces dynamic instantiation).
            var drag = panelGO.AddComponent<DraggablePanel>();

            // Check automatic TargetPanel binding.
            if (drag.TargetPanel != panelRect)
                throw new Exception("DraggablePanel.TargetPanel should return its own RectTransform when not explicitly assigned.");

            // Now connect to the Canvas.
            panelGO.transform.SetParent(canvasGO.transform, false);

            // Check lazy Canvas resolution.
            var resolvedCanvas = drag.EnsureCanvas();
            if (resolvedCanvas != canvas)
                throw new Exception("DraggablePanel.EnsureCanvas() should find the parent Canvas after SetParent.");

            UnityEngine.Object.DestroyImmediate(panelGO);
            UnityEngine.Object.DestroyImmediate(canvasGO);
        }

        private static void Test_DraggablePanel_ScreenClampingMath()
        {
            var parentGO = new GameObject("ParentCanvas", typeof(RectTransform));
            var parentRect = parentGO.GetComponent<RectTransform>();
            parentRect.sizeDelta = new Vector2(1920, 1080);
            parentRect.pivot = new Vector2(0.5f, 0.5f);

            var childGO = new GameObject("Window", typeof(RectTransform));
            childGO.transform.SetParent(parentGO.transform, false);
            var childRect = childGO.GetComponent<RectTransform>();
            childRect.sizeDelta = new Vector2(400, 500);
            childRect.pivot = new Vector2(0.5f, 0.5f);
            childRect.anchorMin = new Vector2(0.5f, 0.5f);
            childRect.anchorMax = new Vector2(0.5f, 0.5f);

            // Try moving the window far beyond the right edge (e.g. x = 2000).
            Vector2 outOfBoundsPos = new Vector2(2000, 0);
            Vector2 clamped = DraggablePanel.ClampWithinParent(outOfBoundsPos, childRect, parentRect);

            // Maximum right limit for a centered pivot (1920/2 - 400/2 = 960 - 200 = 760).
            float expectedMaxX = (1920f * 0.5f) - (400f * 0.5f);
            if (Mathf.Abs(clamped.x - expectedMaxX) > 1.0f)
                throw new Exception($"ClampWithinParent failed at the right boundary: expected ~{expectedMaxX}, got {clamped.x}");

            // Try moving the window far beyond the bottom edge (e.g. y = -2000).
            Vector2 outOfBoundsBottom = new Vector2(0, -2000);
            Vector2 clampedBottom = DraggablePanel.ClampWithinParent(outOfBoundsBottom, childRect, parentRect);

            float expectedMinY = (-1080f * 0.5f) + (500f * 0.5f);
            if (Mathf.Abs(clampedBottom.y - expectedMinY) > 1.0f)
                throw new Exception($"ClampWithinParent failed at the bottom boundary: expected ~{expectedMinY}, got {clampedBottom.y}");

            UnityEngine.Object.DestroyImmediate(childGO);
            UnityEngine.Object.DestroyImmediate(parentGO);
        }

        private static void Test_AllFourCraftingStations_Integrity()
        {
            string[] prefabs = { "Workbench", "Station_Forge", "Station_Cauldron", "Station_ArcaneTable" };
            CraftingStationType[] expectedTypes = { CraftingStationType.Workbench, CraftingStationType.Forge, CraftingStationType.Cauldron, CraftingStationType.ArcaneTable };

            for (int i = 0; i < prefabs.Length; i++)
            {
                var prefab = Resources.Load<GameObject>($"Stations/{prefabs[i]}");
                if (prefab == null)
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Duskborn/Prefabs/World/{prefabs[i]}.prefab");

                if (prefab == null)
                    throw new Exception($"Station prefab '{prefabs[i]}' was not found in Resources or Prefabs/World.");

                var wb = prefab.GetComponent<Workbench>();
                if (wb == null)
                    throw new Exception($"Prefab '{prefabs[i]}' has no Workbench component.");

                if (wb.StationType != expectedTypes[i])
                    throw new Exception($"Station '{prefabs[i]}' expected StationType {expectedTypes[i]}, but has {wb.StationType}.");

                var networkObject = prefab.GetComponent<FishNet.Object.NetworkObject>();
                var registry = AssetDatabase.LoadAssetAtPath<FishNet.Managing.Object.SinglePrefabObjects>(
                    "Assets/_Duskborn/Network/DefaultPrefabObjects.asset");
                if (networkObject == null || registry == null)
                    throw new Exception($"Station '{prefabs[i]}' must have a NetworkObject and gameplay registry.");
                bool registered = false;
                for (int index = 0; index < registry.GetObjectCount(); index++)
                    if (registry.GetObject(true, index) == networkObject) registered = true;
                if (!registered)
                    throw new Exception($"Station '{prefabs[i]}' is missing from the gameplay NetworkManager's spawnable prefabs.");
            }
        }

        private static void Test_MultiTierProgression_Recipes()
        {
            var recipes = Resources.LoadAll<CraftingRecipe>("Crafting");
            if (recipes == null || recipes.Length < 25)
                throw new Exception($"Expected at least 25 recipes in the progression system, but found {recipes?.Length ?? 0}.");

            bool hasT1 = false, hasT2 = false, hasT3 = false, hasT4 = false;
            bool hasWorkbench = false, hasForge = false, hasCauldron = false, hasArcaneTable = false;

            foreach (var r in recipes)
            {
                if (r == null) continue;
                if (r.Tier == CraftingTier.Primitive) hasT1 = true;
                if (r.Tier == CraftingTier.Iron) hasT2 = true;
                if (r.Tier == CraftingTier.Reinforced) hasT3 = true;
                if (r.Tier == CraftingTier.Thornheart) hasT4 = true;

                if (r.RequiredStation == CraftingStationType.Workbench) hasWorkbench = true;
                if (r.RequiredStation == CraftingStationType.Forge) hasForge = true;
                if (r.RequiredStation == CraftingStationType.Cauldron) hasCauldron = true;
                if (r.RequiredStation == CraftingStationType.ArcaneTable) hasArcaneTable = true;
            }

            if (!hasT1 || !hasT2 || !hasT3 || !hasT4)
                throw new Exception($"Recipes are missing from one of the 4 progression tiers (T1={hasT1}, T2={hasT2}, T3={hasT3}, T4={hasT4}).");

            if (!hasWorkbench || !hasForge || !hasCauldron || !hasArcaneTable)
                throw new Exception($"Recipes are missing from one of the 4 crafting stations (Workbench={hasWorkbench}, Forge={hasForge}, Cauldron={hasCauldron}, ArcaneTable={hasArcaneTable}).");
        }

        private static void Test_ConsumableItems_Functionality()
        {
            var consumableItem = new ConsumableItem(
                "test_tonic", "Test Tonic", "Restores health", "icon_tonic",
                ConsumableEffectType.InstantHeal, 50f, 0f, 5);

            if (consumableItem.EffectType != ConsumableEffectType.InstantHeal)
                throw new Exception("ConsumableItem did not retain the correct EffectType.");

            if (Mathf.Abs(consumableItem.EffectValue - 50f) > 0.01f)
                throw new Exception("ConsumableItem did not retain the correct EffectValue.");

            if (consumableItem.StackSize != 5)
                throw new Exception("ConsumableItem did not retain the correct StackSize.");
        }

        private static void Test_EquipmentTradeoffs_Calculations()
        {
            var stats = new EntityStats
            {
                maxHP = 100f,
                damage = 10f,
                moveSpeed = 5f
            };

            // Simulate positive and negative bonuses (trade-off).
            stats.DamageBuffAdditive = 5f;
            stats.MoveSpeedMultiplier = 0.85f; // -15% speed penalty.

            if (stats.Damage < 14f)
                throw new Exception("Incorrect EntityStats damage calculation.");

            // Base 5.0 * 0.85 = 4.25f
            if (Mathf.Abs(stats.MoveSpeed - 4.25f) > 0.05f)
                throw new Exception($"MoveSpeed calculation with trade-off failed: expected ~4.25, got {stats.MoveSpeed}");
        }

        private static void Test_RecipeDiscoveryTracker_Logic()
        {
            var go = new GameObject("Tracker_Test");
            var tracker = go.AddComponent<RecipeDiscoveryTracker>();

            var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_SmeltIronBar");
            if (recipe != null)
            {
                // Before discovery.
                bool initial = tracker.IsDiscovered(recipe);
                tracker.ForceDiscover(recipe);
                bool after = tracker.IsDiscovered(recipe);

                if (!after)
                    throw new Exception("RecipeDiscoveryTracker did not record the recipe discovery.");
            }

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static CraftingIngredient FindIngredient(CraftingRecipe recipe, string materialId)
        {
            foreach (var ing in recipe.Ingredients)
            {
                if (ing.material != null && ing.material.Id == materialId)
                    return ing;
            }
            throw new Exception($"Ingredient with ID '{materialId}' not found in the recipe.");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().FullName, fieldName);
        }
    }
}
