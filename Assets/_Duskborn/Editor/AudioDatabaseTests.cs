using System;
using UnityEngine;
using UnityEditor;
using Duskborn.Audio;
using Duskborn.Audio.Editor;
using Duskborn.Gameplay;

namespace Duskborn.Editor
{
    /// <summary>
    /// Automated tests for the central audio database (AudioDatabase) and sound utilities.
    /// </summary>
    public static class AudioDatabaseTests
    {
        [MenuItem("Duskborn/Tests/Run Audio Database Tests", false, 106)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_AudioDatabase_InstanceExists, ref passed, ref total);
            RunTest(Test_AudioDatabase_AutoPopulateAndCategories, ref passed, ref total);
            RunTest(Test_AudioDatabase_GetDepletedClip_OreAndStone, ref passed, ref total);
            RunTest(Test_AudioDatabase_GetFootstepClip_Surfaces, ref passed, ref total);
            RunTest(Test_AudioDatabase_LootPickupAndDropClips, ref passed, ref total);
            RunTest(Test_CrystalAndMaterialFoleyRouting, ref passed, ref total);
            RunTest(Test_CrystalPrefabsResolveCurrentDestruction, ref passed, ref total);
            RunTest(Test_AllPickupRaritiesUseDistinctFoley, ref passed, ref total);
            RunTest(Test_AudioPreviewUtility_SafeExecution, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[AudioDatabaseTests] {passed}/{total} tests passed!</b></color>");
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

        private static void Test_CrystalPrefabsResolveCurrentDestruction()
        {
            foreach (var element in new[] { "flame", "nature", "storm", "earth", "frost", "blood", "dark", "light" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Duskborn/Prefabs/World/Node_{element}_Crystal.prefab");
                if (prefab == null) throw new Exception($"Missing {element} crystal prefab.");
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var node = instance.GetComponent<Duskborn.Gameplay.Loot.ResourceNode>();
                    // A stale scene override must never take priority over the elemental bank.
                    var serialized = new SerializedObject(node);
                    serialized.FindProperty("depletedClip").objectReferenceValue = AudioDatabase.Instance.ResourcesSettings.oreShatterClips[0];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    string material = node.GetAudioMaterial();
                    if (material != "Crystal_" + element) throw new Exception($"Prefab {element} resolved as {material}.");
                    var clip = node.ResolveDepletedClip(material);
                    if (clip == null || !clip.name.StartsWith("crystal_" + element + "_break_live_v2_"))
                        throw new Exception($"Actual {element} node selected legacy destruction audio.");
                    string path = AssetDatabase.GetAssetPath(clip);
                    if (!path.StartsWith("Assets/_Duskborn/Resources/SFX/CrystalBreaksV2/"))
                        throw new Exception($"Unexpected destruction asset path: {path}.");
                    var visual = instance.GetComponentInChildren<Duskborn.Effects.ElementalCrystalNodeVisual>();
                    UnityEngine.Object.DestroyImmediate(visual);
                    if (node.GetAudioMaterial() != "Crystal_" + element)
                        throw new Exception("Cached scene node lost its element when its old visual component was absent.");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }

        private static void Test_AllPickupRaritiesUseDistinctFoley()
        {
            foreach (Duskborn.Gameplay.Loot.ItemRarity rarity in new[] {
                Duskborn.Gameplay.Loot.ItemRarity.Common, Duskborn.Gameplay.Loot.ItemRarity.Uncommon,
                Duskborn.Gameplay.Loot.ItemRarity.Rare, Duskborn.Gameplay.Loot.ItemRarity.Epic,
                Duskborn.Gameplay.Loot.ItemRarity.Legendary, Duskborn.Gameplay.Loot.ItemRarity.Cursed })
            {
                var tier = rarity.ToString().ToLowerInvariant();
                AudioClip previous = null;
                for (int i = 0; i < 12; i++)
                {
                    var clip = AudioDatabase.Instance.GetPickupClip(rarity);
                    if (clip == null || !clip.name.StartsWith("pickup_" + tier + "_") || clip == previous || clip.length > .5f)
                        throw new Exception($"Missing, incorrect or repeating {rarity} pickup cue.");
                    previous = clip;
                }
                foreach (var material in new[] { "wood", "stone", "crystal", "soft" })
                {
                    var clip = GatheringFoley.Pick("collect_" + material + "_" + tier);
                    if (clip == null || clip.frequency != 48000 || clip.channels != 1)
                        throw new Exception($"Missing {material}/{tier} collection foley.");
                }
            }
        }

        private static void Test_CrystalAndMaterialFoleyRouting()
        {
            var settings = AudioDatabase.Instance.ResourcesSettings;
            foreach (var element in new[] { "flame", "nature", "storm", "earth", "frost", "blood", "dark", "light" })
            {
                AudioClip previousHit = null, previousBreak = null;
                for (int i = 0; i < 12; i++)
                {
                    var hit = settings.GetHitClip("Crystal_" + element);
                    var broken = settings.GetDepletedClip(TargetType.Ore, "Crystal_" + element);
                    if (hit == null || !hit.name.StartsWith("crystal_" + element + "_hit_") || hit == previousHit)
                        throw new Exception($"Incorrect or repeating {element} crystal contact.");
                    if (broken == null || !broken.name.StartsWith("crystal_" + element + "_break_") || broken == previousBreak)
                        throw new Exception($"Incorrect or repeating {element} crystal depletion.");
                    previousHit = hit; previousBreak = broken;
                }
            }
            foreach (var material in new[] { "wood", "stone", "crystal", "soft" })
            {
                AudioClip previous = null;
                for (int i = 0; i < 8; i++)
                {
                    var clip = GatheringFoley.Pick("collect_" + material);
                    if (clip == null || clip == previous || clip.length > .4f)
                        throw new Exception($"Missing, repeating or prolonged {material} pickup foley.");
                    previous = clip;
                }
            }
            if (GatheringFoley.PickupMaterial("material_flame_crystal") != "crystal" ||
                GatheringFoley.PickupMaterial("wood") != "wood" || GatheringFoley.PickupMaterial("iron_ore") != "stone" ||
                GatheringFoley.PickupMaterial("fiber") != "soft")
                throw new Exception("Material pickup routing regressed.");
        }

        private static void Test_AudioDatabase_InstanceExists()
        {
            var db = AudioDatabase.Instance;
            if (db == null)
            {
                throw new Exception("AudioDatabase.Instance returned null.");
            }
        }

        private static void Test_AudioDatabase_AutoPopulateAndCategories()
        {
            var db = AudioDatabase.Instance;
            if (db == null) throw new Exception("AudioDatabase.Instance is null.");

            db.AutoPopulateDefaults();

            if (db.ResourcesSettings == null) throw new Exception("ResourcesSettings is null.");
            if (db.Player == null) throw new Exception("PlayerSettings is null.");
            if (db.Enemies == null) throw new Exception("EnemySettings is null.");
            if (db.Combat == null) throw new Exception("CombatSettings is null.");
            if (db.Loot == null) throw new Exception("LootSettings is null.");
            if (db.UI == null) throw new Exception("UiSettings is null.");
            if (db.Music == null) throw new Exception("MusicSettings is null.");
        }

        private static void Test_AudioDatabase_GetDepletedClip_OreAndStone()
        {
            var db = AudioDatabase.Instance;
            if (db == null) throw new Exception("AudioDatabase.Instance is null.");

            db.AutoPopulateDefaults();

            var stoneClip = db.GetDepletedClip(TargetType.Stone, "Stone");
            if (stoneClip == null) throw new Exception("GetDepletedClip for Stone returned null.");

            var oreClip = db.GetDepletedClip(TargetType.Ore, "Metal");
            if (oreClip == null) throw new Exception("GetDepletedClip for Ore returned null.");

            var treeClip = db.GetDepletedClip(TargetType.Tree, "Tree");
            if (treeClip == null) throw new Exception("GetDepletedClip for Tree returned null.");
        }

        private static void Test_AudioDatabase_GetFootstepClip_Surfaces()
        {
            var db = AudioDatabase.Instance;
            if (db == null) throw new Exception("AudioDatabase.Instance is null.");

            db.AutoPopulateDefaults();

            var grassStep = db.GetFootstepClip("Grass");
            if (grassStep == null) throw new Exception("GetFootstepClip for Grass returned null.");

            var stoneStep = db.GetFootstepClip("Stone");
            if (stoneStep == null) throw new Exception("GetFootstepClip for Stone returned null.");

            var dirtStep = db.GetFootstepClip("Dirt");
            if (dirtStep == null) throw new Exception("GetFootstepClip for Dirt returned null.");

            var waterStep = db.GetFootstepClip("Water");
            if (waterStep == null) throw new Exception("GetFootstepClip for Water returned null.");

            // Tests with typed SurfaceType.
            if (db.GetFootstepClip(SurfaceType.Grass) == null) throw new Exception("GetFootstepClip(SurfaceType.Grass) is null.");
            if (db.GetFootstepClip(SurfaceType.Dirt) == null)  throw new Exception("GetFootstepClip(SurfaceType.Dirt) is null.");
            if (db.GetFootstepClip(SurfaceType.Rock) == null)  throw new Exception("GetFootstepClip(SurfaceType.Rock) is null.");
            if (db.GetFootstepClip(SurfaceType.Water) == null) throw new Exception("GetFootstepClip(SurfaceType.Water) is null.");
        }

        private static void Test_AudioDatabase_LootPickupAndDropClips()
        {
            var db = AudioDatabase.Instance;
            if (db == null) throw new Exception("AudioDatabase.Instance is null.");

            db.AutoPopulateDefaults();

            Gameplay.Loot.ItemRarity[] rarities = new[]
            {
                Gameplay.Loot.ItemRarity.Common,
                Gameplay.Loot.ItemRarity.Uncommon,
                Gameplay.Loot.ItemRarity.Rare,
                Gameplay.Loot.ItemRarity.Epic,
                Gameplay.Loot.ItemRarity.Legendary
            };

            foreach (var rarity in rarities)
            {
                var pickupClip = db.GetPickupClip(rarity);
                if (pickupClip == null)
                    throw new Exception($"GetPickupClip for {rarity} returned null.");

                float pickupVol = db.GetPickupVolume(rarity);
                if (pickupVol <= 0f || pickupVol > 1f)
                    throw new Exception($"GetPickupVolume for {rarity} is outside valid bounds: {pickupVol}");

                var dropClip = db.GetDropClip(rarity);
                if (dropClip == null)
                    throw new Exception($"GetDropClip for {rarity} returned null.");

                float dropVol = db.GetDropVolume(rarity);
                if (dropVol <= 0f || dropVol > 1f)
                    throw new Exception($"GetDropVolume for {rarity} is outside valid bounds: {dropVol}");
            }
        }

        private static void Test_AudioPreviewUtility_SafeExecution()
        {
            // Test stopping audio previews without errors
            AudioPreviewUtility.StopAll();

            var db = AudioDatabase.Instance;
            if (db != null && db.UI != null && db.UI.buttonClickClip != null)
            {
                // Play and immediately stop to test reflection call
                AudioPreviewUtility.PlayClip(db.UI.buttonClickClip);
                AudioPreviewUtility.StopAll();
            }
        }
    }
}
