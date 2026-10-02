using System;
using UnityEngine;
using UnityEditor;
using Duskborn.Inventory;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using InventorySystem.Data;

namespace Duskborn.Editor
{
    /// <summary>
    /// Automated tests for the central initial inventory database (InitialInventoryDatabase) and item utilities.
    /// </summary>
    public static class InitialInventoryDatabaseTests
    {
        [MenuItem("Duskborn/Tests/Run Initial Inventory Tests", false, 107)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_Database_InstanceExists, ref passed, ref total);
            RunTest(Test_Database_AutoPopulateDefaults, ref passed, ref total);
            RunTest(Test_Database_CreateRuntimeItems_WithQuantities, ref passed, ref total);
            RunTest(Test_Database_GearSlotMapping, ref passed, ref total);
            RunTest(Test_Database_ItemEntryQuantityClamping, ref passed, ref total);
            RunTest(Test_ResourceInventory_SyncWithInitialDatabase, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[InitialInventoryDatabaseTests] {passed}/{total} tests passed!</b></color>");
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

        private static void Test_Database_InstanceExists()
        {
            var db = InitialInventoryDatabase.Instance;
            if (db == null)
            {
                throw new Exception("InitialInventoryDatabase.Instance returned null.");
            }
        }

        private static void Test_Database_AutoPopulateDefaults()
        {
            var db = InitialInventoryDatabase.Instance;
            if (db == null) throw new Exception("InitialInventoryDatabase.Instance is null.");

            db.AutoPopulateDefaults();

            if (db.ActionBarItems.Count == 0)
                throw new Exception("ActionBarItems is empty after AutoPopulateDefaults.");

            if (db.BackpackItems.Count == 0)
                throw new Exception("BackpackItems is empty after AutoPopulateDefaults.");

            if (db.StartingGear.Count == 0)
                throw new Exception("StartingGear is empty after AutoPopulateDefaults.");
        }

        private static void Test_Database_CreateRuntimeItems_WithQuantities()
        {
            var db = InitialInventoryDatabase.Instance;
            if (db == null) throw new Exception("InitialInventoryDatabase.Instance is null.");

            var runtimeBackpack = db.CreateRuntimeBackpackItems();
            if (runtimeBackpack == null || runtimeBackpack.Count == 0)
                throw new Exception("CreateRuntimeBackpackItems returned an empty or null list.");

            var runtimeActionBar = db.CreateRuntimeActionBarItems();
            if (runtimeActionBar == null || runtimeActionBar.Count == 0)
                throw new Exception("CreateRuntimeActionBarItems returned an empty or null list.");
        }

        private static void Test_Database_GearSlotMapping()
        {
            var db = InitialInventoryDatabase.Instance;
            if (db == null) throw new Exception("InitialInventoryDatabase.Instance is null.");

            var headGear = db.FindGearForSlot(EquipmentSlot.Head);
            if (headGear != null && headGear.Slot != EquipmentSlot.Head)
            {
                throw new Exception($"Item returned for Head slot declares an incompatible slot: {headGear.Slot}");
            }
        }

        private static void Test_Database_ItemEntryQuantityClamping()
        {
            var entry = new InitialItemEntry(null, -5);
            if (entry.Quantity < 1)
            {
                throw new Exception($"Quantity was not clamped to 1 in InitialItemEntry: {entry.Quantity}");
            }
        }

        private static void Test_ResourceInventory_SyncWithInitialDatabase()
        {
            var go = new GameObject("TestResourceInventory");
            try
            {
                var resInv = go.AddComponent<ResourceInventory>();
                resInv.InitializeFromInitialDatabase();

                var db = InitialInventoryDatabase.Instance;
                if (db != null)
                {
                    foreach (var entry in db.BackpackItems)
                    {
                        if (entry?.Item is MaterialDefinition mat && !string.IsNullOrEmpty(mat.Id))
                        {
                            int count = resInv.GetCount(mat.Id);
                            if (count < entry.Quantity)
                                throw new Exception($"ResourceInventory did not synchronize {mat.Id}. Expected at least: {entry.Quantity}, got: {count}");
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
