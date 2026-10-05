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

            RunTest(Test_DropsScatterAndReleaseIndependently, ref passed, ref total);
            RunTest(Test_NodeContactAndBoundedDebris, ref passed, ref total);
            RunTest(Test_HarvestAudioDistinctAndNonRepeating, ref passed, ref total);
            RunTest(Test_WeightedLaunchAndRepeatedSetup, ref passed, ref total);
            RunTest(Test_CollectionTakesOverPhysicsAndHover, ref passed, ref total);
            RunTest(Test_HoverCatchPreservesMomentumAndFrameRate, ref passed, ref total);
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

        private static void Test_DropsScatterAndReleaseIndependently()
        {
            var go = new GameObject("Scatter regression");
            try
            {
                var manager = go.AddComponent<LootManager>();
                var rng = new Duskborn.Core.SeededRNG(3719);
                var replay = new Duskborn.Core.SeededRNG(3719);
                float minSpeed = float.MaxValue, maxSpeed = 0, minRadius = float.MaxValue, maxRadius = 0;
                float minDelay = float.MaxValue, maxDelay = 0;
                var angles = new List<float>();
                for (int i = 0; i < 5; i++)
                {
                    var launch = manager.CreateDropLaunch(Vector3.zero, i, 5, 0, rng);
                    var again = manager.CreateDropLaunch(Vector3.zero, i, 5, 0, replay);
                    if (launch.Velocity != again.Velocity || launch.Position != again.Position || launch.ReleaseDelay != again.ReleaseDelay)
                        throw new Exception("Seeded launch plan is not reproducible.");
                    float radius = new Vector2(launch.Position.x, launch.Position.z).magnitude;
                    minRadius = Mathf.Min(minRadius, radius); maxRadius = Mathf.Max(maxRadius, radius);
                    minSpeed = Mathf.Min(minSpeed, launch.Velocity.magnitude); maxSpeed = Mathf.Max(maxSpeed, launch.Velocity.magnitude);
                    if (launch.ReleaseDelay < 0 || launch.ReleaseDelay > .32f || radius > .251f)
                        throw new Exception("Cascade exceeded bounded time/spread.");
                    if (i == 0 && launch.ReleaseDelay != 0) throw new Exception("First drop must release immediately.");
                    if (i > 0) minDelay = Mathf.Min(minDelay, launch.ReleaseDelay);
                    maxDelay = Mathf.Max(maxDelay, launch.ReleaseDelay);
                    angles.Add(launch.Azimuth * Mathf.Rad2Deg % 360f);
                }
                angles.Sort();
                float minGap = 360f, maxGap = 0;
                for (int i = 0; i < 5; i++)
                {
                    float gap = (angles[(i + 1) % 5] - angles[i] + 360f) % 360f;
                    minGap = Mathf.Min(minGap, gap); maxGap = Mathf.Max(maxGap, gap);
                }
                if (maxGap - minGap < 30f || maxSpeed - minSpeed < .4f || maxRadius - minRadius < .03f || maxDelay - minDelay < .03f)
                    throw new Exception("Five drops still share regular angles, speed, radius, or release time.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void Test_NodeContactAndBoundedDebris()
        {
            var nodeObject = new GameObject("Node contact regression");
            try
            {
                nodeObject.transform.position = new Vector3(10, 0, 10);
                var collider = nodeObject.AddComponent<BoxCollider>();
                collider.center = Vector3.up * .5f; collider.size = new Vector3(2, 1, 2);
                var node = nodeObject.AddComponent<ResourceNode>();
                Physics.SyncTransforms();
                Vector3 point = node.GetHitPosition(new Vector3(10, .5f, 15));
                if (Vector3.Distance(point, new Vector3(10, .5f, 11)) > .001f)
                    throw new Exception("Node contact feedback must be placed on the struck surface.");
                for (int i = 0; i < 40; i++)
                    Duskborn.Effects.ResourceHitFeedback.Emit(point, Vector3.forward, "Metal", false, i % 2 == 0);
                var root = Duskborn.Effects.ResourceHitFeedback.Root;
                if (root == null || root.transform.parent != null)
                    throw new Exception("Debris must survive independently of a depleted node.");
                var systems = root.GetComponentsInChildren<ParticleSystem>();
                if (systems.Length != Duskborn.Effects.ResourceHitFeedback.SiteCapacity * 2)
                    throw new Exception("Repeated hits allocated more particle sites than the pool permits.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(nodeObject);
                if (Duskborn.Effects.ResourceHitFeedback.Root != null)
                    UnityEngine.Object.DestroyImmediate(Duskborn.Effects.ResourceHitFeedback.Root);
            }
        }

        private static void Test_HarvestAudioDistinctAndNonRepeating()
        {
            var settings = AudioDatabase.Instance.ResourcesSettings;
            foreach (var bank in new[] { settings.woodHarvestClips, settings.stoneHarvestClips, settings.oreHarvestClips, settings.foliageHarvestClips })
            {
                if (bank == null || bank.Length < 4) throw new Exception("Harvest material needs four distinct performances.");
                var seen = new HashSet<AudioClip>();
                foreach (var clip in bank)
                {
                    if (clip == null || !seen.Add(clip) || clip.channels != 1 || clip.frequency != 48000)
                        throw new Exception("Harvest recordings are missing, duplicated, or incorrectly imported.");
                }
                AudioClip previous = null;
                for (int i = 0; i < 24; i++)
                {
                    AudioClip before = previous;
                    var selected = ResourceAudioSettings.PickWithoutRepeat(bank, ref previous);
                    if (selected == null || selected == before) throw new Exception("Adjacent node hits repeated the same recording.");
                }
            }
        }


        private static void Test_CollectionTakesOverPhysicsAndHover()
        {
            var go = new GameObject("Collection pose regression");
            try
            {
                var body = go.AddComponent<Rigidbody>();
                var visuals = go.AddComponent<DroppedItemVisuals>();
                visuals.Setup(ItemRarity.Epic);
                var position = new Vector3(2f, 3f, 4f);
                go.transform.position = position;
                visuals.BeginCollectionMotion();
                if (body.interpolation != RigidbodyInterpolation.None || !body.isKinematic || body.useGravity ||
                    go.transform.position != position)
                    throw new Exception("Collection must own the visible pose without a physics interpolation jump.");
                var hover = typeof(DroppedItemVisuals).GetMethod("UpdateIdleHover",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                for (int frame = 0; frame < 10; frame++)
                {
                    position += Vector3.right * .1f;
                    go.transform.position = position;
                    hover.Invoke(visuals, new object[] { 10f + frame / 120f });
                    if (go.transform.position != position)
                        throw new Exception("Idle hovering overwrote a collection frame.");
                }
                visuals.Setup(ItemRarity.Rare);
                if (!body.isKinematic || body.interpolation != RigidbodyInterpolation.None)
                    throw new Exception("Delayed rarity synchronization reactivated physics during collection.");
                visuals.BeginDropMotion(Vector3.up, Vector3.zero);
                if (body.isKinematic || !body.useGravity || body.interpolation != RigidbodyInterpolation.Interpolate)
                    throw new Exception("A cancelled collection must restore interpolated drop physics.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void Test_WeightedLaunchAndRepeatedSetup()
        {
            var go = new GameObject("Weighted drop regression");
            try
            {
                var body = go.AddComponent<Rigidbody>();
                var collider = go.AddComponent<BoxCollider>();
                var visuals = go.AddComponent<DroppedItemVisuals>();
                visuals.Setup(ItemRarity.Epic);
                body.isKinematic = true;
                body.useGravity = false;
                visuals.Setup(ItemRarity.Epic);
                if (!body.isKinematic || body.useGravity)
                    throw new Exception("Repeated rarity sync released a caught item.");
                Vector3 launch = new Vector3(2f, 4f, 0f);
                Vector3 tumble = new Vector3(1f, 2f, 3f);
                visuals.BeginDropMotion(launch, tumble);
                if (body.isKinematic || !body.useGravity || body.linearVelocity != launch || body.angularVelocity != tumble)
                    throw new Exception("Rethrow must restore launch momentum and free physics.");
                if (body.linearDamping > 0.2f || body.interpolation != RigidbodyInterpolation.Interpolate)
                    throw new Exception("Launch must preserve its arc and interpolate between physics ticks.");
                if (collider.sharedMaterial == null || collider.sharedMaterial.dynamicFriction < 0.4f)
                    throw new Exception("Drop contact must resist sliding.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void Test_HoverCatchPreservesMomentumAndFrameRate()
        {
            Vector3 target = new Vector3(0f, 0.8f, 0f);
            Vector3 start = target + Vector3.up * 0.1f;
            Vector3 incoming = new Vector3(2f, -4f, 0f);
            Vector3 position = start, velocity = incoming;
            DroppedItemVisuals.StepHoverSpring(ref position, ref velocity, target, 1f / 120f);
            if (position.y >= start.y || position.x <= start.x || velocity.sqrMagnitude < 0.1f)
                throw new Exception("Hover catch erased incoming momentum instead of braking it.");
            Vector3 at30 = start, speed30 = incoming, at120 = start, speed120 = incoming;
            bool undershot = false;
            for (int i = 0; i < 30; i++)
            {
                DroppedItemVisuals.StepHoverSpring(ref at30, ref speed30, target, 1f / 30f);
                undershot |= at30.y < target.y;
            }
            for (int i = 0; i < 120; i++)
                DroppedItemVisuals.StepHoverSpring(ref at120, ref speed120, target, 1f / 120f);
            if (!undershot || Vector3.Distance(at30, target) > 0.001f || speed30.magnitude > 0.01f)
                throw new Exception("Hover catch must overshoot slightly and settle within one second.");
            if (Vector3.Distance(at30, at120) > 0.00001f || Vector3.Distance(speed30, speed120) > 0.00001f)
                throw new Exception("Hover catch varies with rendering frame rate.");
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
