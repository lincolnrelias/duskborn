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
                    _ => { }, () => { });

                var canvas = owner.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .035f, .05f, 1f);
                camera.targetTexture = target;
                WriteCapture(camera, target, texture, "building-ui-catalog.png");

                manager.BeginForgeStation("FORGE", () => { });
                manager.AddForgeProcessor(() => null, () => null, () => null,
                    () => "Iron  32/2", () => "Wood  15/1", () => "Iron Bar  ×18",
                    () => "Smelt Iron Bar  •  7.0s", () => .42f, () => .58f, () => false,
                    () => { }, () => { }, () => { });
                manager.Tick();
                WriteCapture(camera, target, texture, "building-ui-processing.png");

                manager.BeginStation("MATERIAL CHEST", () => { });
                manager.AddStationSection("Storage");
                manager.AddStationProgress(() => "CAPACITY  73/200", () => .365f);
                manager.AddStationLabel(() => "PLAYER                                      CHEST\nUse + to store and − to withdraw.", 54);
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
                        // The station's visual animator is allowed; gameplay and network scripts are not.
                        Check(visual.GetComponentsInChildren<MonoBehaviour>(true).All(b => b is Duskborn.Gameplay.Crafting.MoonwellStationAnimator) && visual.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), d.id + " inert preview", ref passed);
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
                ui.ShowCatalog(definitions, d => BuildingPresentation.Create(d, inventory, null, false), _ => { }, () => { });
                Check(uiOwner.transform.Find("BuildingUI/BuildingCatalog") != null, "catalog view hierarchy", ref passed);
                var catalog = uiOwner.transform.Find("BuildingUI/BuildingCatalog");
                Check(catalog.Find("Save") == null && catalog.Find("Load") == null,
                    "checkpoint actions removed from catalog", ref passed);
                Canvas.ForceUpdateCanvases();
                var catalogScroll = catalog.Find("CatalogList/Cards").GetComponent<UnityEngine.UI.ScrollRect>();
                var wheel = new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, 10000) };
                catalogScroll.OnScroll(wheel);
                var topPosition = catalogScroll.content.anchoredPosition;
                catalogScroll.OnScroll(wheel);
                Check(Vector2.Distance(topPosition, catalogScroll.content.anchoredPosition) < .01f,
                    "fast scrolling stops at catalog top", ref passed);
                wheel.scrollDelta = new Vector2(0, -10000);
                catalogScroll.OnScroll(wheel);
                var bottomPosition = catalogScroll.content.anchoredPosition;
                catalogScroll.OnScroll(wheel);
                Check(Vector2.Distance(bottomPosition, catalogScroll.content.anchoredPosition) < .01f && !catalogScroll.inertia,
                    "fast scrolling stops at catalog bottom without momentum", ref passed);
                ui.ShowPlacement(BuildingPresentation.Create(definitions[0], inventory, null, false), null, 45, false);
                var placement = uiOwner.transform.Find("BuildingUI/PlacementHUD");
                Check(placement != null && !placement.GetComponent<UnityEngine.UI.Image>().raycastTarget, "placement HUD ignores clicks", ref passed);
                ui.ShowConfirmation("TEST", "Confirmation", "OK", () => { });
                Check(uiOwner.transform.Find("BuildingUI/Confirmation").gameObject.activeSelf, "confirmation dialog", ref passed);
                ui.BeginForgeStation("FORGE", () => { });
                bool outputCollected = false;
                ui.AddForgeProcessor(() => null, () => null, () => null, () => "input", () => "fuel", () => "output", () => "status",
                    () => .5f, () => .5f, () => false, () => { }, () => { }, () => outputCollected = true);
                var forgeProcessor = uiOwner.transform.Find("BuildingUI/StationPanel/ForgeContent/ForgeProcessor");
                Check(forgeProcessor != null &&
                      !uiOwner.transform.Find("BuildingUI/StationPanel/StationScroll").gameObject.activeSelf,
                    "forge uses fixed non-scrollable content", ref passed);
                ui.Tick();
                Check(forgeProcessor.Find("InputSlot/Value").GetComponent<UnityEngine.UI.Text>().text == "input" &&
                      forgeProcessor.Find("FuelSlot/Value").GetComponent<UnityEngine.UI.Text>().text == "fuel" &&
                      forgeProcessor.Find("OutputSlot/Value").GetComponent<UnityEngine.UI.Text>().text == "output" &&
                      forgeProcessor.Find("StatusPanel/Status").GetComponent<UnityEngine.UI.Text>().text == "status",
                    "forge skin refreshes loaded materials, fuel, output and status", ref passed);
                Check(uiOwner.transform.Find("BuildingUI/StationPanel/Header/CloseButton") != null,
                    "station header close button", ref passed);
                var outputHandler = forgeProcessor.Find("OutputSlot").GetComponent<ForgeSlotClickHandler>();
                outputHandler.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                Check(outputCollected, "forge output supports right-click collection", ref passed);
                ui.BeginForgeStation("FORGE", () => { });
                int loadCount = 0;
                string loadedId = null;
                bool loadedFuel = false;
                bool pendingLoad = false;
                int owned = 3;
                var inputOptions = Enumerable.Range(0, 7).Select(i => new ForgeInventoryOption
                    { Id = i == 0 ? "iron" : "iron_" + i, Name = "Iron", Owned = owned }).ToArray();
                var fuelOptions = new[] { new ForgeInventoryOption { Id = "wood", Name = "Wood", Owned = 2 } };
                ui.AddForgeProcessor(() => null, () => null, () => null, () => "", () => "", () => "Empty", () => "Waiting",
                    () => 0, () => 0, () => false, () => { }, () => { }, () => { },
                    fuel => { inputOptions[0].Owned = owned; inputOptions[0].Blocker = pendingLoad ? "Loading" : null; return fuel ? fuelOptions : inputOptions; },
                    (id, fuel) => { loadCount++; loadedId = id; loadedFuel = fuel; pendingLoad = true; }, () => pendingLoad);
                forgeProcessor = uiOwner.transform.Find("BuildingUI/StationPanel/ForgeContent/ForgeProcessor");
                var inputButton = forgeProcessor.Find("InputSlot").GetComponent<UnityEngine.UI.Button>();
                inputButton.onClick.Invoke();
                var picker = uiOwner.transform.Find("BuildingUI/StationPanel/ForgePicker");
                Check(picker.Find("Dropdown/Items/Content/Item_iron") != null && picker.Find("Dropdown/Items/Content/Item_wood") == null,
                    "material dropdown contains only supplied material options", ref passed);
                var itemButton = picker.Find("Dropdown/Items/Content/Item_iron").GetComponent<UnityEngine.UI.Button>();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                    picker.Find("Dropdown/Items/Content").GetComponent<RectTransform>());
                var firstTile = itemButton.GetComponent<RectTransform>();
                var fourthTile = picker.Find("Dropdown/Items/Content/Item_iron_3").GetComponent<RectTransform>();
                var fifthTile = picker.Find("Dropdown/Items/Content/Item_iron_4").GetComponent<RectTransform>();
                Check(Mathf.Abs(firstTile.rect.width - firstTile.rect.height) < .01f &&
                      Mathf.Abs(firstTile.anchoredPosition.y - fourthTile.anchoredPosition.y) < .01f &&
                      fourthTile.anchoredPosition.x > firstTile.anchoredPosition.x &&
                      fifthTile.anchoredPosition.y < firstTile.anchoredPosition.y,
                    "inventory squares wrap after four items", ref passed);
                var repeatedSelection = itemButton.onClick;
                owned = 0;
                itemButton.onClick.Invoke();
                Check(loadCount == 0, "dropdown rechecks owned quantity before loading", ref passed);
                owned = 3;
                itemButton.onClick.Invoke();
                repeatedSelection.Invoke();
                ui.Tick();
                Check(loadCount == 1 && loadedId == "iron" && !loadedFuel && !inputButton.interactable,
                    "material selection submits once and pending disables slots", ref passed);
                pendingLoad = false;
                forgeProcessor.Find("FuelSlot").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                picker = uiOwner.transform.Find("BuildingUI/StationPanel/ForgePicker");
                Check(picker.Find("Dropdown/Items/Content/Item_wood") != null && picker.Find("Dropdown/Items/Content/Item_iron") == null,
                    "fuel dropdown contains only supplied fuel options", ref passed);
                var pickerScroll = picker.Find("Dropdown/Items").GetComponent<UnityEngine.UI.ScrollRect>();
                Check(pickerScroll.movementType == UnityEngine.UI.ScrollRect.MovementType.Clamped && !pickerScroll.inertia,
                    "inventory dropdown scroll is clamped without inertia", ref passed);
                picker.Find("Dropdown/Items/Content/Item_wood").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Check(loadCount == 2 && loadedFuel && loadedId == "wood", "fuel selection preserves slot role", ref passed);
                var controller = inventoryObject.AddComponent<BuildingController>();
                var recipeFor = typeof(BuildingController).GetMethod("ForgeRecipeFor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var forgeRecipe = Resources.Load<Duskborn.Gameplay.Crafting.CraftingRecipe>("Crafting/Recipe_SmeltIronBar");
                var forgeRecipes = new[] { forgeRecipe };
                Check(recipeFor.Invoke(controller, new object[] { forgeRecipe.Ingredients[0].material.Id, false, forgeRecipes }) == forgeRecipe &&
                      recipeFor.Invoke(controller, new object[] { forgeRecipe.Ingredients[0].material.Id, true, forgeRecipes }) == null &&
                      recipeFor.Invoke(controller, new object[] { forgeRecipe.FuelIngredients[0].material.Id, true, forgeRecipes }) == forgeRecipe &&
                      recipeFor.Invoke(controller, new object[] { "unrelated_item", false, forgeRecipes }) == null,
                    "forge recipe filter separates actual fuel and material inventory items", ref passed);
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
