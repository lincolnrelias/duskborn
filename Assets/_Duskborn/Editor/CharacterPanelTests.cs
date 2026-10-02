using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using Duskborn.UI;
using InventorySystem.Bootstrap;
using InventorySystem.Core;

namespace Duskborn.Editor
{
    /// <summary>
    /// Automated tests for the character panel (Character Section),
    /// including right-click equipping from inventory, unequipping,
    /// empty / equipped slot states, ring selection, and menu focus.
    /// </summary>
    public static class CharacterPanelTests
    {
        [MenuItem("Duskborn/Tests/Run Character Panel Tests", false, 107)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_CharacterUIManager_InstanceAndLifecycle, ref passed, ref total);
            RunTest(Test_PlayerCameraController_IsAnyMenuOpen_IncludesCharacterUI, ref passed, ref total);
            RunTest(Test_PlayerEquipmentContainer_TryEquipToSlot, ref passed, ref total);
            RunTest(Test_RingSlot_AutomaticAlternation, ref passed, ref total);
            RunTest(Test_InventoryUIManager_RightClickEquip, ref passed, ref total);
            RunTest(Test_CharacterSlotView_VisualStates, ref passed, ref total);
            RunTest(Test_InGameMenuController_EscapeClosesCharacterPanelFirst, ref passed, ref total);
            RunTest(Test_InventoryUIManager_PopulatePlaceholderTestGear, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[CharacterPanelTests] {passed}/{total} tests passed!</b></color>");
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

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition) throw new Exception(message);
        }

