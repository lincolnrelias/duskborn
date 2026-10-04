using System;
using System.Reflection;
using Duskborn.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Duskborn.Gameplay.Player;

namespace Duskborn.Editor
{
    public static class WorldMapTests
    {
        public static void RunAllTests()
        {
            var config = ScriptableObject.CreateInstance<LowPolyTerrainConfig>();
            var root = new GameObject("Map mesh test", typeof(RectTransform));
            try
            {
                config.chunkSize = 32; config.cellSize = 1;
                config.chunksX = 5; config.chunksZ = 4;
                Rect bounds = WorldMapProjection.TerrainBounds(config);
                Check(bounds == new Rect(-64, -64, 160, 128), "Odd/even chunk bounds must match terrain generation.");
                Check(WorldMapProjection.Project(new Vector3(10, 900, 30), new Vector2(10, 20), 20) ==
                    new Vector2(0, .5f), "North points up and height does not affect marker positions.");
                Check(WorldMapProjection.Project(new Vector3(20, 0, 20), new Vector2(10, 20), 20) ==
                    new Vector2(.5f, 0), "East points right.");
                Vector2 edge = WorldMapProjection.ClampMarker(new Vector2(30, 40));
                Check(Mathf.Abs(edge.magnitude - .92f) < .0001f && Vector2.Dot(edge.normalized, new Vector2(.6f, .8f)) > .999f,
                    "Distant players stay inside the rim in their actual direction.");
                Check(WorldMapProjection.ClampMarker(new Vector2(.2f, .3f)) == new Vector2(.2f, .3f),
                    "Nearby markers retain their exact positions.");

                var graphic = root.AddComponent<WorldMapGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(270, 270);
                using var vertices = new VertexHelper();
                typeof(WorldMapGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(graphic, new object[] { vertices });
                Check(vertices.currentVertCount == 98 && vertices.currentIndexCount == 288,
                    "Circular terrain has a complete fan with no rectangular background.");
                var vertex = new UIVertex();
                for (int i = 1; i < vertices.currentVertCount; i++)
                {
                    vertices.PopulateUIVertex(ref vertex, i);
                    Check(Mathf.Abs(((Vector2)vertex.position).magnitude - 135f) < .001f, "Terrain edge stays circular.");
                }
                Debug.Log("[WorldMapTests] Projection, terrain bounds, edge indicators and circular mesh passed.");
                graphic.Square = true;
                typeof(WorldMapGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { vertices });
                Check(vertices.currentVertCount == 4 && vertices.currentIndexCount == 6,
                    "World map must use a separate rectangular chart.");
                TestEscapePriority(config);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void TestEscapePriority(LowPolyTerrainConfig config)
        {
            var mapRoot = new GameObject("Map controls test");
            var pauseRoot = new GameObject("Map pause priority test");
            try
            {
                var map = EditModeTestSupport.AddInitialized<WorldMapUI>(mapRoot);
                typeof(WorldMapUI).GetMethod("SetSource", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(map, new object[] { config, 4242, 2f });
                Check(mapRoot.GetComponent<RectTransform>() != null && mapRoot.GetComponent<Canvas>() != null &&
                    mapRoot.GetComponent<GraphicRaycaster>() != null, "HUD must initialize a real canvas and pointer input.");
                Check(mapRoot.GetComponent<CanvasScaler>().matchWidthOrHeight == 1f,
                    "Map must match the pause menu and HUD's screen-height scaling.");
                TestCanvasIsolation(map);
                map.ZoomMinimap(true); Check(map.MinimapRadius < 55, "Minimap zoom-in button must reduce its range.");
                for (int i = 0; i < 30; i++) map.ZoomMinimap(true);
                Check(map.MinimapRadius == 20, "Minimap zoom must clamp to a useful minimum.");
                for (int i = 0; i < 30; i++) map.ZoomMinimap(false);
                Check(map.MinimapRadius == 160, "Minimap zoom must clamp to a useful maximum.");
                map.ZoomMap(true); Check(map.MapZoom > 1, "World chart zoom must change independently.");
                var centerField = typeof(WorldMapUI).GetField("mapCenter", BindingFlags.NonPublic | BindingFlags.Instance);
                Vector2 before = (Vector2)centerField.GetValue(map);
                map.Pan(new Vector2(40, -10));
                Check((Vector2)centerField.GetValue(map) != before, "Dragging must change the world chart center.");
                map.ResetMap(); Check(map.MapZoom == 1, "Reset must restore the complete world.");
                mapRoot.GetComponent<Canvas>().enabled = true;
                TestCameraPointerOwnership(map);
                foreach (var button in mapRoot.GetComponentsInChildren<Button>(true))
                    if (button.name == "M") button.onClick.Invoke();
                Check(map.IsExpanded && Duskborn.Gameplay.Player.PlayerCameraController.IsAnyMenuOpen(),
                    "The minimap map button must open the chart and reserve mouse input.");
                foreach (var button in mapRoot.GetComponentsInChildren<Button>(true))
                    if (button.name == "X") button.onClick.Invoke();
                Check(!map.IsExpanded, "The world chart close button must dismiss the window.");
                // Exercise Escape without the pause controller's play-mode persistence setup.
                var pause = pauseRoot.AddComponent<InGameMenuController>();
                pauseRoot.SetActive(false);
                pause.enabled = false;
                typeof(InGameMenuController).GetProperty(nameof(InGameMenuController.Instance)).SetValue(null, null);
                typeof(InGameMenuController).GetMethod("AutoInitialize", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, null);
                Check(InGameMenuController.Instance == pause && pause.isActiveAndEnabled,
                    "Startup must find and activate pause even when the existing controller is inactive.");
                var expanded = typeof(WorldMapUI).GetProperty(nameof(WorldMapUI.IsExpanded));
                var escape = typeof(InGameMenuController).GetMethod("HandleEscapeKey", BindingFlags.NonPublic | BindingFlags.Instance);
                expanded.SetValue(map, true);
                escape.Invoke(pause, null);
                Check(!map.IsExpanded && pause.IsOpen, "Escape must open pause and dismiss the world chart.");
                float beforeZoom = map.MinimapRadius;
                foreach (var button in mapRoot.GetComponentsInChildren<Button>(true))
                    if (button.name == "+" && button.transform.parent.name == "Minimap")
                    {
                        button.onClick.Invoke();
                        float after = map.MinimapRadius;
                        Check(after < beforeZoom && pause.IsOpen, "Minimap zoom must work while pause stays open.");
                        button.onClick.Invoke();
                        Check(map.MinimapRadius == after, "The UI and pause pointer paths must not double-fire a click.");
                    }
                map.Toggle();
                Check(map.IsExpanded && !pause.IsOpen, "Opening the map from pause must transition to the interactive chart.");
                escape.Invoke(pause, null);
                Check(pause.IsOpen && !map.IsExpanded, "Escape must continue to open pause after interacting with the chart.");
                pause.CloseMenu(false);
                Debug.Log("[WorldMapTests] Height scaling, paused zoom, click deduplication and pause/map transitions passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mapRoot);
                UnityEngine.Object.DestroyImmediate(pauseRoot);
            }
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new Exception(message);
        }

        private static void TestCameraPointerOwnership(WorldMapUI map)
        {
            var cameraRoot = new GameObject("Map pointer ownership camera");
            // Unity's input test support allows player input buffers in edit mode
            // without running the scene or entering Play Mode.
            var inputManager = typeof(InputSystem).GetField("s_Manager", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var editInput = inputManager.GetType().GetProperty("runPlayerUpdatesInEditMode");
            bool previousEditInput = (bool)editInput.GetValue(inputManager);
            editInput.SetValue(inputManager, true);
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            bool oldVisible = Cursor.visible;
            var oldLock = Cursor.lockState;
            try
            {
                var camera = EditModeTestSupport.AddInitialized<PlayerCameraController>(cameraRoot);
                var cursorUpdate = typeof(PlayerCameraController).GetMethod("UpdateCursorLock", BindingFlags.NonPublic | BindingFlags.Instance);
                var cursorLocked = typeof(PlayerCameraController).GetField("_isCursorLocked", BindingFlags.NonPublic | BindingFlags.Instance);
                var altUnlocked = typeof(PlayerCameraController).GetField("_isAltUnlocked", BindingFlags.NonPublic | BindingFlags.Instance);
                cursorLocked.SetValue(camera, true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftAlt));
                PumpPlayerInput();
                Check(keyboard.leftAltKey.wasPressedThisFrame, "Synthetic Alt must reach the player input buffer.");
                cursorUpdate.Invoke(camera, null);
                Check((bool)altUnlocked.GetValue(camera) && !(bool)cursorLocked.GetValue(camera),
                    "Alt must preserve the unlocked state on the following frame instead of immediately reclaiming the pointer.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                PumpPlayerInput();
                Canvas.ForceUpdateCanvases();
                foreach (var button in map.GetComponentsInChildren<Button>(true))
                {
                    if (button.transform.parent.name != "Minimap") continue;
                    var corners = new Vector3[4];
                    ((RectTransform)button.transform).GetWorldCorners(corners);
                    Vector2 point = (corners[0] + corners[2]) * .5f;
                    Check(map.ContainsScreenPoint(point), "Actual minimap button bounds must reserve pointer input.");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                    PumpPlayerInput();
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    typeof(PlayerCameraController).GetField("_isAltUnlocked", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(camera, false);
                    typeof(PlayerCameraController).GetMethod("UpdateCursorLock", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(camera, null);
                    Check(Cursor.visible && Cursor.lockState == CursorLockMode.None &&
                        !(bool)typeof(PlayerCameraController).GetField("_isCursorLocked", BindingFlags.NonPublic | BindingFlags.Instance)
                            .GetValue(camera),
                        "Camera must preserve the pointer over M/+/- before EventSystem has processed the frame.");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                    PumpPlayerInput();
                    cursorUpdate.Invoke(camera, null);
                    Check(Cursor.visible && !(bool)cursorLocked.GetValue(camera),
                        "Pointer must remain usable through mouse release, when UGUI dispatches its button click.");
                }
                var mini = map.transform.Find("Minimap") as RectTransform;
                var miniCorners = new Vector3[4]; mini.GetWorldCorners(miniCorners);
                Check(!map.ContainsScreenPoint((Vector2)miniCorners[0] - Vector2.one * 20),
                    "Minimap must not reserve clicks outside its screen bounds.");
                Debug.Log("[WorldMapTests] Real screen bounds preserve the cursor before EventSystem input processing.");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                editInput.SetValue(inputManager, previousEditInput);
                Cursor.lockState = oldLock; Cursor.visible = oldVisible;
                UnityEngine.Object.DestroyImmediate(cameraRoot);
            }
        }

        private static void PumpPlayerInput() => typeof(InputSystem).GetMethod("Update",
            BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(InputUpdateType) }, null)
            .Invoke(null, new object[] { InputUpdateType.Dynamic });

        private static void TestCanvasIsolation(WorldMapUI map)
        {
            var craftingRoot = new GameObject("Map canvas isolation crafting");
            var characterRoot = new GameObject("Map canvas isolation character");
            try
            {
                var canvas = map.GetComponent<Canvas>();
                var crafting = EditModeTestSupport.AddInitialized<CraftingUIManager>(craftingRoot);
                var character = EditModeTestSupport.AddInitialized<CharacterUIManager>(characterRoot);
                foreach (var manager in new MonoBehaviour[] { crafting, character })
                {
                    var type = manager.GetType();
                    var field = type.GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance);
                    field.SetValue(manager, canvas);
                    type.GetMethod("TryFindIntegrations", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
                    Check(field.GetValue(manager) != canvas, "Other UI must never borrow the map canvas.");
                }
                var scaler = map.GetComponent<CanvasScaler>();
                Check(scaler.referenceResolution == new Vector2(1920, 1080) && scaler.matchWidthOrHeight == 1,
                    "Crafting must not overwrite map sizing with its 800x600 inventory scale.");
                scaler.referenceResolution = new Vector2(800, 600); scaler.matchWidthOrHeight = 0;
                typeof(WorldMapUI).GetField("mapScaler", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(map, null);
                typeof(WorldMapUI).GetMethod("EnsureCanvasScale", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(map, null);
                Check(scaler.referenceResolution == new Vector2(1920, 1080) && scaler.matchWidthOrHeight == 1,
                    "Map must recover its height-based layout if existing runtime UI changed it.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(craftingRoot);
                UnityEngine.Object.DestroyImmediate(characterRoot);
            }
        }
    }
}
