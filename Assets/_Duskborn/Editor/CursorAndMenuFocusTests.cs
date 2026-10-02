using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Duskborn.Gameplay.Player;
using Duskborn.UI;
using InventorySystem.Bootstrap;
using InventorySystem.Core;
using InventorySystem.UI;

namespace Duskborn.Editor
{
    /// <summary>
    /// Automated tests to validate cursor focus behavior,
    /// empty-area clicks, and Escape key priority between menus and the pause menu.
    /// </summary>
    public static class CursorAndMenuFocusTests
    {
        [MenuItem("Duskborn/Tests/Run Cursor And Menu Focus Tests", false, 106)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_GameHUD_ShowStats_CloseStats, ref passed, ref total);
            RunTest(Test_IsAnyMenuOpen_ReflectsMenuStates, ref passed, ref total);
            RunTest(Test_InventoryUIManager_LastClosedFrameTracking, ref passed, ref total);
            RunTest(Test_CraftingUIManager_LastClosedFrameTracking, ref passed, ref total);
            RunTest(Test_InGameMenuController_EscapeClosesOtherMenuFirst, ref passed, ref total);
            RunTest(Test_InventoryDragController_IsDraggingPropertyExposed, ref passed, ref total);
            RunTest(Test_PlayerCameraController_PropertiesExistAndAreValid, ref passed, ref total);
            RunTest(Test_WhenMenuHidden_CursorDisappearsAndLocks, ref passed, ref total);
            RunTest(Test_PlayerCameraController_SetRotationLocked_False_LocksCursor, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[CursorAndMenuFocusTests] {passed}/{total} tests passed!</b></color>");
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
                Debug.LogError($"[FAIL] {testMethod.Method.Name}: {ex}");
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

        private static void Test_GameHUD_ShowStats_CloseStats()
        {
            var go = new GameObject("Test_GameHUD");
            try
            {
                var hud = EditModeTestSupport.AddInitialized<GameHUD>(go);
                AssertTrue(GameHUD.Instance == hud, "GameHUD.Instance must point to the created instance.");

                hud.ShowStats = true;
                AssertTrue(hud.ShowStats, "ShowStats must be true after assignment.");

                hud.CloseStats();
                AssertFalse(hud.ShowStats, "ShowStats must be false after CloseStats().");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_IsAnyMenuOpen_ReflectsMenuStates()
        {
            var go = new GameObject("Test_HUD");
            try
            {
                var hud = EditModeTestSupport.AddInitialized<GameHUD>(go);
                hud.ShowStats = false;
                AssertFalse(PlayerCameraController.IsAnyMenuOpen(), "No menu must be open initially.");

                hud.ShowStats = true;
                AssertTrue(PlayerCameraController.IsAnyMenuOpen(), "IsAnyMenuOpen must return true when hud.ShowStats is active.");

                hud.CloseStats();
                AssertFalse(PlayerCameraController.IsAnyMenuOpen(), "IsAnyMenuOpen must return false after closing stats.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_InventoryUIManager_LastClosedFrameTracking()
        {
            var go = new GameObject("Test_InventoryUI");
            try
            {
                var inv = EditModeTestSupport.AddInventory(go);
                AssertTrue(InventoryUIManager.Instance == inv, "InventoryUIManager.Instance must point to the created instance.");
                AssertTrue(inv.LastClosedFrame == -1, "Initial LastClosedFrame must be -1.");

                inv.Open();
                inv.Close();
                AssertTrue(inv.LastClosedFrame == Time.frameCount, "LastClosedFrame must update to Time.frameCount on closing.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_CraftingUIManager_LastClosedFrameTracking()
        {
            var go = new GameObject("Test_CraftingUI", typeof(Canvas));
            try
            {
                var crafting = EditModeTestSupport.AddInitialized<CraftingUIManager>(go);
                AssertTrue(CraftingUIManager.Instance == crafting, "CraftingUIManager.Instance must point to the created instance.");
                AssertTrue(crafting.LastClosedFrame == -1, "Initial LastClosedFrame must be -1.");

                var workbench = go.AddComponent<Duskborn.Gameplay.Crafting.Workbench>();
                crafting.Open(workbench);
                crafting.Close();
                AssertTrue(crafting.LastClosedFrame == Time.frameCount, "LastClosedFrame must update on closing.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_InGameMenuController_EscapeClosesOtherMenuFirst()
        {
            var goMenu = new GameObject("Test_InGameMenu");
            var goInv = new GameObject("Test_InvUI");
            try
            {
                var menu = goMenu.AddComponent<InGameMenuController>();
                var inv = EditModeTestSupport.AddInventory(goInv);

                // Simulate the inventory closing in this frame.
                inv.Open();
                inv.Close();
                AssertTrue(inv.LastClosedFrame == Time.frameCount, "Inventory must record closing in the current frame.");

                // Invoke HandleEscapeKey through reflection to check priority.
                var method = typeof(InGameMenuController).GetMethod("HandleEscapeKey", BindingFlags.NonPublic | BindingFlags.Instance);
                AssertTrue(method != null, "The HandleEscapeKey method must exist.");

                method.Invoke(menu, null);

                // Since inventory closed in this frame, the pause menu must NOT have opened!
                AssertFalse(menu.IsOpen, "The pause menu must NOT open in the same frame as another menu closes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goInv);
                UnityEngine.Object.DestroyImmediate(goMenu);
            }
        }

        private static void Test_InventoryDragController_IsDraggingPropertyExposed()
        {
            var prop = typeof(InventoryDragController).GetProperty("IsDragging", BindingFlags.Public | BindingFlags.Instance);
            AssertTrue(prop != null, "InventoryDragController must have the public IsDragging property.");

            var installerProp = typeof(InventoryInstaller).GetProperty("IsDraggingItem", BindingFlags.Public | BindingFlags.Instance);
            AssertTrue(installerProp != null, "InventoryInstaller must have the public IsDraggingItem property.");
        }

        private static void Test_PlayerCameraController_PropertiesExistAndAreValid()
        {
            var propJustLocked = typeof(PlayerCameraController).GetProperty("JustLockedCursorThisFrame", BindingFlags.Public | BindingFlags.Instance);
            AssertTrue(propJustLocked != null, "PlayerCameraController must have JustLockedCursorThisFrame.");

            var methodIsAnyMenuOpen = typeof(PlayerCameraController).GetMethod("IsAnyMenuOpen", BindingFlags.Public | BindingFlags.Static);
            AssertTrue(methodIsAnyMenuOpen != null, "PlayerCameraController must have the static IsAnyMenuOpen method.");
        }

        private static void Test_WhenMenuHidden_CursorDisappearsAndLocks()
        {
            var goMenu = new GameObject("Test_PauseMenu");
            var goCamera = new GameObject("Test_MenuCamera");
            var previousCamera = PlayerCameraController.LocalInstance;
            try
            {
                var camera = goCamera.AddComponent<PlayerCameraController>();
                PlayerCameraController.LocalInstance = camera;
                var menu = goMenu.AddComponent<InGameMenuController>();
                menu.OpenMenu();
                AssertTrue(menu.IsOpen && camera.IsRotationLocked, "An open menu must block rotation.");
                EditModeTestSupport.AssertCursorRequested(camera, false);

                menu.CloseMenu();
                AssertFalse(menu.IsOpen || camera.IsRotationLocked, "A closed menu must release rotation.");
                EditModeTestSupport.AssertCursorRequested(camera, true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goMenu);
                PlayerCameraController.LocalInstance = previousCamera;
                UnityEngine.Object.DestroyImmediate(goCamera);
            }
        }

        private static void Test_PlayerCameraController_SetRotationLocked_False_LocksCursor()
        {
            var goCam = new GameObject("Test_CamGO");
            try
            {
                var cam = goCam.AddComponent<PlayerCameraController>();
                cam.SetRotationLocked(true);
                AssertTrue(cam.IsRotationLocked, "The camera must block rotation.");
                EditModeTestSupport.AssertCursorRequested(cam, false);

                cam.SetRotationLocked(false);
                AssertFalse(cam.IsRotationLocked, "The camera must release rotation.");
                EditModeTestSupport.AssertCursorRequested(cam, true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goCam);
            }
        }
    }
}
