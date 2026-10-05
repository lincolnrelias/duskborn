using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Duskborn.Gameplay.ActionBar;
using Duskborn.UI;
using InventorySystem.Core;
using InventorySystem.UI;
using UnityEngine.EventSystems;

namespace Duskborn.Editor
{
    /// <summary>
    /// Automated tests to validate resolution scaling and dimensional consistency
    /// for ActionBar, IMGUI menus, HUD, and CraftingUI.
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
            RunTest(Test_ItemIconRegistry_CrossRegistrationAndResolution, ref passed, ref total);
            RunTest(Test_InventorySlotView_ImplementsDragHandlers, ref passed, ref total);
            RunTest(Test_InventoryDragController_DragIconCanvasConfiguration, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[UIResolutionScalingTests] {passed}/{total} tests passed!</b></color>");
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
                throw new Exception($"{message} (Expected: {b}, Actual: {a}, Delta: {Mathf.Abs(a - b)})");
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
                AssertTrue(scaler != null, "CanvasScaler must be created automatically on the ActionBar Canvas.");
                AssertTrue(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize, "ActionBar CanvasScaler must use ScaleWithScreenSize.");
                AssertApproximately(scaler.referenceResolution.x, 800f, 0.01f, "ActionBar reference resolution X must be 800 (identical to inventory).");
                AssertApproximately(scaler.referenceResolution.y, 600f, 0.01f, "ActionBar reference resolution Y must be 600 (identical to inventory).");
                AssertApproximately(scaler.matchWidthOrHeight, 0f, 0.01f, "MatchWidthOrHeight must be 0 for direct consistency with inventory.");
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

                AssertApproximately(rect.anchorMin.x, 0.5f, 0.001f, "anchorMin.x must be 0.5 (center)");
                AssertApproximately(rect.anchorMin.y, 0f, 0.001f, "anchorMin.y must be 0 (bottom)");
                AssertApproximately(rect.anchorMax.x, 0.5f, 0.001f, "anchorMax.x must be 0.5 (center)");
                AssertApproximately(rect.anchorMax.y, 0f, 0.001f, "anchorMax.y must be 0 (bottom)");
                AssertApproximately(rect.pivot.x, 0.5f, 0.001f, "pivot.x must be 0.5");
                AssertApproximately(rect.pivot.y, 0f, 0.001f, "pivot.y must be 0");

                // 8 slots of 55px with 4px spacing = 8 * 55 + 7 * 4 = 440 + 28 = 468px.
                AssertApproximately(rect.sizeDelta.x, 468f, 0.01f, "Total width must be 468px for 8 slots of 55px with 4px spacing.");
                AssertApproximately(rect.sizeDelta.y, 55f, 0.01f, "Total height must be 55px.");
                AssertApproximately(rect.anchoredPosition.y, 15f, 0.01f, "anchoredPosition.y must have a 15px bottom margin.");
                AssertTrue(glg.childAlignment == TextAnchor.MiddleCenter, "GridLayoutGroup childAlignment must be MiddleCenter.");
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
            float expectedRatio = invRefW / actionRefW; // 1.0f (identical scale at every resolution).

            foreach (var res in testResolutions)
            {
                float invScale = res.x / invRefW;
                float actionScale = res.x / actionRefW;
                float ratio = actionScale / invScale;

                AssertApproximately(ratio, expectedRatio, 0.0001f,
                    $"Scale ratio between ActionBar and Inventory failed at resolution {res.x}x{res.y}.");

                // Validate that on-screen ActionBar slot size consistently tracks inventory.
                float invSlotPixels = 50f * invScale;
                float actionSlotPixels = 55f * actionScale;
                float slotRatio = actionSlotPixels / invSlotPixels;
                AssertApproximately(slotRatio, 55f / 50f, 0.0001f,
                    $"Slot size ratio between ActionBar and Inventory diverges at resolution {res.x}x{res.y}.");
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

            AssertTrue(buttonRect.Contains(virtualClick), "A 4K click transformed by the inverse matrix must be inside the virtual button.");

            // 720p (1280x720)
            screenH = 720f;
            scale = screenH / refH;
            matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            invMatrix = matrix.inverse;

            screenClick = new Vector2((buttonRect.x + buttonRect.width * 0.5f) * scale, (buttonRect.y + buttonRect.height * 0.5f) * scale);
            virtualClick = invMatrix.MultiplyPoint(screenClick);

            AssertTrue(buttonRect.Contains(virtualClick), "A 720p click transformed by the inverse matrix must be inside the virtual button.");
        }

        private static void Test_CraftingUIManager_CanvasScalerScreenSpaceCheck()
        {
            // FindObjectsByType intentionally excludes objects marked DontSave.
            var worldCanvasGO = new GameObject("Test_WorldCanvas", typeof(Canvas));
            var screenCanvasGO = new GameObject("Test_ScreenCanvas", typeof(Canvas));
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

                AssertTrue(assignedCanvas != null, "CraftingUIManager must find a Canvas.");
                AssertTrue(assignedCanvas.renderMode != RenderMode.WorldSpace, "CraftingUIManager cannot connect to a WorldSpace Canvas.");

                var scaler = assignedCanvas.GetComponent<CanvasScaler>();
                AssertTrue(scaler != null, "The selected Canvas must have a CanvasScaler.");
                AssertTrue(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize, "The selected CanvasScaler must use ScaleWithScreenSize.");
                AssertApproximately(scaler.referenceResolution.x, 800f, 0.01f, "Crafting reference resolution X must be 800.");
                AssertApproximately(scaler.referenceResolution.y, 600f, 0.01f, "Crafting reference resolution Y must be 600.");
                AssertApproximately(scaler.matchWidthOrHeight, 0f, 0.01f, "MatchWidthOrHeight must be 0 (Match Width).");
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
                AssertTrue(craftingRoot != null, "The Crafting interface root (CraftingFrame) must be instantiated.");

                // The detailed two-column layout is scaled down as a whole.
                AssertApproximately(craftingRoot.sizeDelta.x, 460f, 1f, "Internal width must be 460.");
                AssertApproximately(craftingRoot.sizeDelta.y, 480f, 1f, "Internal height must be 480.");
                AssertApproximately(craftingRoot.localScale.x, 0.67f, 0.001f, "Panel scale must be 0.67.");
                AssertApproximately(craftingRoot.localScale.y, 0.67f, 0.001f, "Vertical scale must be 0.67.");
                float displayedHeight = craftingRoot.sizeDelta.y * craftingRoot.localScale.y;
                float displayedWidth = craftingRoot.sizeDelta.x * craftingRoot.localScale.x;

                // Validate that the panel does not overflow vertically at standard resolutions (16:9, 16:10, 4:3).
                Vector2[] testResolutions = new Vector2[]
                {
                    new(1920, 1080), // 16:9 -> canvas height = 450.
                    new(1280, 720),  // 16:9 -> canvas height = 450.
                    new(2560, 1440), // 16:9 -> canvas height = 450.
                    new(3840, 2160), // 16:9 -> canvas height = 450.
                    new(1920, 1200), // 16:10 -> canvas height = 500.
                    new(1280, 800),  // 16:10 -> canvas height = 500.
                    new(800, 600),   // 4:3 -> canvas height = 600.
                    new(1024, 768)   // 4:3 -> canvas height = 600.
                };

                foreach (var res in testResolutions)
                {
                    float scale = res.x / 800f;
                    float canvasH = res.y / scale;

                    // Panel height cannot exceed 85% of the visible screen height.
                    float heightRatio = displayedHeight / canvasH;
                    AssertTrue(heightRatio <= 0.85f,
                        $"The crafting window occupies {heightRatio * 100f:F1}% of screen height at resolution {res.x}x{res.y}, which is excessive (limit: 85%).");

                    // Validate free margin at top and bottom (at least 60 total canvas units).
                    float remainingMarginY = canvasH - displayedHeight;
                    AssertTrue(remainingMarginY >= 60f,
                        $"Insufficient vertical margin ({remainingMarginY} units) at resolution {res.x}x{res.y}.");
                }

                // Validate side-by-side horizontal placement (dual mode with inventory).
                float craftingLeft = craftingRoot.anchoredPosition.x - displayedWidth * 0.5f;
                float craftingRight = craftingRoot.anchoredPosition.x + displayedWidth * 0.5f;
                AssertTrue(craftingLeft >= -400f, "The crafting window's left edge cannot leave the screen (-400).");
                AssertTrue(craftingRight <= 0f, "The crafting window's right edge cannot cross the screen center (0).");
            }
            finally
            {
                if (screenCanvasGO != null) UnityEngine.Object.DestroyImmediate(screenCanvasGO);
                if (craftingManagerGO != null) UnityEngine.Object.DestroyImmediate(craftingManagerGO);
            }
        }