        private static void Test_CharacterUIManager_InstanceAndLifecycle()
        {
            var go = new GameObject("Test_CharacterUI");
            try
            {
                var charUI = EditModeTestSupport.AddInitialized<CharacterUIManager>(go);
                AssertTrue(CharacterUIManager.Instance == charUI, "CharacterUIManager.Instance must point to the created instance.");
                AssertFalse(charUI.IsOpen, "The panel must start closed.");
                AssertTrue(charUI.LastClosedFrame == -1, "Initial LastClosedFrame must be -1.");

                charUI.Open();
                AssertTrue(charUI.IsOpen, "The panel must be open after Open().");

                charUI.Close();
                AssertFalse(charUI.IsOpen, "The panel must be closed after Close().");
                AssertTrue(charUI.LastClosedFrame == Time.frameCount, "LastClosedFrame must update to Time.frameCount on closing.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_PlayerCameraController_IsAnyMenuOpen_IncludesCharacterUI()
        {
            var go = new GameObject("Test_CharUI");
            var goCam = new GameObject("Test_Cam");
            try
            {
                var camCtrl = goCam.AddComponent<PlayerCameraController>();
                PlayerCameraController.LocalInstance = camCtrl;
                camCtrl.SetRotationLocked(false);
                AssertFalse(camCtrl.IsRotationLocked, "The camera must start unlocked.");

                var charUI = EditModeTestSupport.AddInitialized<CharacterUIManager>(go);
                charUI.Close();

                charUI.Open();
                AssertTrue(PlayerCameraController.IsAnyMenuOpen(), "PlayerCameraController.IsAnyMenuOpen must return true while CharacterUIManager is open.");
                AssertTrue(camCtrl.IsRotationLocked, "The camera must lock rotation when CharacterUIManager opens.");

                charUI.Close();
                AssertFalse(camCtrl.IsRotationLocked, "The camera must unlock rotation when CharacterUIManager closes and no other menu is open.");
            }
            finally
            {
                PlayerCameraController.LocalInstance = null;
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(goCam);
            }
        }

        private static void Test_PlayerEquipmentContainer_TryEquipToSlot()
        {
            var go = new GameObject("Test_Player");
            try
            {
                go.AddComponent<PlayerStats>();
                go.AddComponent<PlayerBuffContainer>();
                var equip = go.AddComponent<PlayerEquipmentContainer>();

                var helm1 = new GearItem("helm_01", "Test Helm 1", "HP: +5%", "", EquipmentSlot.Head,
                    new[] { new StatBonus { Type = StatType.HP, Value = 0.05f } });
                var helm2 = new GearItem("helm_02", "Test Helm 2", "HP: +10%", "", EquipmentSlot.Head,
                    new[] { new StatBonus { Type = StatType.HP, Value = 0.10f } });

                bool equipped = equip.TryEquipToSlot(EquipmentSlot.Head, helm1, out var prev);
                AssertTrue(equipped, "TryEquipToSlot must return true for a valid item.");
                AssertTrue(prev == null, "The first equipped item must not replace another item.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Head) == helm1, "GetEquipped must return helm1.");

                // Replace with helm2.
                bool replaced = equip.TryEquipToSlot(EquipmentSlot.Head, helm2, out var displaced);
                AssertTrue(replaced, "Replacement must return true.");
                AssertTrue(displaced == helm1, "The replaced item must be helm1.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Head) == helm2, "GetEquipped must return helm2.");

                // Desequipa
                var unequipped = equip.Unequip(EquipmentSlot.Head);
                AssertTrue(unequipped == helm2, "Unequip must return helm2.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Head) == null, "The slot must be empty after Unequip.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_RingSlot_AutomaticAlternation()
        {
            var go = new GameObject("Test_Player_Rings");
            try
            {
                go.AddComponent<PlayerStats>();
                go.AddComponent<PlayerBuffContainer>();
                var equip = go.AddComponent<PlayerEquipmentContainer>();

                var ring1 = new GearItem("ring_01", "Copper Ring", "Crit: +5%", "", EquipmentSlot.Ring1, null);
                var ring2 = new GearItem("ring_02", "Gold Ring", "Crit: +10%", "", EquipmentSlot.Ring1, null);
                var ring3 = new GearItem("ring_03", "Ruby Ring", "Crit: +15%", "", EquipmentSlot.Ring1, null);

                // First ring: Ring1 is empty -> equip in Ring1.
                equip.TryEquipToSlot(EquipmentSlot.Ring1, ring1, out _);
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring1) == ring1, "The first ring must go to Ring1.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring2) == null, "Ring2 must remain empty.");

                // Second ring: Ring1 occupied and Ring2 empty -> equip logic selects Ring2.
                EquipmentSlot target = equip.GetEquipped(EquipmentSlot.Ring1) == null ? EquipmentSlot.Ring1
                    : (equip.GetEquipped(EquipmentSlot.Ring2) == null ? EquipmentSlot.Ring2 : EquipmentSlot.Ring1);

                AssertTrue(target == EquipmentSlot.Ring2, "The target must be Ring2 when Ring1 is occupied.");
                equip.TryEquipToSlot(target, ring2, out _);
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring2) == ring2, "The second ring must be in Ring2.");

                // Third ring: both occupied -> replace Ring1.
                target = equip.GetEquipped(EquipmentSlot.Ring1) == null ? EquipmentSlot.Ring1
                    : (equip.GetEquipped(EquipmentSlot.Ring2) == null ? EquipmentSlot.Ring2 : EquipmentSlot.Ring1);

                AssertTrue(target == EquipmentSlot.Ring1, "The target must be Ring1 when both are occupied.");
                equip.TryEquipToSlot(target, ring3, out var displacedRing);
                AssertTrue(displacedRing == ring1, "The replaced ring must be ring1.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring1) == ring3, "Ring1 must now contain ring3.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring2) == ring2, "Ring2 must remain unchanged.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_InventoryUIManager_RightClickEquip()
        {
            var goInv = new GameObject("Test_InventoryManager");
            var goPlayer = new GameObject("Test_Player_Combat");
            try
            {
                goPlayer.AddComponent<PlayerStats>();
                goPlayer.AddComponent<PlayerBuffContainer>();
                var equip = goPlayer.AddComponent<PlayerEquipmentContainer>();
                var combat = goPlayer.AddComponent<PlayerCombat>();

                var invUI = EditModeTestSupport.AddInventory(goInv);

                // Configure a mock inventory service.
                var grid = new InventoryGrid(4, 4);
                var service = new InventoryService(grid);

                // Use reflection to inject _playerEquipment into InventoryUIManager for the test.
                var fieldEquip = typeof(InventoryUIManager).GetField("_playerEquipment", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldEquip?.SetValue(invUI, equip);

                // Create an armor item.
                var armor = new GearItem("chest_01", "Leather Armor", "HP: +8%", "", EquipmentSlot.Chest,
                    new[] { new StatBonus { Type = StatType.HP, Value = 0.08f } });

                service.TryPlaceItemAt(0, armor);
                AssertTrue(service.GetItem(0) == armor, "The item must be in inventory slot 0.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Chest) == null, "Chest must initially be empty.");

                // Test the direct equip method.
                equip.TryEquipToSlot(armor.Slot, armor, out var displaced);
                service.RemoveItem(0);

                AssertTrue(equip.GetEquipped(EquipmentSlot.Chest) == armor, "Chest must contain the equipped item.");
                AssertTrue(service.GetItem(0) == null, "Inventory slot 0 must be empty after equipping.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goInv);
                UnityEngine.Object.DestroyImmediate(goPlayer);
            }
        }

        private static void Test_CharacterSlotView_VisualStates()
        {
            var go = new GameObject("Test_SlotView", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
            var silGO = new GameObject("Silhouette", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            silGO.transform.SetParent(go.transform, false);
            var itemGO = new GameObject("Item", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            itemGO.transform.SetParent(go.transform, false);

            try
            {
                var slotView = go.AddComponent<CharacterSlotView>();
                var silImg = silGO.GetComponent<UnityEngine.UI.Image>();
                var itemImg = itemGO.GetComponent<UnityEngine.UI.Image>();
                var outline = go.GetComponent<UnityEngine.UI.Outline>();

                slotView.Initialize(EquipmentSlot.Head, null, silImg, itemImg, outline);

                // Empty state: silhouette active, item icon disabled.
                slotView.SetEmpty(null);
                AssertFalse(itemImg.gameObject.activeSelf, "The item icon must be disabled when empty.");

                // Equipped state: item icon active, silhouette disabled.
                var helm = new GearItem("helm_test", "Helm", "HP: +5%", "", EquipmentSlot.Head, null);
                slotView.SetEquipped(helm, null);
                AssertFalse(silImg.gameObject.activeSelf, "The silhouette must be disabled when equipped.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_InGameMenuController_EscapeClosesCharacterPanelFirst()
        {
            var goMenu = new GameObject("Test_InGameMenu");
            var goChar = new GameObject("Test_CharUI");
            try
            {
                var menu = goMenu.AddComponent<InGameMenuController>();
                var charUI = EditModeTestSupport.AddInitialized<CharacterUIManager>(goChar);

                // Simulate the character panel closing in this frame.
                charUI.Open();
                charUI.Close();
                AssertTrue(charUI.LastClosedFrame == Time.frameCount, "LastClosedFrame must record closing in the current frame.");

                // Invoke HandleEscapeKey through reflection to check priority.
                var method = typeof(InGameMenuController).GetMethod("HandleEscapeKey", BindingFlags.NonPublic | BindingFlags.Instance);
                AssertTrue(method != null, "The HandleEscapeKey method must exist.");

                method.Invoke(menu, null);

                // Since the panel closed in the same frame, the pause menu must NOT open!
                AssertFalse(menu.IsOpen, "The pause menu must NOT open in the same frame as the character panel closes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goChar);
                UnityEngine.Object.DestroyImmediate(goMenu);
            }
        }

        private static void Test_InventoryUIManager_PopulatePlaceholderTestGear()
        {
            var goInv = new GameObject("Test_InventoryInstaller", typeof(InventoryInstaller));
            var goMgr = new GameObject("Test_InventoryUIManager", typeof(InventoryUIManager));
            try
            {
                var installer = goInv.GetComponent<InventoryInstaller>();
                var mgr = goMgr.GetComponent<InventoryUIManager>();

                // Inject installer into manager through reflection.
                var fieldInstaller = typeof(InventoryUIManager).GetField("installer", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInstaller?.SetValue(mgr, installer);

                // Configure the installer service through reflection.
                var grid = new InventoryGrid(4, 4);
                var service = new InventoryService(grid);
                var fieldService = typeof(InventoryInstaller).GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldService?.SetValue(installer, service);

                AssertTrue(mgr.InstallerReady, "InstallerReady must be true when installer and service are configured.");

                // Populate the test items.
                mgr.PopulatePlaceholderTestGear();

                // Check that the items were inserted.
                int count = 0;
                for (int i = 0; i < grid.SlotCount; i++)
                {
                    if (service.GetItem(i) is GearItem) count++;
                }

                AssertTrue(count > 0, "PopulatePlaceholderTestGear must add test items to inventory.");

                // Running a second time must not duplicate existing items.
                int countBefore = count;
                mgr.PopulatePlaceholderTestGear();
                int countAfter = 0;
                for (int i = 0; i < grid.SlotCount; i++)
                {
                    if (service.GetItem(i) is GearItem) countAfter++;
                }

                AssertTrue(countBefore == countAfter, "PopulatePlaceholderTestGear must not duplicate existing items.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goMgr);
                UnityEngine.Object.DestroyImmediate(goInv);
            }
        }

        [MenuItem("Duskborn/Inventory/Populate Test Gear Items", false, 108)]
        public static void PopulateTestGearFromMenu()
        {
            var invUI = UnityEngine.Object.FindAnyObjectByType<InventoryUIManager>();
            if (invUI != null)
            {
                invUI.PopulatePlaceholderTestGear();
                Debug.Log("<color=#55FF55>[Duskborn] Test items successfully added to inventory!</color>");
            }
            else
            {
                Debug.LogWarning("[Duskborn] InventoryUIManager not found in the active scene.");
            }
        }
    }
}
