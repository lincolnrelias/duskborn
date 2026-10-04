using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Duskborn.Gameplay.Building;
using Duskborn.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Editor
{
    // Renders the actual production canvases in edit mode, without entering Play Mode.
    public static class WorldMapCapture
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        public static void Run()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Map capture requires graphics.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var local = new GameObject("Local player fixture"); local.AddComponent<PlayerStats>();
            var controller = local.AddComponent<PlayerController>(); local.transform.position = new Vector3(10, 0, 10);
            LocalPlayerContext.Register(controller); PlayerRegistry.Register(local.GetComponent<PlayerStats>());
            var friend = new GameObject("Teammate fixture"); friend.transform.position = new Vector3(32, 0, 25);
            var friendStats = EditModeTestSupport.AddInitialized<PlayerStats>(friend); PlayerRegistry.Register(friendStats);
            // Edit mode has no active FishNet server to initialize the fixture's SyncVar.
            var hp = typeof(PlayerStats).GetField("_currentHP", Private | BindingFlags.Public).GetValue(friendStats);
            hp.GetType().GetField("_value", Private).SetValue(hp, 100f);
            var world = EditModeTestSupport.AddInitialized<BuildingWorld>(new GameObject("Buildings fixture"));
            var definition = ScriptableObject.CreateInstance<BuildableDefinition>(); definition.displayName = "Workshop";
            for (int i = 0; i < 4; i++)
            {
                var building = new GameObject("Workshop fixture").AddComponent<PlacedBuilding>();
                building.Initialize(definition, new BuildingState { instanceId = "capture" + i,
                    position = new Vector3(-15 + i * 16, 0, 8 - i * 12) });
                world.Buildings.Add(building.State.instanceId, building);
            }
            var root = new GameObject("Map capture HUD", typeof(RectTransform));
            var ui = EditModeTestSupport.AddInitialized<Duskborn.UI.WorldMapUI>(root);
            var config = AssetDatabase.LoadAssetAtPath<LowPolyTerrainConfig>("Assets/_Duskborn/ScriptableObjects/World/TerrainConfig_CoopWorld_5x5.asset");
            typeof(Duskborn.UI.WorldMapUI).GetMethod("SetSource", Private).Invoke(ui, new object[] { config, config.seed, config.waterLevel });
            var bake = (IEnumerator)typeof(Duskborn.UI.WorldMapUI).GetMethod("BakeAtlas", Private).Invoke(ui, null);
            while (bake.MoveNext()) { }
            var camera = new GameObject("Map capture camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f, .14f, .12f);
            camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.enabled = true;
            var render = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); render.Create();
            camera.targetTexture = render;
            Directory.CreateDirectory("Artifacts/WorldMap");
            Capture(camera, render, "minimap.png");
            ui.Toggle();
            Capture(camera, render, "world-map.png");
            ui.CloseExpanded(); LocalPlayerContext.Unregister(controller); PlayerRegistry.Unregister(friendStats);
            RenderTexture.active = null; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(render);
            UnityEngine.Object.DestroyImmediate(definition);
            Debug.Log("[WorldMapCapture] Capture succeeded: Artifacts/WorldMap/world-map.png");
        }
        private static void Capture(Camera camera, RenderTexture render, string file)
        {
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = render;
            var texture = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine("Artifacts/WorldMap", file), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
