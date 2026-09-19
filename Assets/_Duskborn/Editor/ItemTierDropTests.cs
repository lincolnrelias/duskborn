using System;
using System.Collections.Generic;
using Duskborn.Audio;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.World;
using InventorySystem.Data;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    /// <summary>
    /// Testes automatizados para o sistema de tiers (Comum -> Incomum -> Raro -> Épico -> Lendário),
    /// efeitos visuais de luz/feixe estilo Diablo e integração de áudio do item mais raro dropado.
    /// </summary>
    public static class ItemTierDropTests
    {
        [MenuItem("Duskborn/Tests/Run Item Tier & Drop Tests", false, 107)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_ItemRarity_HierarchyAndOrdering, ref passed, ref total);
            RunTest(Test_ItemTierHelper_ColorsAndLights, ref passed, ref total);
            RunTest(Test_AudioDatabase_TierDropClipsRegistered, ref passed, ref total);
            RunTest(Test_DropSound_SingleRarestItemSelection, ref passed, ref total);
            RunTest(Test_DroppedItemVisuals_ComponentAndMeshCreation, ref passed, ref total);
            RunTest(Test_DroppedItemVisuals_ScaleIsolation_MaintainsUniformWorldScale, ref passed, ref total);
            RunTest(Test_ItemTierHelper_ShouldFloatInAir_OnlyEpicAndUp, ref passed, ref total);
            RunTest(Test_DroppedItemVisuals_FloatingBehavior_CommonRareVsEpic, ref passed, ref total);
            RunTest(Test_TestItemsAndTables_AssetIntegrity, ref passed, ref total);
            RunTest(Test_StaticTierTestNodes_Configuration, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[ItemTierDropTests] {passed}/{total} testes passaram com sucesso!</b></color>");
        }

        private static void RunTest(Action testMethod, ref int passed, ref int total)
        {
            total++;
            try
            {
                testMethod();
                passed++;
                Debug.Log($"<color=#55FF55>[PASS]</color> {testMethod.Method.Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=#FF5555>[FAIL]</color> {testMethod.Method.Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void Test_ItemRarity_HierarchyAndOrdering()
        {
            // O sistema de tiers deve obedecer a ordem estrita: Common -> Uncommon -> Rare -> Epic -> Legendary
            if ((int)ItemRarity.Common != 0)
                throw new Exception($"ItemRarity.Common deve ter valor 0, obtido: {(int)ItemRarity.Common}");
            if ((int)ItemRarity.Uncommon != 1)
                throw new Exception($"ItemRarity.Uncommon deve ter valor 1, obtido: {(int)ItemRarity.Uncommon}");
            if ((int)ItemRarity.Rare != 2)
                throw new Exception($"ItemRarity.Rare deve ter valor 2, obtido: {(int)ItemRarity.Rare}");
            if ((int)ItemRarity.Epic != 3)
                throw new Exception($"ItemRarity.Epic deve ter valor 3, obtido: {(int)ItemRarity.Epic}");
            if ((int)ItemRarity.Legendary != 4)
                throw new Exception($"ItemRarity.Legendary deve ter valor 4, obtido: {(int)ItemRarity.Legendary}");

            if (ItemRarity.Common >= ItemRarity.Uncommon ||
                ItemRarity.Uncommon >= ItemRarity.Rare ||
                ItemRarity.Rare >= ItemRarity.Epic ||
                ItemRarity.Epic >= ItemRarity.Legendary)
            {
                throw new Exception("Falha na ordenação de hierarquia de raridades.");
            }
        }

        private static void Test_ItemTierHelper_ColorsAndLights()
        {
            ItemRarity[] rarities = new[]
            {
                ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary
            };

            float prevIntensity = 0f;
            float prevRange = 0f;
            float prevBeamHeight = 0f;

            foreach (var r in rarities)
            {
                Color col = ItemTierHelper.GetColor(r);
                if (col.a <= 0f)
                    throw new Exception($"Cor inválida para raridade {r}");

                float intensity = ItemTierHelper.GetLightIntensity(r);
                if (intensity <= prevIntensity)
                    throw new Exception($"Intensidade de luz para {r} ({intensity}) deveria ser maior que {prevIntensity}");
                prevIntensity = intensity;

                float range = ItemTierHelper.GetLightRange(r);
                if (range <= prevRange)
                    throw new Exception($"Alcance de luz para {r} ({range}) deveria ser maior que {prevRange}");
                prevRange = range;

                float beamHeight = ItemTierHelper.GetBeamHeight(r);
                if (beamHeight <= prevBeamHeight)
                    throw new Exception($"Altura do feixe para {r} ({beamHeight}) deveria ser maior que {prevBeamHeight}");
                prevBeamHeight = beamHeight;

                string localized = ItemTierHelper.GetLocalizedName(r);
                if (string.IsNullOrEmpty(localized))
                    throw new Exception($"Nome localizado vazio para {r}");
            }
        }

        private static void Test_AudioDatabase_TierDropClipsRegistered()
        {
            var db = AudioDatabase.Instance;
            if (db == null) throw new Exception("AudioDatabase.Instance é nulo.");

            db.AutoPopulateDefaults();

            ItemRarity[] rarities = new[]
            {
                ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary
            };

            foreach (var r in rarities)
            {
                AudioClip clip = db.GetDropClip(r);
                if (clip == null)
                    throw new Exception($"AudioDatabase não possui clipe de drop configurado para raridade {r}");

                float vol = db.GetDropVolume(r);
                if (vol <= 0f)
                    throw new Exception($"Volume de drop para {r} deve ser > 0, obtido: {vol}");
            }
        }

        private static void Test_DropSound_SingleRarestItemSelection()
        {
            // Simula uma lista de drops mistos para garantir que a raridade máxima é sempre escolhida
            List<ItemRarity> dropListA = new List<ItemRarity> { ItemRarity.Common, ItemRarity.Common, ItemRarity.Common };
            ItemRarity rarestA = GetRarestRarity(dropListA);
            if (rarestA != ItemRarity.Common)
                throw new Exception($"Esperado Comum para dropListA, obtido: {rarestA}");

            // Misto: 3 Comuns e 1 Lendário -> deve selecionar Lendário
            List<ItemRarity> dropListB = new List<ItemRarity> { ItemRarity.Common, ItemRarity.Legendary, ItemRarity.Common, ItemRarity.Common };
            ItemRarity rarestB = GetRarestRarity(dropListB);
            if (rarestB != ItemRarity.Legendary)
                throw new Exception($"Esperado Lendário para dropListB, obtido: {rarestB}");

            // Misto: Incomum + Raro + Épico -> deve selecionar Épico
            List<ItemRarity> dropListC = new List<ItemRarity> { ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Common };
            ItemRarity rarestC = GetRarestRarity(dropListC);
            if (rarestC != ItemRarity.Epic)
                throw new Exception($"Esperado Épico para dropListC, obtido: {rarestC}");
        }

        private static ItemRarity GetRarestRarity(List<ItemRarity> items)
        {
            ItemRarity rarest = ItemRarity.Common;
            foreach (var item in items)
            {
                if (item > rarest) rarest = item;
            }
            return rarest;
        }

        private static void Test_DroppedItemVisuals_ComponentAndMeshCreation()
        {
            var go = new GameObject("TestDroppedItem");
            try
            {
                var rb = go.AddComponent<Rigidbody>();
                var visuals = go.AddComponent<DroppedItemVisuals>();
                visuals.Setup(ItemRarity.Legendary);

                Light light = visuals.PointLight != null ? visuals.PointLight : go.GetComponentInChildren<Light>();
                if (light == null) throw new Exception("Luz pontual não foi criada em DroppedItemVisuals.");
                if (light.color != ItemTierHelper.GetColor(ItemRarity.Legendary))
                    throw new Exception("Cor da luz não corresponde à cor do tier Lendário.");

                var beam = visuals.BeamObject != null ? visuals.BeamObject : go.transform.Find("DiabloLootBeam")?.gameObject;
                if (beam == null) throw new Exception("Objeto DiabloLootBeam não foi criado.");

                var halo = visuals.HaloObject != null ? visuals.HaloObject : go.transform.Find("GroundHalo")?.gameObject;
                if (halo == null) throw new Exception("Objeto GroundHalo não foi criado.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_DroppedItemVisuals_ScaleIsolation_MaintainsUniformWorldScale()
        {
            var go = new GameObject("TestScaledItem");
            try
            {
                // Simula prefab com escala extrema como ferro (20,20,20) e tora (15,30,30)
                go.transform.localScale = new Vector3(20f, 20f, 20f);
                var visuals = go.AddComponent<DroppedItemVisuals>();
                visuals.Setup(ItemRarity.Epic);

                if (visuals.VfxRoot == null)
                    throw new Exception("VfxRoot não foi criado para isolamento de escala.");

                // A escala mundial da raiz VFX deve permanecer estritamente uniforme (1, 1, 1)
                Vector3 vfxScale = visuals.VfxRoot.transform.lossyScale;
                if (Mathf.Abs(vfxScale.x - 1f) > 0.001f ||
                    Mathf.Abs(vfxScale.y - 1f) > 0.001f ||
                    Mathf.Abs(vfxScale.z - 1f) > 0.001f)
                {
                    throw new Exception($"VfxRoot não manteve escala uniforme (1,1,1). Obtido: {vfxScale}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_TestItemsAndTables_AssetIntegrity()
        {
            string[] itemPaths = new[]
            {
                "Assets/_Duskborn/ScriptableObjects/Resources/material_test_common.asset",
                "Assets/_Duskborn/ScriptableObjects/Resources/material_test_uncommon.asset",
                "Assets/_Duskborn/ScriptableObjects/Resources/material_test_rare.asset",
                "Assets/_Duskborn/ScriptableObjects/Resources/material_test_epic.asset",
                "Assets/_Duskborn/ScriptableObjects/Resources/material_test_legendary.asset"
            };

            ItemRarity[] expectedRarities = new[]
            {
                ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary
            };

            for (int i = 0; i < itemPaths.Length; i++)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(itemPaths[i]);
                if (item == null)
                    throw new Exception($"Item de teste '{itemPaths[i]}' não pôde ser carregado.");
                if (item.Rarity != expectedRarities[i])
                    throw new Exception($"Item '{item.name}' deveria ter raridade {expectedRarities[i]}, obtido: {item.Rarity}");
                if (item.dropPrefab == null)
                    throw new Exception($"Item '{item.name}' não possui dropPrefab atribuído.");
                if (item.dropPrefab.name.Equals("droppable_coin", StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Item '{item.name}' não pode usar droppable_coin como dropPrefab regular.");
                if (item.dropPrefab.GetComponent<WorldItemPickup>() == null)
                    throw new Exception($"Item '{item.name}' dropPrefab '{item.dropPrefab.name}' não possui componente WorldItemPickup.");
            }

            string[] tablePaths = new[]
            {
                "Assets/_Duskborn/ScriptableObjects/Loot/table_test_common.asset",
                "Assets/_Duskborn/ScriptableObjects/Loot/table_test_uncommon.asset",
                "Assets/_Duskborn/ScriptableObjects/Loot/table_test_rare.asset",
                "Assets/_Duskborn/ScriptableObjects/Loot/table_test_epic.asset",
                "Assets/_Duskborn/ScriptableObjects/Loot/table_test_legendary.asset",
                "Assets/_Duskborn/ScriptableObjects/Loot/table_test_mixed.asset"
            };

            foreach (var tPath in tablePaths)
            {
                var table = AssetDatabase.LoadAssetAtPath<DropLootTable>(tPath);
                if (table == null)
                    throw new Exception($"Tabela de loot '{tPath}' não pôde ser carregada.");
                if (table.entries == null || table.entries.Length == 0)
                    throw new Exception($"Tabela '{tPath}' não possui entradas de drop.");
            }
        }

        private static void Test_StaticTierTestNodes_Configuration()
        {
            if (StaticTierTestNodes.TestConfigs == null || StaticTierTestNodes.TestConfigs.Length < 10)
                throw new Exception("StaticTierTestNodes deve ter pelo menos 10 configurações de teste de nós (2 de cada tier).");

            int countCommon = 0, countUncommon = 0, countRare = 0, countEpic = 0, countLegendary = 0;
            foreach (var cfg in StaticTierTestNodes.TestConfigs)
            {
                if (cfg.rarity == ItemRarity.Common) countCommon++;
                if (cfg.rarity == ItemRarity.Uncommon) countUncommon++;
                if (cfg.rarity == ItemRarity.Rare) countRare++;
                if (cfg.rarity == ItemRarity.Epic) countEpic++;
                if (cfg.rarity == ItemRarity.Legendary) countLegendary++;
            }

            if (countCommon < 2 || countUncommon < 2 || countRare < 2 || countEpic < 2 || countLegendary < 2)
                throw new Exception($"StaticTierTestNodes deve conter pelo menos 2 nós de cada tier. Obtido: C={countCommon}, U={countUncommon}, R={countRare}, E={countEpic}, L={countLegendary}");
        }

        private static void Test_ItemTierHelper_ShouldFloatInAir_OnlyEpicAndUp()
        {
            // Apenas Épico e Lendário devem flutuar no ar. Comum, Incomum e Raro caem no chão.
            if (ItemTierHelper.ShouldFloatInAir(ItemRarity.Common))
                throw new Exception("Common NÃO deve flutuar no ar.");
            if (ItemTierHelper.ShouldFloatInAir(ItemRarity.Uncommon))
                throw new Exception("Uncommon NÃO deve flutuar no ar.");
            if (ItemTierHelper.ShouldFloatInAir(ItemRarity.Rare))
                throw new Exception("Rare NÃO deve flutuar no ar.");
            if (!ItemTierHelper.ShouldFloatInAir(ItemRarity.Epic))
                throw new Exception("Epic DEVE flutuar no ar.");
            if (!ItemTierHelper.ShouldFloatInAir(ItemRarity.Legendary))
                throw new Exception("Legendary DEVE flutuar no ar.");

            // Altura de flutuação deve ser > 0 apenas para Epic+ e crescente
            if (ItemTierHelper.GetHoverHeight(ItemRarity.Common) != 0f)
                throw new Exception("HoverHeight para Common deve ser 0.");
            if (ItemTierHelper.GetHoverHeight(ItemRarity.Uncommon) != 0f)
                throw new Exception("HoverHeight para Uncommon deve ser 0.");
            if (ItemTierHelper.GetHoverHeight(ItemRarity.Rare) != 0f)
                throw new Exception("HoverHeight para Rare deve ser 0.");

            float hEpic = ItemTierHelper.GetHoverHeight(ItemRarity.Epic);
            float hLeg = ItemTierHelper.GetHoverHeight(ItemRarity.Legendary);

            if (hEpic <= 0f) throw new Exception("HoverHeight para Epic deve ser > 0.");
            if (hLeg <= hEpic) throw new Exception($"HoverHeight para Legendary ({hLeg}) deve ser maior que Epic ({hEpic}).");
        }

        private static void Test_DroppedItemVisuals_FloatingBehavior_CommonRareVsEpic()
        {
            var go = new GameObject("TestFloatingItem");
            try
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;

                var visuals = go.AddComponent<DroppedItemVisuals>();

                // 1) Teste com item Comum: Não deve flutuar
                visuals.Setup(ItemRarity.Common);
                if (rb.isKinematic)
                    throw new Exception("Item Comum não deve ser kinematic!");
                if (ItemTierHelper.ShouldFloatInAir(visuals.CurrentRarity))
                    throw new Exception("Item Comum NÃO deve ter ShouldFloatInAir ativo.");

                // 2) Teste com item Raro: Não deve flutuar
                visuals.Setup(ItemRarity.Rare);
                if (ItemTierHelper.ShouldFloatInAir(visuals.CurrentRarity))
                    throw new Exception("Item Raro NÃO deve ter ShouldFloatInAir ativo.");

                // 3) Teste com item Épico: Deve ter ShouldFloatInAir ativo
                visuals.Setup(ItemRarity.Epic);
                if (!ItemTierHelper.ShouldFloatInAir(visuals.CurrentRarity))
                    throw new Exception("Item Épico deveria ter ShouldFloatInAir ativo.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
