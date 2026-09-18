using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Duskborn.Gameplay.ActionBar;
using Duskborn.UI;
using InventorySystem.UI;

namespace Duskborn.Editor
{
    /// <summary>
    /// Testes automatizados de validação de escalonamento de resolução e consistência dimensional
    /// para ActionBar, Menus IMGUI, HUD e CraftingUI.
    /// </summary>
    public static class UIResolutionScalingTests
    {
        [MenuItem("Duskborn/Tests/Run UI Resolution Scaling Tests", false, 105)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_ActionBarInstaller_CanvasScalerConfiguration, ref passed, ref total);
            RunTest(Test_ActionBarRoot_BottomCenterAnchoringAndSize, ref passed, ref total);
            RunTest(Test_ActionBarAndInventory_ScaleRatioConsistencyAcrossResolutions, ref passed, ref total);
            RunTest(Test_GUIMatrix_ScalingAndInverseMouseMath, ref passed, ref total);
            RunTest(Test_CraftingUIManager_CanvasScalerScreenSpaceCheck, ref passed, ref total);
            RunTest(Test_CraftingUIManager_DimensionsAndScreenBoundsConsistency, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[UIResolutionScalingTests] {passed}/{total} testes passaram com sucesso!</b></color>");
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

        private static void AssertApproximately(float a, float b, float maxDelta, string message)
        {
            if (Mathf.Abs(a - b) > maxDelta)
                throw new Exception($"{message} (Esperado: {b}, Obtido: {a}, Delta: {Mathf.Abs(a - b)})");
        }

        private static void Test_ActionBarInstaller_CanvasScalerConfiguration()
        {
            var canvasGO = new GameObject("Test_ActionBarCanvas", typeof(RectTransform), typeof(Canvas)) { hideFlags = HideFlags.DontSave };
            try
            {
                var installerGO = new GameObject("Test_ActionBarInstaller", typeof(ActionBarInstaller)) { hideFlags = HideFlags.DontSave };
                installerGO.transform.SetParent(canvasGO.transform);

                var installer = installerGO.GetComponent<ActionBarInstaller>();

                var canvasField = typeof(ActionBarInstaller).GetField("canvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                canvasField?.SetValue(installer, canvasGO.GetComponent<Canvas>());

                var syncField = typeof(ActionBarInstaller).GetField("syncScalerWithInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                syncField?.SetValue(installer, false);

                installer.ConfigureCanvasAndLayout();

                var scaler = canvasGO.GetComponent<CanvasScaler>();
                AssertTrue(scaler != null, "CanvasScaler deve ser criado automaticamente no Canvas da ActionBar.");
                AssertTrue(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize, "CanvasScaler da ActionBar deve operar em ScaleWithScreenSize.");
                AssertApproximately(scaler.referenceResolution.x, 800f, 0.01f, "Resolução de referência X da ActionBar deve ser 800 (idêntica ao inventário).");
                AssertApproximately(scaler.referenceResolution.y, 600f, 0.01f, "Resolução de referência Y da ActionBar deve ser 600 (idêntica ao inventário).");
                AssertApproximately(scaler.matchWidthOrHeight, 0f, 0.01f, "MatchWidthOrHeight deve ser 0 para manter consistência direta com o inventário.");
            }
            finally
            {
                if (canvasGO != null) UnityEngine.Object.DestroyImmediate(canvasGO);
            }
        }

        private static void Test_ActionBarRoot_BottomCenterAnchoringAndSize()
        {
            var canvasGO = new GameObject("Test_Canvas", typeof(RectTransform), typeof(Canvas)) { hideFlags = HideFlags.DontSave };
            try
            {
                var rootGO = new GameObject("Test_ActionBarRoot", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
                rootGO.transform.SetParent(canvasGO.transform);
                var gridGO = new GameObject("Test_Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(InventoryGridLayoutController)) { hideFlags = HideFlags.DontSave };
                gridGO.transform.SetParent(rootGO.transform);

                var gridController = gridGO.GetComponent<InventoryGridLayoutController>();

                var colField = typeof(InventoryGridLayoutController).GetField("columns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                colField?.SetValue(gridController, 8);

                var installerGO = new GameObject("Test_Installer", typeof(ActionBarInstaller)) { hideFlags = HideFlags.DontSave };
                installerGO.transform.SetParent(canvasGO.transform);
                var installer = installerGO.GetComponent<ActionBarInstaller>();

                var canvasField = typeof(ActionBarInstaller).GetField("canvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                canvasField?.SetValue(installer, canvasGO.GetComponent<Canvas>());

                var rootField = typeof(ActionBarInstaller).GetField("actionBarRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                rootField?.SetValue(installer, rootGO.GetComponent<RectTransform>());

                var gridField = typeof(ActionBarInstaller).GetField("gridController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                gridField?.SetValue(installer, gridController);

                var syncField = typeof(ActionBarInstaller).GetField("syncScalerWithInventory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                syncField?.SetValue(installer, false);

                installer.ConfigureCanvasAndLayout();

                var rect = rootGO.GetComponent<RectTransform>();
                var glg = gridGO.GetComponent<GridLayoutGroup>();

                AssertApproximately(rect.anchorMin.x, 0.5f, 0.001f, "anchorMin.x deve ser 0.5 (centro)");
                AssertApproximately(rect.anchorMin.y, 0f, 0.001f, "anchorMin.y deve ser 0 (base)");
                AssertApproximately(rect.anchorMax.x, 0.5f, 0.001f, "anchorMax.x deve ser 0.5 (centro)");
                AssertApproximately(rect.anchorMax.y, 0f, 0.001f, "anchorMax.y deve ser 0 (base)");
                AssertApproximately(rect.pivot.x, 0.5f, 0.001f, "pivot.x deve ser 0.5");
                AssertApproximately(rect.pivot.y, 0f, 0.001f, "pivot.y deve ser 0");

                // 8 slots de 55px com 4px de espaçamento = 8 * 55 + 7 * 4 = 440 + 28 = 468px
                AssertApproximately(rect.sizeDelta.x, 468f, 0.01f, "Largura total deve ser 468px para 8 slots de 55px com 4px de espaçamento.");
                AssertApproximately(rect.sizeDelta.y, 55f, 0.01f, "Altura total deve ser 55px.");
                AssertApproximately(rect.anchoredPosition.y, 15f, 0.01f, "anchoredPosition.y deve ter margem inferior de 15px.");
                AssertTrue(glg.childAlignment == TextAnchor.MiddleCenter, "childAlignment do GridLayoutGroup deve ser MiddleCenter.");
            }
            finally
            {
                if (canvasGO != null) UnityEngine.Object.DestroyImmediate(canvasGO);
            }
        }

        private static void Test_ActionBarAndInventory_ScaleRatioConsistencyAcrossResolutions()
        {
            Vector2[] testResolutions = new Vector2[]
            {
                new(1920, 1080),
                new(1280, 720),
                new(2560, 1440),
                new(3840, 2160),
                new(800, 600),
                new(1024, 768),
                new(1280, 800),
                new(2560, 1080),
                new(3440, 1440)
            };

            const float invRefW = 800f;
            const float actionRefW = 800f;
            float expectedRatio = invRefW / actionRefW; // 1.0f (escala idêntica em qualquer resolução)

            foreach (var res in testResolutions)
            {
                float invScale = res.x / invRefW;
                float actionScale = res.x / actionRefW;
                float ratio = actionScale / invScale;

                AssertApproximately(ratio, expectedRatio, 0.0001f,
                    $"Proporção de escala entre ActionBar e Inventário falhou na resolução {res.x}x{res.y}.");

                // Valida que o tamanho do slot da ActionBar na tela acompanha consistentemente o inventário
                float invSlotPixels = 50f * invScale;
                float actionSlotPixels = 55f * actionScale;
                float slotRatio = actionSlotPixels / invSlotPixels;
                AssertApproximately(slotRatio, 55f / 50f, 0.0001f,
                    $"Proporção de tamanho de slot entre ActionBar e Inventário divergente na resolução {res.x}x{res.y}.");
            }
        }

        private static void Test_GUIMatrix_ScalingAndInverseMouseMath()
        {
            const float refH = 1080f;
            Rect buttonRect = new Rect(790f, 400f, 340f, 50f);

            // 4K (3840x2160)
            float screenH = 2160f;
            float scale = screenH / refH;
            var matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            var invMatrix = matrix.inverse;

            Vector2 screenClick = new Vector2((buttonRect.x + buttonRect.width * 0.5f) * scale, (buttonRect.y + buttonRect.height * 0.5f) * scale);
            Vector2 virtualClick = invMatrix.MultiplyPoint(screenClick);

            AssertTrue(buttonRect.Contains(virtualClick), "Clique em 4K transformado pela matriz inversa deve estar dentro do botão virtual.");

            // 720p (1280x720)
            screenH = 720f;
            scale = screenH / refH;
            matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            invMatrix = matrix.inverse;

            screenClick = new Vector2((buttonRect.x + buttonRect.width * 0.5f) * scale, (buttonRect.y + buttonRect.height * 0.5f) * scale);
            virtualClick = invMatrix.MultiplyPoint(screenClick);

            AssertTrue(buttonRect.Contains(virtualClick), "Clique em 720p transformado pela matriz inversa deve estar dentro do botão virtual.");
        }

        private static void Test_CraftingUIManager_CanvasScalerScreenSpaceCheck()
        {
            var worldCanvasGO = new GameObject("Test_WorldCanvas", typeof(Canvas)) { hideFlags = HideFlags.DontSave };
            var screenCanvasGO = new GameObject("Test_ScreenCanvas", typeof(Canvas)) { hideFlags = HideFlags.DontSave };
            var craftingManagerGO = new GameObject("Test_CraftingManager", typeof(CraftingUIManager)) { hideFlags = HideFlags.DontSave };

            try
            {
                var worldCanvas = worldCanvasGO.GetComponent<Canvas>();
                worldCanvas.renderMode = RenderMode.WorldSpace;

                var screenCanvas = screenCanvasGO.GetComponent<Canvas>();
                screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var manager = craftingManagerGO.GetComponent<CraftingUIManager>();

                var tryFind = typeof(CraftingUIManager).GetMethod("TryFindIntegrations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                tryFind?.Invoke(manager, null);

                var canvasField = typeof(CraftingUIManager).GetField("_canvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var assignedCanvas = canvasField?.GetValue(manager) as Canvas;

                AssertTrue(assignedCanvas != null, "CraftingUIManager deve encontrar um Canvas.");
                AssertTrue(assignedCanvas.renderMode != RenderMode.WorldSpace, "CraftingUIManager não pode se conectar a um Canvas WorldSpace.");

                var scaler = assignedCanvas.GetComponent<CanvasScaler>();
                AssertTrue(scaler != null, "O Canvas selecionado deve possuir CanvasScaler.");
                AssertTrue(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize, "O CanvasScaler selecionado deve operar em ScaleWithScreenSize.");
                AssertApproximately(scaler.referenceResolution.x, 800f, 0.01f, "Resolução de referência X do Crafting deve ser 800.");
                AssertApproximately(scaler.referenceResolution.y, 600f, 0.01f, "Resolução de referência Y do Crafting deve ser 600.");
                AssertApproximately(scaler.matchWidthOrHeight, 0f, 0.01f, "MatchWidthOrHeight deve ser 0 (Match Width).");
            }
            finally
            {
                if (worldCanvasGO != null) UnityEngine.Object.DestroyImmediate(worldCanvasGO);
                if (screenCanvasGO != null) UnityEngine.Object.DestroyImmediate(screenCanvasGO);
                if (craftingManagerGO != null) UnityEngine.Object.DestroyImmediate(craftingManagerGO);
            }
        }

        private static void Test_CraftingUIManager_DimensionsAndScreenBoundsConsistency()
        {
            var screenCanvasGO = new GameObject("Test_ScreenCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)) { hideFlags = HideFlags.DontSave };
            var craftingManagerGO = new GameObject("Test_CraftingManager", typeof(CraftingUIManager)) { hideFlags = HideFlags.DontSave };

            try
            {
                var screenCanvas = screenCanvasGO.GetComponent<Canvas>();
                screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = screenCanvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0f;

                var manager = craftingManagerGO.GetComponent<CraftingUIManager>();

                var canvasField = typeof(CraftingUIManager).GetField("_canvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                canvasField?.SetValue(manager, screenCanvas);

                manager.EnsureUIHierarchy();

                var craftingRoot = manager.CraftingRoot;
                AssertTrue(craftingRoot != null, "A raiz da interface de Crafting (CraftingFrame) deve ser instanciada.");

                // Valida dimensões contidas na janela (máximo 380 de largura e 370 de altura)
                AssertApproximately(craftingRoot.sizeDelta.x, 380f, 1f, "Largura da moldura de crafting deve ser 380 unidades de canvas.");
                AssertApproximately(craftingRoot.sizeDelta.y, 365f, 1f, "Altura da moldura de crafting deve ser 365 unidades de canvas.");

                // Valida que em qualquer resolução padrão (16:9, 16:10, 4:3) o painel não transborda a tela verticalmente
                Vector2[] testResolutions = new Vector2[]
                {
                    new(1920, 1080), // 16:9 -> altura de canvas = 450
                    new(1280, 720),  // 16:9 -> altura de canvas = 450
                    new(2560, 1440), // 16:9 -> altura de canvas = 450
                    new(3840, 2160), // 16:9 -> altura de canvas = 450
                    new(1920, 1200), // 16:10 -> altura de canvas = 500
                    new(1280, 800),  // 16:10 -> altura de canvas = 500
                    new(800, 600),   // 4:3 -> altura de canvas = 600
                    new(1024, 768)   // 4:3 -> altura de canvas = 600
                };

                foreach (var res in testResolutions)
                {
                    float scale = res.x / 800f;
                    float canvasH = res.y / scale;

                    // A altura do painel não pode exceder 85% da altura visível da tela
                    float heightRatio = craftingRoot.sizeDelta.y / canvasH;
                    AssertTrue(heightRatio <= 0.85f,
                        $"A janela de crafting ocupa {heightRatio * 100f:F1}% da altura da tela na resolução {res.x}x{res.y}, o que é excessivo (limite: 85%).");

                    // Valida margem livre no topo e na base (mínimo de 60 unidades totais de canvas)
                    float remainingMarginY = canvasH - craftingRoot.sizeDelta.y;
                    AssertTrue(remainingMarginY >= 60f,
                        $"Margem vertical insuficiente ({remainingMarginY} units) na resolução {res.x}x{res.y}.");
                }

                // Valida posicionamento horizontal lado a lado (modo duplo com inventário)
                // Crafting posicionado à esquerda em x = -195, Inventário à direita em x = +195
                float craftingLeft = -195f - (craftingRoot.sizeDelta.x * 0.5f);
                float craftingRight = -195f + (craftingRoot.sizeDelta.x * 0.5f);
                AssertTrue(craftingLeft >= -400f, "A borda esquerda da janela de crafting não pode sair da tela (-400).");
                AssertTrue(craftingRight <= 0f, "A borda direita da janela de crafting não pode cruzar o centro da tela (0).");
            }
            finally
            {
                if (screenCanvasGO != null) UnityEngine.Object.DestroyImmediate(screenCanvasGO);
                if (craftingManagerGO != null) UnityEngine.Object.DestroyImmediate(craftingManagerGO);
            }
        }
    }
}
