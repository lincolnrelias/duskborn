using System;
using Duskborn.Core;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using InventorySystem.Data;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    /// <summary>
    /// Automated tests to validate resource gathering bonuses and drop variance changes.
    /// </summary>
    public static class ResourceGatheringTests
    {
        [MenuItem("Duskborn/Tests/Run Resource Gathering Tests", false, 105)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_DropLootTables_VarianceRework, ref passed, ref total);
            RunTest(Test_CalculateDropCount_ZeroBonus, ref passed, ref total);
            RunTest(Test_CalculateDropCount_WithBonus, ref passed, ref total);
            RunTest(Test_GetResourceBonus_TypeMatching, ref passed, ref total);
            RunTest(Test_GetResourceBonus_EnemyImmunity, ref passed, ref total);
            RunTest(Test_WeaponAssets_GatheringBonusIntegrity, ref passed, ref total);
            RunTest(Test_EntityStats_BuffLayersAndReset, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[ResourceGatheringTests] {passed}/{total} tests passed!</b></color>");
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

        private static void Test_DropLootTables_VarianceRework()
        {
            // 1. Trees: 3 to 4 wood.
            var treeTable = AssetDatabase.LoadAssetAtPath<DropLootTable>("Assets/_Duskborn/ScriptableObjects/Enemies/tree_loot_table.asset");
            if (treeTable == null || treeTable.entries.Length == 0)
                throw new Exception("Failed to load 'tree_loot_table.asset'.");
            if (treeTable.entries[0].minAmount != 3 || treeTable.entries[0].maxAmount != 4)
                throw new Exception($"tree_loot_table must have min=3, max=4. Got: min={treeTable.entries[0].minAmount}, max={treeTable.entries[0].maxAmount}");

            // 2. Rocks: 3 to 4 rocks
            var stoneTable = AssetDatabase.LoadAssetAtPath<DropLootTable>("Assets/_Duskborn/ScriptableObjects/Loot/stone_loot_table.asset");
            if (stoneTable == null || stoneTable.entries.Length == 0)
                throw new Exception("Failed to load 'stone_loot_table.asset'.");
            if (stoneTable.entries[0].minAmount != 3 || stoneTable.entries[0].maxAmount != 4)
                throw new Exception($"stone_loot_table must have min=3, max=4. Got: min={stoneTable.entries[0].minAmount}, max={stoneTable.entries[0].maxAmount}");

            // 3. Iron: 2 to 3 iron.
            var ironTable = AssetDatabase.LoadAssetAtPath<DropLootTable>("Assets/_Duskborn/ScriptableObjects/Loot/iron_loot_table.asset");
            if (ironTable == null || ironTable.entries.Length == 0)
                throw new Exception("Failed to load 'iron_loot_table.asset'.");
            if (ironTable.entries[0].minAmount != 2 || ironTable.entries[0].maxAmount != 3)
                throw new Exception($"iron_loot_table must have min=2, max=3. Got: min={ironTable.entries[0].minAmount}, max={ironTable.entries[0].maxAmount}");

            // 4. Fiber / Shrubs: 2 to 3 fiber.
            var fiberTable = AssetDatabase.LoadAssetAtPath<DropLootTable>("Assets/_Duskborn/ScriptableObjects/Loot/fiber_loot_table.asset");
            if (fiberTable == null || fiberTable.entries.Length == 0)
                throw new Exception("Failed to load 'fiber_loot_table.asset'.");
            if (fiberTable.entries[0].minAmount != 2 || fiberTable.entries[0].maxAmount != 3)
                throw new Exception($"fiber_loot_table must have min=2, max=3. Got: min={fiberTable.entries[0].minAmount}, max={fiberTable.entries[0].maxAmount}");
        }

        private static void Test_CalculateDropCount_ZeroBonus()
        {
            var rng = new SeededRNG(42);
            for (int i = 0; i < 50; i++)
            {
                int count = LootManager.CalculateDropCount(3, 4, 0f, rng);
                if (count < 3 || count > 4)
                    throw new Exception($"With bonus 0, count must be between 3 and 4. Got: {count}");
            }
        }

        private static void Test_CalculateDropCount_WithBonus()
        {
            var rng = new SeededRNG(12345);

            // +100% bonus with min=3, max=4 must double the values (3 -> 6, 4 -> 8).
            for (int i = 0; i < 50; i++)
            {
                int count = LootManager.CalculateDropCount(3, 4, 1.0f, rng);
                if (count != 6 && count != 8)
                    throw new Exception($"With +100% bonus on 3-4, count must be 6 or 8. Got: {count}");
            }

            // +50% bonus on 3 base drops yields 3 + floor(1.5) + (50% chance of +1) = 4 or 5.
            for (int i = 0; i < 50; i++)
            {
                int count = LootManager.CalculateDropCount(3, 3, 0.5f, rng);
                if (count != 4 && count != 5)
                    throw new Exception($"With +50% bonus on 3 base, count must be 4 or 5. Got: {count}");
            }
        }

        private static void Test_GetResourceBonus_TypeMatching()
        {
            var go = new GameObject("TestPlayerStats");
            try
            {
                var stats = go.AddComponent<PlayerStats>();
                stats.MiningResourceBonus = 0.30f;
                stats.WoodcuttingResourceBonus = 0.45f;

                // Tree (TargetType.Tree) -> Woodcutting bonus.
                float treeBonus = LootManager.GetResourceBonus(stats, TargetType.Tree, null);
                if (Mathf.Abs(treeBonus - 0.45f) > 0.001f)
                    throw new Exception($"TargetType.Tree bonus must be 0.45. Got: {treeBonus}");

                // Bush (TargetType.Bush) -> Woodcutting bonus.
                float bushBonus = LootManager.GetResourceBonus(stats, TargetType.Bush, null);
                if (Mathf.Abs(bushBonus - 0.45f) > 0.001f)
                    throw new Exception($"TargetType.Bush bonus must be 0.45. Got: {bushBonus}");

                // Stone (TargetType.Stone) -> Mining bonus.
                float stoneBonus = LootManager.GetResourceBonus(stats, TargetType.Stone, null);
                if (Mathf.Abs(stoneBonus - 0.30f) > 0.001f)
                    throw new Exception($"TargetType.Stone bonus must be 0.30. Got: {stoneBonus}");

                // Ore (TargetType.Ore) -> Mining bonus.
                float oreBonus = LootManager.GetResourceBonus(stats, TargetType.Ore, null);
                if (Mathf.Abs(oreBonus - 0.30f) > 0.001f)
                    throw new Exception($"TargetType.Ore bonus must be 0.30. Got: {oreBonus}");

                // Match by item definition ID (without explicit TargetType).
                var woodAsset = AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>("Assets/_Duskborn/ScriptableObjects/Resources/material_wood.asset");
                if (woodAsset == null)
                    throw new Exception("Failed to load 'material_wood.asset'.");
                float woodItemBonus = LootManager.GetResourceBonus(stats, TargetType.None, woodAsset);
                if (Mathf.Abs(woodItemBonus - 0.45f) > 0.001f)
                    throw new Exception($"material_wood bonus must be 0.45. Got: {woodItemBonus}");

                var ironAsset = AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>("Assets/_Duskborn/ScriptableObjects/Resources/material_iron.asset");
                if (ironAsset == null)
                    throw new Exception("Failed to load 'material_iron.asset'.");
                float ironItemBonus = LootManager.GetResourceBonus(stats, TargetType.None, ironAsset);
                if (Mathf.Abs(ironItemBonus - 0.30f) > 0.001f)
                    throw new Exception($"material_iron bonus must be 0.30. Got: {ironItemBonus}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_GetResourceBonus_EnemyImmunity()
        {
            var go = new GameObject("TestPlayerStatsEnemy");
            try
            {
                var stats = go.AddComponent<PlayerStats>();
                stats.MiningResourceBonus = 0.50f;
                stats.WoodcuttingResourceBonus = 0.50f;

                var ironAsset = AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>("Assets/_Duskborn/ScriptableObjects/Resources/material_iron.asset");
                if (ironAsset == null)
                    throw new Exception("Failed to load 'material_iron.asset'.");

                // A beast or humanoid enemy dropping iron must never receive a mining bonus.
                float enemyDropBonus = LootManager.GetResourceBonus(stats, TargetType.Beast, ironAsset);
                if (enemyDropBonus != 0f)
                    throw new Exception($"Enemy loot (Beast) must have bonus 0. Got: {enemyDropBonus}");

                float humanoidDropBonus = LootManager.GetResourceBonus(stats, TargetType.Humanoid, ironAsset);
                if (humanoidDropBonus != 0f)
                    throw new Exception($"Enemy loot (Humanoid) must have bonus 0. Got: {humanoidDropBonus}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_WeaponAssets_GatheringBonusIntegrity()
        {
            // Stone Axe must have a WoodcuttingResourceBonus.
            var axe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Axe/stone_axe.asset");
            if (axe == null)
                throw new Exception("Could not load 'stone_axe.asset'.");

            bool axeFound = false;
            foreach (var b in axe.Bonuses)
            {
                if (b.Type == StatType.WoodcuttingResourceBonus)
                {
                    axeFound = true;
                    if (b.Value <= 0f)
                        throw new Exception($"Stone Axe WoodcuttingResourceBonus must be positive. Got: {b.Value}");
                }
            }
            if (!axeFound)
                throw new Exception("Stone Axe has no WoodcuttingResourceBonus configured.");

            // Stone Pickaxe must have a MiningResourceBonus.
            var pickaxe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Pickaxe/stone_pickaxe.asset");
            if (pickaxe == null)
                throw new Exception("Could not load 'stone_pickaxe.asset'.");

            bool pickaxeFound = false;
            foreach (var b in pickaxe.Bonuses)
            {
                if (b.Type == StatType.MiningResourceBonus)
                {
                    pickaxeFound = true;
                    if (b.Value <= 0f)
                        throw new Exception($"Stone Pickaxe MiningResourceBonus must be positive. Got: {b.Value}");
                }
            }
            if (!pickaxeFound)
                throw new Exception("Stone Pickaxe has no MiningResourceBonus configured.");
        }

        private static void Test_EntityStats_BuffLayersAndReset()
        {
            var stats = new EntityStats();

            // Configure equipment bonuses.
            stats.MiningResourceBonus = 0.20f;
            stats.WoodcuttingResourceBonus = 0.15f;

            // Additive buff bonuses.
            stats.MiningResourceBuffAdditive = 0.10f;
            stats.WoodcuttingResourceBuffAdditive = 0.05f;

            // Multiplicative buff bonuses.
            stats.MiningResourceBuffFactor = 1.20f;
            stats.WoodcuttingResourceBuffFactor = 1.50f;

            // Effective mining calculation: (0.20 + 0.10) * 1.20 = 0.36.
            float expectedMining = (0.20f + 0.10f) * 1.20f;
            if (Mathf.Abs(stats.EffectiveMiningResourceBonus - expectedMining) > 0.0001f)
                throw new Exception($"Incorrect EffectiveMiningResourceBonus. Expected: {expectedMining}, Actual: {stats.EffectiveMiningResourceBonus}");

            // Effective woodcutting calculation: (0.15 + 0.05) * 1.50 = 0.30.
            float expectedWood = (0.15f + 0.05f) * 1.50f;
            if (Mathf.Abs(stats.EffectiveWoodcuttingResourceBonus - expectedWood) > 0.0001f)
                throw new Exception($"Incorrect EffectiveWoodcuttingResourceBonus. Expected: {expectedWood}, Actual: {stats.EffectiveWoodcuttingResourceBonus}");

            // Reset
            stats.ResetMultipliers();
            if (stats.EffectiveMiningResourceBonus != 0f || stats.EffectiveWoodcuttingResourceBonus != 0f)
                throw new Exception("ResetMultipliers did not reset resource bonuses to zero.");
        }
    }
}
