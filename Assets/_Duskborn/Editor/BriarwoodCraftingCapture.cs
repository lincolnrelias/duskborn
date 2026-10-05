using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Loot;
using Duskborn.UI;
using InventorySystem.Bootstrap;
using InventorySystem.Core;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Duskborn.Editor
{
    // Uses the production hierarchy, real recipes and real resource counts in edit mode.
    internal sealed class CraftingUIFixture : IDisposable
    {
        internal readonly GameObject Owner = new("Crafting UI fixture");
        internal readonly Canvas Canvas;
        internal readonly CraftingUIManager Manager;
        internal readonly ResourceInventory Resources;
        internal readonly CraftingRecipe Axe;
        internal CraftingUIFixture()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(Owner.transform, false);
            Canvas = canvasObject.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Resources = Owner.AddComponent<ResourceInventory>();
            var discovery = Owner.AddComponent<RecipeDiscoveryTracker>();
            var installer = Owner.AddComponent<InventoryInstaller>();
            typeof(InventoryInstaller).GetField("_service", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(installer, new InventoryService(new InventoryGrid(4, 4)));
            Manager = Owner.AddComponent<CraftingUIManager>();
            Set("_canvas", Canvas); Set("_playerResources", Resources);
            Set("_discoveryTracker", discovery); Set("_inventoryInstaller", installer);
            var station = Owner.AddComponent<Workbench>();
            station.Configure(CraftingStationType.Workbench, "Workbench");
            Manager.Open(station);
            Axe = UnityEngine.Resources.Load<CraftingRecipe>("Crafting/Recipe_StoneAxe");
            Call("SetActiveCategory", "Tools");
            Manager.SelectRecipe(Axe);
        }
        internal void Set(string field, object value) => typeof(CraftingUIManager)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Manager, value);
        internal object Call(string method, params object[] args) => typeof(CraftingUIManager)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Manager, args);
        internal void Stock()
        {
            foreach (var ingredient in UnityEngine.Resources.LoadAll<CraftingRecipe>("Crafting")
                .SelectMany(r => r.Ingredients).Where(i => i.material != null).GroupBy(i => i.material.Id).Select(g => g.First()))
                Resources.Add(ingredient.material.Id, 30);
            Call("RefreshRecipeListStates"); Call("RefreshDetailsView");
        }
        public void Dispose()
        {
            Manager.Close();
            UnityEngine.Object.DestroyImmediate(Owner);
        }
    }

    public static class BriarwoodCraftingCapture
    {
        public static void Run()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Crafting capture requires graphics.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            var quality = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            var cameraObject = new GameObject("Crafting capture camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .065f, .06f);
            try
            {
                using var fixture = new CraftingUIFixture();
                fixture.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                fixture.Canvas.worldCamera = camera; fixture.Canvas.planeDistance = 1;
                // Capture the station panel alone; live pairing remains a manual check.
                fixture.Manager.CraftingRoot.anchoredPosition = Vector2.zero;
                foreach (var state in new[] { "missing", "ready", "empty" })
                {
                    if (state == "ready") fixture.Stock();
                    if (state == "empty") fixture.Call("SetActiveCategory", "No recipes fixture");
                    foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                    {
                        var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                        target.Create(); camera.targetTexture = target;
                        try
                        {
                            Canvas.ForceUpdateCanvases();
                            foreach (var label in fixture.Canvas.GetComponentsInChildren<TextMeshProUGUI>()) label.ForceMeshUpdate();
                            // Edit mode has no player-loop LateUpdate to apply AutoHide.
                            foreach (var scroll in fixture.Canvas.GetComponentsInChildren<ScrollRect>())
                                typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scroll, null);
                            Canvas.ForceUpdateCanvases(); camera.Render();
                            RenderTexture.active = target;
                            var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                            texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                            Directory.CreateDirectory("Artifacts/BriarwoodUI");
                            File.WriteAllBytes($"Artifacts/BriarwoodUI/crafting-{state}-{size.x}.png", texture.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(texture);
                        }
                        finally
                        {
                            RenderTexture.active = null; camera.targetTexture = null;
                            UnityEngine.Object.DestroyImmediate(target);
                        }
                    }
                }
                Debug.Log("[BriarwoodCrafting] Capture succeeded.");
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = quality;
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