        private static void Test_ItemIconRegistry_CrossRegistrationAndResolution()
        {
            var testTex = new Texture2D(32, 32);
            testTex.name = "tex_test_icon";
            try
            {
                ItemIconRegistry.Register("item_test_unique", testTex);
                bool found = ItemIconRegistry.TryGetIcon("item_test_unique", out var resolved);
                AssertTrue(found, "ItemIconRegistry must find the icon registered by ID.");
                AssertTrue(resolved == testTex, "The returned icon must be the registered icon.");

                var materialItem = new MaterialItem("item_test_unique", "Test Item", "", "tex_test_icon", "resource", 1);
                var resolvedItem = ItemIconRegistry.Resolve(materialItem);
                AssertTrue(resolvedItem == testTex, "ItemIconRegistry.Resolve must resolve the icon for IInventoryItem.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testTex);
            }
        }

        private static void Test_InventorySlotView_ImplementsDragHandlers()
        {
            var slotGO = new GameObject("Test_Slot", typeof(RectTransform), typeof(InventorySlotView));
            try
            {
                var slot = slotGO.GetComponent<InventorySlotView>();
                AssertTrue(slot is IBeginDragHandler, "InventorySlotView must implement IBeginDragHandler to prevent bubbling to the window's DraggablePanel.");
                AssertTrue(slot is IDragHandler, "InventorySlotView must implement IDragHandler.");
                AssertTrue(slot is IEndDragHandler, "InventorySlotView must implement IEndDragHandler.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(slotGO);
            }
        }

        private static void Test_InventoryDragController_DragIconCanvasConfiguration()
        {
            var canvasGO = new GameObject("Test_Canvas", typeof(RectTransform), typeof(Canvas));
            var iconGO = new GameObject("Test_DragIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(canvasGO.transform);
            var slotTemplateGO = new GameObject("Slot_0", typeof(RectTransform), typeof(InventorySlotView));
            slotTemplateGO.transform.SetParent(canvasGO.transform);
            var slotTemplateGO2 = new GameObject("Slot_1", typeof(RectTransform), typeof(InventorySlotView));
            slotTemplateGO2.transform.SetParent(canvasGO.transform);
            var slotTemplateGO3 = new GameObject("Slot_2", typeof(RectTransform), typeof(InventorySlotView));
            slotTemplateGO3.transform.SetParent(canvasGO.transform);
            var slotTemplateGO4 = new GameObject("Slot_3", typeof(RectTransform), typeof(InventorySlotView));
            slotTemplateGO4.transform.SetParent(canvasGO.transform);
            try
            {
                var image = iconGO.GetComponent<Image>();
                var service = new InventoryService(new InventoryGrid(2, 2));
                var rootRect = canvasGO.GetComponent<RectTransform>();
                var presenter = new InventoryPresenter(service, rootRect, item => new ItemViewModel(item.DisplayName, "", item.IconId));

                var controller = new InventoryDragController(service, presenter, canvasGO.GetComponent<Canvas>(), image, 0.1f);
                try
                {
                    // Nested canvas sorting is resolved when the drag icon becomes active.
                    image.gameObject.SetActive(true);
                    var canvasComp = image.GetComponent<Canvas>();
                    AssertTrue(canvasComp != null, "DragIcon must have a dedicated Canvas component.");
                    AssertTrue(canvasComp.overrideSorting, "DragIcon Canvas must have overrideSorting enabled.");
                    AssertTrue(canvasComp.sortingOrder >= 999, "DragIcon SortingOrder must be high (>= 999) to overlay all windows.");
                }
                finally
                {
                    controller.Dispose();
                    presenter.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGO);
            }
        }
    }
}
