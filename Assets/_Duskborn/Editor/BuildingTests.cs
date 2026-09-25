using System;
using System.IO;
using System.Linq;
using Duskborn.Gameplay.Building;
using Duskborn.UI.Building;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Duskborn.Editor
{
    public static class BuildingTests
    {
        [MenuItem("Duskborn/Tests/Capture Building UI Preview")]
        public static void CaptureUiPreview()
        {
            var owner = new GameObject("BuildingUIPreviewOwner");
            var inventory = owner.AddComponent<Duskborn.Gameplay.Loot.ResourceInventory>();
            var cameraObject = new GameObject("BuildingUIPreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                var definitions = Resources.LoadAll<BuildableDefinition>("Building");
                if (definitions.Length == 0) throw new InvalidOperationException("No building definitions were loaded.");
                foreach (var cost in definitions[0].costs)
                    if (cost.material != null) inventory.Add(cost.material.Id, cost.amount);

                var manager = BuildingUIManager.Create(owner.transform);
                manager.ShowCatalog(definitions, d => BuildingPresentation.Create(d, inventory, null, false),
                    _ => { }, () => { }, () => { }, () => { });

                var canvas = owner.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .035f, .05f, 1f);
                camera.targetTexture = target;
                WriteCapture(camera, target, texture, "building-ui-catalog.png");

                manager.BeginForgeStation("FORJA", () => { });
                manager.AddForgeProcessor(() => null, () => null, () => null,
                    () => "Iron  32/2", () => "Wood  15/1", () => "Barra de Ferro  ×18",
                    () => "Fundir Barra de Ferro  •  7.0s", () => .42f, () => .58f, () => false,
                    () => { }, () => { }, () => { });
                manager.Tick();
                WriteCapture(camera, target, texture, "building-ui-processing.png");

                manager.BeginStation("BAÚ DE MATERIAIS", () => { });
                manager.AddStationSection("Armazenamento");
                manager.AddStationProgress(() => "CAPACIDADE  73/200", () => .365f);
                manager.AddStationLabel(() => "JOGADOR                                      BAÚ\nUse + para guardar e − para retirar.", 54);
                manager.AddStorageRow("Wood", definitions[0].costs[0].material.Icon, () => 24, () => 51,
                    () => { }, () => { }, () => { }, () => { }, () => true, () => true);
                manager.AddStorageRow("Stone", null, () => 9, () => 22,
                    () => { }, () => { }, () => { }, () => { }, () => true, () => true);
                manager.Tick();
                WriteCapture(camera, target, texture, "building-ui-storage.png");
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void WriteCapture(Camera camera, RenderTexture target, Texture2D texture, string fileName)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            string path = Path.GetFullPath(Path.Combine("Artifacts/BuildingUI", fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log("[BuildingTests] UI preview captured: " + path);
        }

        [MenuItem("Duskborn/Tests/Run Building Physics and Asset Tests")]
        public static void Run()
        {
            int passed = 0;
            var origin = new Vector3(10000, 10000, 10000);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var inventoryObject = new GameObject("BuildingPresentationInventory");
            var inventory = inventoryObject.AddComponent<Duskborn.Gameplay.Loot.ResourceInventory>();
            var starterInventoryObject = new GameObject("StarterMaterialInventory");
            var starterInventory = starterInventoryObject.AddComponent<Duskborn.Gameplay.Loot.ResourceInventory>();
            var uiOwner = new GameObject("BuildingUITestOwner");
            var definition = ScriptableObject.CreateInstance<BuildableDefinition>();
            var testPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                starterInventory.EnsureStartingAmount("material_stone", 50);
                starterInventory.EnsureStartingAmount("material_wood", 50);
                starterInventory.EnsureStartingAmount("material_iron", 50);
                Check(starterInventory.GetCount("material_stone") == 50 &&
                      starterInventory.GetCount("material_wood") == 50 &&
                      starterInventory.GetCount("material_iron") == 50,
                    "starter building materials", ref passed);
                Check(starterInventory.Revision == 0, "starter materials preserve load gate", ref passed);
                starterInventory.EnsureStartingAmount("material_stone", 50);
                Check(starterInventory.GetCount("material_stone") == 50 && starterInventory.Revision == 0,
                    "starter materials are idempotent", ref passed);
                testPrefab.transform.localScale = new Vector3(2, 1, 2);
                definition.prefab = testPrefab;
                ground.transform.position = origin + Vector3.down * .5f; ground.transform.localScale = new Vector3(20, 1, 20);
                obstacle.SetActive(false); Physics.SyncTransforms();
                Check(PlacementValidator.Validate(definition, origin, Quaternion.identity, origin + Vector3.right * 3) == null, "flat supported footprint", ref passed);
                Check(PlacementValidator.Validate(definition, origin, Quaternion.identity, origin + Vector3.right * 30) != null, "maximum reach", ref passed);
                Check(PlacementValidator.Validate(definition, origin + Vector3.up * 2, Quaternion.identity, origin) != null, "floating placement", ref passed);
                Check(PlacementValidator.Validate(definition, new Vector3(float.NaN,0,0), Quaternion.identity, origin) != null, "nonfinite placement", ref passed);
                obstacle.SetActive(true); obstacle.transform.position = origin + Vector3.up * .5f; Physics.SyncTransforms();
                Check(PlacementValidator.Validate(definition, origin, Quaternion.identity, origin) != null, "solid obstacle", ref passed);
                obstacle.SetActive(false); ground.transform.localScale = new Vector3(.4f,1,.4f); Physics.SyncTransforms();
                Check(PlacementValidator.Validate(definition, origin, Quaternion.identity, origin) != null, "unsupported footprint corners", ref passed);
                foreach (var d in Resources.LoadAll<BuildableDefinition>("Building"))
                {
                    Check(d.prefab != null && MaterialCosts.Aggregate(d.costs, out _) && BuildableBounds.TryGet(d, out var bounds) && bounds.size.x > 0 && bounds.size.y > 0 && bounds.size.z > 0, d.id + " definition", ref passed);
                    var unavailable = BuildingPresentation.Create(d, inventory, null, false);
                    Check(unavailable.Costs.Count > 0 && unavailable.Costs.All(cost => cost.Missing >= 0), d.id + " presentation costs", ref passed);
                    foreach (var cost in unavailable.Costs) inventory.Add(cost.MaterialId, cost.Required);
                    var affordable = BuildingPresentation.Create(d, inventory, null, false);
                    if (d.unlockRecipe == null)
                        Check(affordable.IsAffordable && affordable.CanPlace, d.id + " affordable presentation", ref passed);
                    var visual = BuildingWorld.CreateVisual(d);
                    try
                    {
                        Check(visual.GetComponentsInChildren<MonoBehaviour>().Length == 0 && visual.GetComponentsInChildren<Collider>().All(c => !c.enabled), d.id + " inert preview", ref passed);
                        Check(visual.GetComponentsInChildren<Renderer>().Length > 0, d.id + " preview graphics", ref passed);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(visual); }
                }
                var state = new BuildingSnapshot();
                state.buildings.Add(new BuildingState { instanceId = "test", definitionId = "forge", position = origin, yaw = 45, upgradeLevel = 1 });
                state.buildings[0].jobs.Add(new ProcessingJob { recipe = "Recipe_SmeltIronBar", remaining = 3.25f });
                state.buildings[0].contents.Add(new MaterialStack { id = "material_iron_bar", amount = 2 });
                state.buildings[0].inputs.Add(new MaterialStack { id = "material_iron", amount = 8 });
                state.buildings[0].fuel.Add(new MaterialStack { id = "material_wood", amount = 4 });
                state.buildings[0].fuelCharges = 1;
                state.buildings[0].selectedRecipe = "Recipe_SmeltIronBar";
                var restored = JsonUtility.FromJson<BuildingSnapshot>(JsonUtility.ToJson(state));
                Check(restored.buildings[0].jobs[0].remaining == 3.25f && restored.buildings[0].contents[0].amount == 2 &&
                      restored.buildings[0].inputs[0].amount == 8 && restored.buildings[0].fuel[0].amount == 4 &&
                    restored.buildings[0].fuelCharges == 1 && restored.buildings[0].selectedRecipe == "Recipe_SmeltIronBar" &&
                    restored.buildings[0].yaw == 45 && restored.buildings[0].upgradeLevel == 1,
                    "save roundtrip", ref passed);
                var storageObject = new GameObject("StorageWithdrawalTest");
                var placedStorage = storageObject.AddComponent<PlacedBuilding>();
                placedStorage.Initialize(definition, new BuildingState { contents = { new MaterialStack { id = "wood", amount = 5 } } });
                Check(placedStorage.TryRemove("wood", 2) && placedStorage.State.contents[0].amount == 3, "precise storage withdrawal", ref passed);
                Check(!placedStorage.TryRemove("wood", 4) && placedStorage.State.contents[0].amount == 3, "failed storage withdrawal is atomic", ref passed);
                Check(placedStorage.TryRemove("wood", 3) && placedStorage.State.contents.Count == 0, "empty storage stack removed", ref passed);
                UnityEngine.Object.DestroyImmediate(storageObject);
                var definitions = Resources.LoadAll<BuildableDefinition>("Building");
                var ui = BuildingUIManager.Create(uiOwner.transform);
                ui.ShowCatalog(definitions, d => BuildingPresentation.Create(d, inventory, null, false), _ => { }, () => { }, () => { }, () => { });
                Check(uiOwner.transform.Find("BuildingUI/BuildingCatalog") != null, "catalog view hierarchy", ref passed);
                ui.ShowPlacement(BuildingPresentation.Create(definitions[0], inventory, null, false), null, 45, false);
                var placement = uiOwner.transform.Find("BuildingUI/PlacementHUD");
                Check(placement != null && !placement.GetComponent<UnityEngine.UI.Image>().raycastTarget, "placement HUD ignores clicks", ref passed);
                ui.ShowConfirmation("TESTE", "Confirmação", "OK", () => { });
                Check(uiOwner.transform.Find("BuildingUI/Confirmation").gameObject.activeSelf, "confirmation dialog", ref passed);
                ui.BeginForgeStation("FORJA", () => { });
                bool outputCollected = false;
                ui.AddForgeProcessor(() => null, () => null, () => null, () => "input", () => "fuel", () => "output", () => "status",
                    () => .5f, () => .5f, () => false, () => { }, () => { }, () => outputCollected = true);
                var forgeProcessor = uiOwner.transform.Find("BuildingUI/StationPanel/ForgeContent/ForgeProcessor");
                Check(forgeProcessor != null &&
                      !uiOwner.transform.Find("BuildingUI/StationPanel/StationScroll").gameObject.activeSelf,
                    "forge uses fixed non-scrollable content", ref passed);
                Check(forgeProcessor.GetComponentsInChildren<UnityEngine.UI.Image>(true).All(image => image.sprite == null),
                    "forge panels do not depend on UI sprites", ref passed);
                Check(uiOwner.transform.Find("BuildingUI/StationPanel/Header/CloseButton") != null,
                    "station header close button", ref passed);
                var outputHandler = forgeProcessor.Find("OutputSlot").GetComponent<ForgeSlotClickHandler>();
                outputHandler.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                Check(outputCollected, "forge output supports right-click collection", ref passed);
                Debug.Log($"[BuildingTests] {passed} checks passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ground); UnityEngine.Object.DestroyImmediate(obstacle);
                UnityEngine.Object.DestroyImmediate(inventoryObject); UnityEngine.Object.DestroyImmediate(starterInventoryObject);
                UnityEngine.Object.DestroyImmediate(uiOwner);
                UnityEngine.Object.DestroyImmediate(testPrefab);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }
        private static void Check(bool value, string name, ref int passed)
        {
            if (!value) throw new Exception("[BuildingTests] FAIL " + name);
            passed++; Debug.Log("[BuildingTests] PASS " + name);
        }
    }
}
