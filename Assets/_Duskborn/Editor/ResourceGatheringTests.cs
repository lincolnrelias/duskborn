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
    /// Testes automatizados para validação do bônus de coleta de recursos e rework de variância de drops.
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

            Debug.Log($"<color=#55FF55><b>[ResourceGatheringTests] {passed}/{total} testes passaram com sucesso!</b></color>");
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
            // 1. Árvores: 3 a 4 madeiras
            var treeTable = AssetDatabase.LoadAssetAtPath<DropLootTable>("Assets/_Duskborn/ScriptableObjects/Enemies/tree_loot_table.asset");
            if (treeTable == null || treeTable.entries.Length == 0)
                throw new Exception("Falha ao carregar 'tree_loot_table.asset'.");
            if (treeTable.entries[0].minAmount != 3 || treeTable.entries[0].maxAmount != 4)
                throw new Exception($"tree_loot_table deve ter min=3, max=4. Obtido: min={treeTable.entries[0].minAmount}, max={treeTable.entries[0].maxAmount}");

            // 2. Pedras: 3 a 4 pedras
            var stoneTable = AssetDatabase.LoadAssetAtPath<DropLootTable>("Assets/_Duskborn/ScriptableObjects/Loot/stone_loot_table.asset");
            if (stoneTable == null || stoneTable.entries.Length == 0)
                throw new Exception("Falha ao carregar 'stone_loot_table.asset'.");
            if (stoneTable.entries[0].minAmount != 3 || stoneTable.entries[0].maxAmount != 4)
                throw new Exception($"stone_loot_table deve ter min=3, max=4. Obtido: min={stoneTable.entries[0].minAmount}, max={stoneTable.entries[0].maxAmount}");

            // 3. Ferro: 2 a 3 ferros
            var ironTable = AssetDatabase.LoadAssetAtPath<DropLootTable>("Assets/_Duskborn/ScriptableObjects/Loot/iron_loot_table.asset");
            if (ironTable == null || ironTable.entries.Length == 0)
                throw new Exception("Falha ao carregar 'iron_loot_table.asset'.");
            if (ironTable.entries[0].minAmount != 2 || ironTable.entries[0].maxAmount != 3)
                throw new Exception($"iron_loot_table deve ter min=2, max=3. Obtido: min={ironTable.entries[0].minAmount}, max={ironTable.entries[0].maxAmount}");

            // 4. Fibra / Arbustos: 2 a 3 fibras
            var fiberTable = AssetDatabase.LoadAssetAtPath<DropLootTable>("Assets/_Duskborn/ScriptableObjects/Loot/fiber_loot_table.asset");
            if (fiberTable == null || fiberTable.entries.Length == 0)
                throw new Exception("Falha ao carregar 'fiber_loot_table.asset'.");
            if (fiberTable.entries[0].minAmount != 2 || fiberTable.entries[0].maxAmount != 3)
                throw new Exception($"fiber_loot_table deve ter min=2, max=3. Obtido: min={fiberTable.entries[0].minAmount}, max={fiberTable.entries[0].maxAmount}");
        }

        private static void Test_CalculateDropCount_ZeroBonus()
        {
            var rng = new SeededRNG(42);
            for (int i = 0; i < 50; i++)
            {
                int count = LootManager.CalculateDropCount(3, 4, 0f, rng);
                if (count < 3 || count > 4)
                    throw new Exception($"Com bonus 0, count deve estar entre 3 e 4. Obtido: {count}");
            }
        }

        private static void Test_CalculateDropCount_WithBonus()
        {
            var rng = new SeededRNG(12345);

            // +100% de bônus em min=3, max=4 deve dobrar os valores (3 -> 6, 4 -> 8)
            for (int i = 0; i < 50; i++)
            {
                int count = LootManager.CalculateDropCount(3, 4, 1.0f, rng);
                if (count != 6 && count != 8)
                    throw new Exception($"Com +100% bonus em 3-4, count deve ser 6 ou 8. Obtido: {count}");
            }

            // +50% de bônus em 3 drops base resulta em 3 + floor(1.5) + (50% chance de +1) = 4 ou 5
            for (int i = 0; i < 50; i++)
            {
                int count = LootManager.CalculateDropCount(3, 3, 0.5f, rng);
                if (count != 4 && count != 5)
                    throw new Exception($"Com +50% bonus em 3 base, count deve ser 4 ou 5. Obtido: {count}");
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

                // Árvore (TargetType.Tree) -> Bônus de corte de madeira
                float treeBonus = LootManager.GetResourceBonus(stats, TargetType.Tree, null);
                if (Mathf.Abs(treeBonus - 0.45f) > 0.001f)
                    throw new Exception($"Bônus de TargetType.Tree deve ser 0.45. Obtido: {treeBonus}");

                // Arbusto (TargetType.Bush) -> Bônus de corte de madeira
                float bushBonus = LootManager.GetResourceBonus(stats, TargetType.Bush, null);
                if (Mathf.Abs(bushBonus - 0.45f) > 0.001f)
                    throw new Exception($"Bônus de TargetType.Bush deve ser 0.45. Obtido: {bushBonus}");

                // Pedra (TargetType.Stone) -> Bônus de mineração
                float stoneBonus = LootManager.GetResourceBonus(stats, TargetType.Stone, null);
                if (Mathf.Abs(stoneBonus - 0.30f) > 0.001f)
                    throw new Exception($"Bônus de TargetType.Stone deve ser 0.30. Obtido: {stoneBonus}");

                // Minério (TargetType.Ore) -> Bônus de mineração
                float oreBonus = LootManager.GetResourceBonus(stats, TargetType.Ore, null);
                if (Mathf.Abs(oreBonus - 0.30f) > 0.001f)
                    throw new Exception($"Bônus de TargetType.Ore deve ser 0.30. Obtido: {oreBonus}");

                // Matching por item definition ID (sem TargetType explícito)
                var woodAsset = AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>("Assets/_Duskborn/ScriptableObjects/Resources/material_wood.asset");
                if (woodAsset == null)
                    throw new Exception("Falha ao carregar 'material_wood.asset'.");
                float woodItemBonus = LootManager.GetResourceBonus(stats, TargetType.None, woodAsset);
                if (Mathf.Abs(woodItemBonus - 0.45f) > 0.001f)
                    throw new Exception($"Bônus de material_wood deve ser 0.45. Obtido: {woodItemBonus}");

                var ironAsset = AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>("Assets/_Duskborn/ScriptableObjects/Resources/material_iron.asset");
                if (ironAsset == null)
                    throw new Exception("Falha ao carregar 'material_iron.asset'.");
                float ironItemBonus = LootManager.GetResourceBonus(stats, TargetType.None, ironAsset);
                if (Mathf.Abs(ironItemBonus - 0.30f) > 0.001f)
                    throw new Exception($"Bônus de material_iron deve ser 0.30. Obtido: {ironItemBonus}");
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
                    throw new Exception("Falha ao carregar 'material_iron.asset'.");

                // Inimigo besta ou humanoide soltando ferro nunca deve receber bônus de mineração
                float enemyDropBonus = LootManager.GetResourceBonus(stats, TargetType.Beast, ironAsset);
                if (enemyDropBonus != 0f)
                    throw new Exception($"Saque de inimigos (Beast) deve ter bonus 0. Obtido: {enemyDropBonus}");

                float humanoidDropBonus = LootManager.GetResourceBonus(stats, TargetType.Humanoid, ironAsset);
                if (humanoidDropBonus != 0f)
                    throw new Exception($"Saque de inimigos (Humanoid) deve ter bonus 0. Obtido: {humanoidDropBonus}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_WeaponAssets_GatheringBonusIntegrity()
        {
            // Stone Axe deve possuir bônus de WoodcuttingResourceBonus
            var axe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Axe/stone_axe.asset");
            if (axe == null)
                throw new Exception("Nao foi possivel carregar 'stone_axe.asset'.");

            bool axeFound = false;
            foreach (var b in axe.Bonuses)
            {
                if (b.Type == StatType.WoodcuttingResourceBonus)
                {
                    axeFound = true;
                    if (b.Value <= 0f)
                        throw new Exception($"Stone Axe WoodcuttingResourceBonus deve ser positivo. Obtido: {b.Value}");
                }
            }
            if (!axeFound)
                throw new Exception("Stone Axe nao possui o bonus WoodcuttingResourceBonus configurado.");

            // Stone Pickaxe deve possuir bônus de MiningResourceBonus
            var pickaxe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Pickaxe/stone_pickaxe.asset");
            if (pickaxe == null)
                throw new Exception("Nao foi possivel carregar 'stone_pickaxe.asset'.");

            bool pickaxeFound = false;
            foreach (var b in pickaxe.Bonuses)
            {
                if (b.Type == StatType.MiningResourceBonus)
                {
                    pickaxeFound = true;
                    if (b.Value <= 0f)
                        throw new Exception($"Stone Pickaxe MiningResourceBonus deve ser positivo. Obtido: {b.Value}");
                }
            }
            if (!pickaxeFound)
                throw new Exception("Stone Pickaxe nao possui o bonus MiningResourceBonus configurado.");
        }

        private static void Test_EntityStats_BuffLayersAndReset()
        {
            var stats = new EntityStats();

            // Configura bônus de equipamento
            stats.MiningResourceBonus = 0.20f;
            stats.WoodcuttingResourceBonus = 0.15f;

            // Bônus aditivos de buffs
            stats.MiningResourceBuffAdditive = 0.10f;
            stats.WoodcuttingResourceBuffAdditive = 0.05f;

            // Bônus multiplicativos de buffs
            stats.MiningResourceBuffFactor = 1.20f;
            stats.WoodcuttingResourceBuffFactor = 1.50f;

            // Cálculo efetivo de mineração: (0.20 + 0.10) * 1.20 = 0.36
            float expectedMining = (0.20f + 0.10f) * 1.20f;
            if (Mathf.Abs(stats.EffectiveMiningResourceBonus - expectedMining) > 0.0001f)
                throw new Exception($"EffectiveMiningResourceBonus incorreto. Esperado: {expectedMining}, Obtido: {stats.EffectiveMiningResourceBonus}");

            // Cálculo efetivo de corte de madeira: (0.15 + 0.05) * 1.50 = 0.30
            float expectedWood = (0.15f + 0.05f) * 1.50f;
            if (Mathf.Abs(stats.EffectiveWoodcuttingResourceBonus - expectedWood) > 0.0001f)
                throw new Exception($"EffectiveWoodcuttingResourceBonus incorreto. Esperado: {expectedWood}, Obtido: {stats.EffectiveWoodcuttingResourceBonus}");

            // Reset
            stats.ResetMultipliers();
            if (stats.EffectiveMiningResourceBonus != 0f || stats.EffectiveWoodcuttingResourceBonus != 0f)
                throw new Exception("ResetMultipliers nao zerou os bonus de recursos.");
        }
    }
}
