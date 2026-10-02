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
    /// Automated tests for the tier system (Common -> Uncommon -> Rare -> Epic -> Legendary),
    /// Diablo-style light / beam effects and audio integration for the rarest dropped item.
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
            RunTest(Test_DroppedItemVisuals_RarityAdjustments_WhiteGreenAndSkyward, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[ItemTierDropTests] {passed}/{total} tests passed!</b></color>");
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
            // The tier system must follow the strict order: Common -> Uncommon -> Rare -> Epic -> Legendary.
            if ((int)ItemRarity.Common != 0)
                throw new Exception($"ItemRarity.Common must have value 0, got: {(int)ItemRarity.Common}");
            if ((int)ItemRarity.Uncommon != 1)
                throw new Exception($"ItemRarity.Uncommon must have value 1, got: {(int)ItemRarity.Uncommon}");
            if ((int)ItemRarity.Rare != 2)
                throw new Exception($"ItemRarity.Rare must have value 2, got: {(int)ItemRarity.Rare}");
            if ((int)ItemRarity.Epic != 3)
                throw new Exception($"ItemRarity.Epic must have value 3, got: {(int)ItemRarity.Epic}");
            if ((int)ItemRarity.Legendary != 4)
                throw new Exception($"ItemRarity.Legendary must have value 4, got: {(int)ItemRarity.Legendary}");

            if (ItemRarity.Common >= ItemRarity.Uncommon ||
                ItemRarity.Uncommon >= ItemRarity.Rare ||
                ItemRarity.Rare >= ItemRarity.Epic ||
                ItemRarity.Epic >= ItemRarity.Legendary)
            {
                throw new Exception("Rarity hierarchy ordering failed.");
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
                    throw new Exception($"Invalid color for rarity {r}");

                float intensity = ItemTierHelper.GetLightIntensity(r);
                if (intensity <= prevIntensity)
                    throw new Exception($"Light intensity for {r} ({intensity}) should exceed {prevIntensity}");
                prevIntensity = intensity;

                float range = ItemTierHelper.GetLightRange(r);
                if (range <= prevRange)
                    throw new Exception($"Light range for {r} ({range}) should exceed {prevRange}");
                prevRange = range;

                float beamHeight = ItemTierHelper.GetBeamHeight(r);
                if (beamHeight <= prevBeamHeight)
                    throw new Exception($"Beam height for {r} ({beamHeight}) should exceed {prevBeamHeight}");
                prevBeamHeight = beamHeight;

                string localized = ItemTierHelper.GetLocalizedName(r);
                if (string.IsNullOrEmpty(localized))
                    throw new Exception($"Empty display name for {r}");
            }
        }

        private static void Test_AudioDatabase_TierDropClipsRegistered()
        {
            var db = AudioDatabase.Instance;
            if (db == null) throw new Exception("AudioDatabase.Instance is null.");

            db.AutoPopulateDefaults();

            ItemRarity[] rarities = new[]
            {
                ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary
            };

            foreach (var r in rarities)
            {
                AudioClip clip = db.GetDropClip(r);
                if (clip == null)
                    throw new Exception($"AudioDatabase has no drop clip configured for rarity {r}");

                float vol = db.GetDropVolume(r);
                if (vol <= 0f)
                    throw new Exception($"Drop volume for {r} must be > 0, got: {vol}");
            }
        }

        private static void Test_DropSound_SingleRarestItemSelection()
        {
            // Simulate mixed drops to ensure the highest rarity is always selected.
            List<ItemRarity> dropListA = new List<ItemRarity> { ItemRarity.Common, ItemRarity.Common, ItemRarity.Common };
            ItemRarity rarestA = GetRarestRarity(dropListA);
            if (rarestA != ItemRarity.Common)
                throw new Exception($"Expected Common for dropListA, got: {rarestA}");

            // Mixed: 3 Common and 1 Legendary -> must select Legendary.
            List<ItemRarity> dropListB = new List<ItemRarity> { ItemRarity.Common, ItemRarity.Legendary, ItemRarity.Common, ItemRarity.Common };
            ItemRarity rarestB = GetRarestRarity(dropListB);
            if (rarestB != ItemRarity.Legendary)
                throw new Exception($"Expected Legendary for dropListB, got: {rarestB}");

            // Mixed: Uncommon + Rare + Epic -> must select Epic.
            List<ItemRarity> dropListC = new List<ItemRarity> { ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Common };
            ItemRarity rarestC = GetRarestRarity(dropListC);
            if (rarestC != ItemRarity.Epic)
                throw new Exception($"Expected Epic for dropListC, got: {rarestC}");
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
                if (light == null) throw new Exception("Point light was not created in DroppedItemVisuals.");
                if (light.color != ItemTierHelper.GetColor(ItemRarity.Legendary))
                    throw new Exception("Light color does not match the Legendary tier color.");

                var beam = visuals.BeamObject != null ? visuals.BeamObject : go.transform.Find("DiabloLootBeam")?.gameObject;
                if (beam == null) throw new Exception("DiabloLootBeam object was not created.");

                var halo = visuals.HaloObject != null ? visuals.HaloObject : go.transform.Find("GroundHalo")?.gameObject;
                if (halo == null) throw new Exception("GroundHalo object was not created.");
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
                // Simulate a prefab with extreme scale, such as iron (20,20,20) and a log (15,30,30).
                go.transform.localScale = new Vector3(20f, 20f, 20f);
                var visuals = go.AddComponent<DroppedItemVisuals>();
                visuals.Setup(ItemRarity.Epic);

                if (visuals.VfxRoot == null)
                    throw new Exception("VfxRoot was not created for scale isolation.");

                // The VFX root world scale must remain strictly uniform (1, 1, 1).
                Vector3 vfxScale = visuals.VfxRoot.transform.lossyScale;
                if (Mathf.Abs(vfxScale.x - 1f) > 0.001f ||
                    Mathf.Abs(vfxScale.y - 1f) > 0.001f ||
                    Mathf.Abs(vfxScale.z - 1f) > 0.001f)
                {
                    throw new Exception($"VfxRoot did not retain uniform scale (1,1,1). Got: {vfxScale}");
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
                    throw new Exception($"Test item '{itemPaths[i]}' could not be loaded.");
                if (item.Rarity != expectedRarities[i])
                    throw new Exception($"Item '{item.name}' should have rarity {expectedRarities[i]}, actual: {item.Rarity}");
                if (item.dropPrefab == null)
                    throw new Exception($"Item '{item.name}' has no dropPrefab assigned.");
                if (item.dropPrefab.name.Equals("droppable_coin", StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Item '{item.name}' cannot use droppable_coin as a regular dropPrefab.");
                if (item.dropPrefab.GetComponent<WorldItemPickup>() == null)
                    throw new Exception($"Item '{item.name}' dropPrefab '{item.dropPrefab.name}' has no WorldItemPickup component.");
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
                    throw new Exception($"Loot table '{tPath}' could not be loaded.");
                if (table.entries == null || table.entries.Length == 0)
                    throw new Exception($"Table '{tPath}' has no drop entries.");
            }
        }

        private static void Test_StaticTierTestNodes_Configuration()
        {
            if (StaticTierTestNodes.TestConfigs == null || StaticTierTestNodes.TestConfigs.Length < 10)
                throw new Exception("StaticTierTestNodes must have at least 10 node test configurations (2 of each tier).");

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
                throw new Exception($"StaticTierTestNodes must contain at least 2 nodes of each tier. Got: C={countCommon}, U={countUncommon}, R={countRare}, E={countEpic}, L={countLegendary}");
        }

        private static void Test_ItemTierHelper_ShouldFloatInAir_OnlyEpicAndUp()
        {
            // Only Epic and Legendary items float in the air. Common, Uncommon, and Rare fall to the ground.
            if (ItemTierHelper.ShouldFloatInAir(ItemRarity.Common))
                throw new Exception("Common must NOT float in the air.");
            if (ItemTierHelper.ShouldFloatInAir(ItemRarity.Uncommon))
                throw new Exception("Uncommon must NOT float in the air.");
            if (ItemTierHelper.ShouldFloatInAir(ItemRarity.Rare))
                throw new Exception("Rare must NOT float in the air.");
            if (!ItemTierHelper.ShouldFloatInAir(ItemRarity.Epic))
                throw new Exception("Epic MUST float in the air.");
            if (!ItemTierHelper.ShouldFloatInAir(ItemRarity.Legendary))
                throw new Exception("Legendary MUST float in the air.");

            // Hover height must be > 0 only for Epic+ and increase with rarity.
            if (ItemTierHelper.GetHoverHeight(ItemRarity.Common) != 0f)
                throw new Exception("HoverHeight for Common must be 0.");
            if (ItemTierHelper.GetHoverHeight(ItemRarity.Uncommon) != 0f)
                throw new Exception("HoverHeight for Uncommon must be 0.");
            if (ItemTierHelper.GetHoverHeight(ItemRarity.Rare) != 0f)
                throw new Exception("HoverHeight for Rare must be 0.");

            float hEpic = ItemTierHelper.GetHoverHeight(ItemRarity.Epic);
            float hLeg = ItemTierHelper.GetHoverHeight(ItemRarity.Legendary);

            if (hEpic <= 0f) throw new Exception("HoverHeight for Epic must be > 0.");
            if (hLeg <= hEpic) throw new Exception($"HoverHeight for Legendary ({hLeg}) must exceed Epic ({hEpic}).");
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

                // 1) Test with a Common item: must not float.
                visuals.Setup(ItemRarity.Common);
                if (rb.isKinematic)
                    throw new Exception("Common item must not be kinematic!");
                if (ItemTierHelper.ShouldFloatInAir(visuals.CurrentRarity))
                    throw new Exception("Common item must NOT have ShouldFloatInAir active.");

                // 2) Test with a Rare item: must not float.
                visuals.Setup(ItemRarity.Rare);
                if (ItemTierHelper.ShouldFloatInAir(visuals.CurrentRarity))
                    throw new Exception("Rare item must NOT have ShouldFloatInAir active.");

                // 3) Test with an Epic item: ShouldFloatInAir must be active.
                visuals.Setup(ItemRarity.Epic);
                if (!ItemTierHelper.ShouldFloatInAir(visuals.CurrentRarity))
                    throw new Exception("Epic item should have ShouldFloatInAir active.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_DroppedItemVisuals_RarityAdjustments_WhiteGreenAndSkyward()
        {
            var go = new GameObject("TestRarityRulesItem");
            try
            {
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                var mr = go.AddComponent<MeshRenderer>();
                var rb = go.AddComponent<Rigidbody>();
                var visuals = go.AddComponent<DroppedItemVisuals>();

                // 1) Common: no visual effects (no beam, halo, light, or overlay).
                visuals.Setup(ItemRarity.Common);
                if (visuals.BeamObject != null)
                    throw new Exception("Common item must not have BeamObject!");
                if (visuals.HaloObject != null)
                    throw new Exception("Common item must not have HaloObject!");
                if (visuals.PointLight != null)
                    throw new Exception("Common item must not have PointLight!");
                if (visuals.OverlayObjects.Count > 0)
                    throw new Exception("Common item must not have RarityWaveOverlay!");

                // 2) Uncommon: only a green wave / outline shader (no sky beam, halo, or light).
                visuals.Setup(ItemRarity.Uncommon);
                if (visuals.BeamObject != null)
                    throw new Exception("Uncommon item must not have BeamObject (no sky beam)!");
                if (visuals.HaloObject != null)
                    throw new Exception("Uncommon item must not have HaloObject!");
                if (visuals.PointLight != null)
                    throw new Exception("Uncommon item must not have PointLight!");
                if (visuals.OverlayObjects.Count == 0)
                    throw new Exception("Uncommon item MUST have RarityWaveOverlay!");

                // 3) Rare: no sky beam, only a blue wave overlay.
                visuals.Setup(ItemRarity.Rare);
                if (visuals.BeamObject != null)
                    throw new Exception("Rare item must NOT have BeamObject (no sky beam)!");
                if (visuals.HaloObject != null)
                    throw new Exception("Rare item must NOT have HaloObject!");
                if (visuals.PointLight != null)
                    throw new Exception("Rare item must NOT have PointLight!");
                if (visuals.OverlayObjects.Count == 0)
                    throw new Exception("Rare item MUST have RarityWaveOverlay!");

                // 4) Epic and Legendary: retain the vertical sky beam and halo, and also have the wave shader.
                visuals.Setup(ItemRarity.Epic);
                if (visuals.BeamObject == null)
                    throw new Exception("Epic item MUST have BeamObject directed toward the sky!");
                if (visuals.OverlayObjects.Count == 0)
                    throw new Exception("Epic item MUST have RarityWaveOverlay!");

                visuals.Setup(ItemRarity.Legendary);
                if (visuals.BeamObject == null)
                    throw new Exception("Legendary item MUST have BeamObject directed toward the sky!");
                if (visuals.OverlayObjects.Count == 0)
                    throw new Exception("Legendary item MUST have RarityWaveOverlay!");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
