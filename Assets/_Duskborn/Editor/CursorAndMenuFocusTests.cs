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
    /// Testes automatizados para validação do comportamento de foco do cursor,
    /// clique em área vazia e hierarquia da tecla Escape entre menus e o menu de pausa.
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

            Debug.Log($"<color=#55FF55><b>[CursorAndMenuFocusTests] {passed}/{total} testes passaram com sucesso!</b></color>");
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

        private static void Test_GameHUD_ShowStats_CloseStats()
        {
            var go = new GameObject("Test_GameHUD");
            try
            {
                var hud = go.AddComponent<GameHUD>();
                AssertTrue(GameHUD.Instance == hud, "GameHUD.Instance deve apontar para a instância criada.");

                hud.ShowStats = true;
                AssertTrue(hud.ShowStats, "ShowStats deve ser true após atribuição.");

                hud.CloseStats();
                AssertFalse(hud.ShowStats, "ShowStats deve ser false após CloseStats().");
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
                var hud = go.AddComponent<GameHUD>();
                hud.ShowStats = false;
                AssertFalse(PlayerCameraController.IsAnyMenuOpen(), "Nenhum menu deve estar aberto inicialmente.");

                hud.ShowStats = true;
                AssertTrue(PlayerCameraController.IsAnyMenuOpen(), "IsAnyMenuOpen deve retornar true quando hud.ShowStats estiver ativo.");

                hud.CloseStats();
                AssertFalse(PlayerCameraController.IsAnyMenuOpen(), "IsAnyMenuOpen deve retornar false após fechar stats.");
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
                var inv = go.AddComponent<InventoryUIManager>();
                AssertTrue(InventoryUIManager.Instance == inv, "InventoryUIManager.Instance deve apontar para a instância criada.");
                AssertTrue(inv.LastClosedFrame == -1, "LastClosedFrame inicial deve ser -1.");

                inv.Close();
                AssertTrue(inv.LastClosedFrame == Time.frameCount, "LastClosedFrame deve ser atualizado para Time.frameCount ao fechar.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_CraftingUIManager_LastClosedFrameTracking()
        {
            var go = new GameObject("Test_CraftingUI");
            try
            {
                var crafting = go.AddComponent<CraftingUIManager>();
                AssertTrue(CraftingUIManager.Instance == crafting, "CraftingUIManager.Instance deve apontar para a instância criada.");
                AssertTrue(crafting.LastClosedFrame == -1, "LastClosedFrame inicial deve ser -1.");

                crafting.Close();
                AssertTrue(crafting.LastClosedFrame == Time.frameCount, "LastClosedFrame deve ser atualizado ao fechar.");
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
                var inv = goInv.AddComponent<InventoryUIManager>();

                // Simula que o inventário acabou de fechar neste frame
                inv.Close();
                AssertTrue(inv.LastClosedFrame == Time.frameCount, "Inventário deve registrar fechamento no frame atual.");

                // Invoca HandleEscapeKey via Reflection para verificar a prioridade
                var method = typeof(InGameMenuController).GetMethod("HandleEscapeKey", BindingFlags.NonPublic | BindingFlags.Instance);
                AssertTrue(method != null, "Método HandleEscapeKey deve existir.");

                method.Invoke(menu, null);

                // Como o inventário fechou neste frame, o menu de pausa NÃO deve ter aberto!
                AssertFalse(menu.IsOpen, "Menu de pausa NÃO deve abrir no mesmo frame em que outro menu foi fechado.");
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
            AssertTrue(prop != null, "InventoryDragController deve possuir a propriedade pública IsDragging.");

            var installerProp = typeof(InventoryInstaller).GetProperty("IsDraggingItem", BindingFlags.Public | BindingFlags.Instance);
            AssertTrue(installerProp != null, "InventoryInstaller deve possuir a propriedade pública IsDraggingItem.");
        }

        private static void Test_PlayerCameraController_PropertiesExistAndAreValid()
        {
            var propJustLocked = typeof(PlayerCameraController).GetProperty("JustLockedCursorThisFrame", BindingFlags.Public | BindingFlags.Instance);
            AssertTrue(propJustLocked != null, "PlayerCameraController deve possuir JustLockedCursorThisFrame.");

            var methodIsAnyMenuOpen = typeof(PlayerCameraController).GetMethod("IsAnyMenuOpen", BindingFlags.Public | BindingFlags.Static);
            AssertTrue(methodIsAnyMenuOpen != null, "PlayerCameraController deve possuir o método estático IsAnyMenuOpen.");
        }

        private static void Test_WhenMenuHidden_CursorDisappearsAndLocks()
        {
            var goMenu = new GameObject("Test_PauseMenu");
            try
            {
                var menu = goMenu.AddComponent<InGameMenuController>();
                menu.OpenMenu();
                AssertTrue(Cursor.lockState == CursorLockMode.None, "Cursor deve estar livre com o menu de pausa aberto.");
                AssertTrue(Cursor.visible, "Cursor deve estar visível com o menu de pausa aberto.");

                menu.CloseMenu();
                AssertTrue(Cursor.lockState == CursorLockMode.Locked, "Cursor deve sumir e travar ao fechar o menu de pausa.");
                AssertFalse(Cursor.visible, "Cursor.visible deve ser false ao fechar o menu de pausa.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goMenu);
            }
        }

        private static void Test_PlayerCameraController_SetRotationLocked_False_LocksCursor()
        {
            var goCam = new GameObject("Test_CamGO");
            try
            {
                var cam = goCam.AddComponent<PlayerCameraController>();
                cam.SetRotationLocked(true);
                AssertTrue(Cursor.lockState == CursorLockMode.None, "Cursor deve estar livre quando a rotação da câmera está travada.");
                AssertTrue(Cursor.visible, "Cursor deve estar visível quando a rotação da câmera está travada.");

                cam.SetRotationLocked(false);
                AssertTrue(Cursor.lockState == CursorLockMode.Locked, "Cursor deve travar e sumir quando a rotação da câmera é liberada.");
                AssertFalse(Cursor.visible, "Cursor deve ficar invisível quando a rotação da câmera é liberada.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goCam);
            }
        }
    }
}
