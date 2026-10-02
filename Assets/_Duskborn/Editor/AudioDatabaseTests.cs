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
