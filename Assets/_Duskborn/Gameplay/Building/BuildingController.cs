using System;
using System.Collections.Generic;
using System.Linq;
using Duskborn.Effects;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Hotkeys;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using Duskborn.UI;
using Duskborn.UI.Building;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Duskborn.Gameplay.Building
{
    // Local input and presentation only. PlayerInteractor transports every world mutation.
    public sealed class BuildingController : MonoBehaviour
    {
        public static BuildingController Local { get; private set; }
        public static bool MenuOpen => Local != null && Local.ui != null && Local.ui.IsPanelOpen;
        public static bool IsPlacing => Local != null && Local.preview != null;
        public static bool BlocksGameplay => Local != null && (MenuOpen || Local.preview != null || Local.busy);

        private PlayerInteractor player;
        private ResourceInventory resources;
        private RecipeDiscoveryTracker discovery;
        private BuildableDefinition selected;
        private PlacedBuilding moving;
        private PlacedBuilding station;
        private CraftingRecipe selectedRecipe;
        private GameObject preview;
        private Material ghost;
        private BuildingUIManager ui;
        private HotkeyManager hotkeys;
        private float yaw;
        private Vector3 position;
        private Vector3 previewGroundOffset;
        private string invalid;
        private bool busy;
        private bool hasGround;
        private string lastAction;
        private string placementError;
        private float placementErrorUntil;
        private float refreshAt;

        private void Start()
        {
            player = GetComponent<PlayerInteractor>();
            resources = GetComponent<ResourceInventory>();
            discovery = GetComponent<RecipeDiscoveryTracker>();
            Local = this;
            ui = BuildingUIManager.Create(transform);
            if (resources != null) resources.ResourceChanged += OnResourceChanged;
            if (discovery != null) discovery.OnRecipeDiscovered += OnRecipeDiscovered;
        }

        private void Update()
        {
            ui?.Tick();
            if (hotkeys == null && HotkeyManager.Instance != null)
            {
                hotkeys = HotkeyManager.Instance;
                hotkeys.Register(HotkeyManager.Build, Toggle);
                hotkeys.Register(HotkeyManager.BuildRotate, Rotate);
            }
            if (player == null || !player.IsOwner) return;
            if (GetComponent<PlayerStats>()?.IsAlive != true) { Cancel(); return; }
            if (station != null && Vector3.Distance(transform.position, station.transform.position) > 4f)
            {
                ui.HidePanels();
                InventoryUIManager.Instance?.SetStationContext(false);
                station = null;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) Cancel();

            if (preview != null) UpdatePlacement();
            else ui.HidePlacement();

            if (Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + .2f;
                ui.RefreshCatalog();
            }
        }

        private void UpdatePlacement()
        {
            if (PlayerCameraController.IsAnyMenuOpen()) { Cancel(); return; }
            float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : Input.mouseScrollDelta.y;
            if (!busy && Mathf.Abs(wheel) > .01f)
                yaw = Mathf.Repeat(yaw + Mathf.Sign(wheel) * selected.rotationStep, 360f);

            var camera = Camera.main;
            RaycastHit ground = default;
            hasGround = camera != null && Physics.Raycast(
                camera.ViewportPointToRay(new Vector3(.5f, .5f)), out ground, 30f,
                selected.groundLayers, QueryTriggerInteraction.Ignore);
            if (hasGround) position = ground.point;
            invalid = hasGround
                ? PlacementValidator.Validate(selected, position, Quaternion.Euler(0, yaw, 0), transform.position,
                    moving != null ? moving.transform : null)
                : "Aponte para o chão.";
            if (moving == null && !MaterialCosts.CanPay(resources, selected.costs))
                invalid = "Materiais insuficientes.";

            preview.SetActive(hasGround);
            preview.transform.SetPositionAndRotation(position + previewGroundOffset, Quaternion.Euler(0, yaw, 0));
            if (ghost != null)
                ghost.SetColor("_BaseColor", invalid == null
                    ? new Color(.2f, 1f, .5f, .4f)
                    : new Color(1f, .18f, .15f, .45f));

            string visibleReason = Time.unscaledTime < placementErrorUntil ? placementError : invalid;
            ui.ShowPlacement(Presentation(selected), visibleReason, yaw, busy);

            // The HUD is non-raycastable. A locked cursor therefore cannot lose the world click to UI.
            bool confirm = Mouse.current != null
                ? Mouse.current.leftButton.wasPressedThisFrame
                : Input.GetMouseButtonDown(0);
            if (confirm && invalid == null && !busy &&
                !(PlayerCameraController.LocalInstance != null && PlayerCameraController.LocalInstance.JustLockedCursorThisFrame))
            {
                Send(new BuildingCommand
                {
                    action = moving == null ? "place" : "move",
                    definition = selected.id,
                    instance = moving != null ? moving.State.instanceId : null,
                    position = position,
                    yaw = yaw
                });
            }
        }

        private void OnDestroy()
        {
            if (hotkeys != null)
            {
                hotkeys.Unregister(HotkeyManager.Build, Toggle);
                hotkeys.Unregister(HotkeyManager.BuildRotate, Rotate);
            }
            if (resources != null) resources.ResourceChanged -= OnResourceChanged;
            if (discovery != null) discovery.OnRecipeDiscovered -= OnRecipeDiscovered;
            if (preview != null) Destroy(preview);
            if (ghost != null) Destroy(ghost);
            if (Local == this) Local = null;
        }

        private void Rotate()
        {
            if (preview != null && !busy) yaw = Mathf.Repeat(yaw + selected.rotationStep, 360f);
        }

        private void Toggle()
        {
            if (busy) return;
            if (preview != null || MenuOpen) { Cancel(); return; }
            if (PlayerCameraController.IsAnyMenuOpen()) return;
            OpenCatalog();
        }

        public void Cancel()
        {
            if (busy) return;
            if (preview != null) Destroy(preview);
            preview = null;
            moving = null;
            selected = null;
            station = null;
            selectedRecipe = null;
            InventoryUIManager.Instance?.SetStationContext(false);
            if (ghost != null) Destroy(ghost);
            ghost = null;
            ui?.HidePlacement();
            ui?.HidePanels();
        }

        public void Complete(string error)
        {
            busy = false;
            if (!string.IsNullOrEmpty(error))
            {
                placementError = error;
                placementErrorUntil = Time.unscaledTime + 4f;
                ui.ShowToast(error, true);
                return;
            }

            bool closesFlow = preview != null || lastAction == "dismantle";
            if (closesFlow) Cancel();
            else if (station != null) RenderStation(station);
            ui.ShowToast(lastAction == "place" ? "Construção concluída." : "Ação concluída.");
            ui.RefreshCatalog();
        }

        public void Queue(CraftingRecipe recipe, PlacedBuilding building)
        {
            Send(new BuildingCommand { action = "queue", instance = building.State.instanceId, recipe = recipe.RecipeId });
        }

        private void Send(BuildingCommand command)
        {
            if (busy) return;
            lastAction = command.action;
            busy = true;
            player.RequestBuilding(command);
        }

        private void Begin(BuildableDefinition definition, PlacedBuilding relocation = null)
        {
            if (busy) return;
            if (definition == null || definition.prefab == null)
            {
                ui.ShowToast("Modelo da construção ausente. Verifique o catálogo.", true);
                return;
            }
            if (relocation == null && !Presentation(definition).CanPlace)
            {
                ui.ShowToast(Presentation(definition).DisabledReason, true);
                return;
            }

            Cancel();
            selected = definition;
            moving = relocation;
            previewGroundOffset = BuildableBounds.TryGet(definition, out var bounds)
                ? BuildableBounds.GroundOffset(bounds)
                : Vector3.zero;
            yaw = relocation != null ? relocation.State.yaw : transform.eulerAngles.y;
            placementErrorUntil = 0f;
            preview = BuildingWorld.CreateVisual(definition);
            ghost = GhostMaterial.Create();
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>())
            {
                if (ghost != null)
                    renderer.sharedMaterials = Enumerable.Repeat(ghost, renderer.sharedMaterials.Length).ToArray();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var child in preview.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
        }

        public void OpenStation(PlacedBuilding building) => OpenStation(building, null);

        public void OpenStation(PlacedBuilding building, CraftingRecipe preferredRecipe)
        {
            if (station != building)
            {
                Cancel();
                station = building;
            }
            selectedRecipe = preferredRecipe;
            RenderStation(building);
            InventoryUIManager.Instance?.SetStationContext(building != null && building.Definition.station == CraftingStationType.Forja);
        }

        private void RenderStation(PlacedBuilding building)
        {
            if (busy || building == null) return;
            if (station != building)
            {
                Cancel();
                station = building;
            }
            station = building;
            bool isForge = !building.Definition.storage && building.Definition.station == CraftingStationType.Forja;
            if (isForge) ui.BeginForgeStation(building.Definition.displayName, Cancel);
            else ui.BeginStation(building.Definition.displayName, Cancel);
            UnlockCursor();

            if (!building.Definition.storage)
            {
                var recipes = Resources.LoadAll<CraftingRecipe>("Crafting")
                    .Where(recipe => recipe.RequiredStation == building.Definition.station && recipe.ProcessingSeconds > 0)
                    .ToArray();
                var stateRecipe = BuildingWorld.Recipe(building.State.selectedRecipe);
                if (stateRecipe == null && building.State.jobs.Count > 0)
                    stateRecipe = BuildingWorld.Recipe(building.State.jobs[0].recipe);
                if (stateRecipe != null && recipes.Contains(stateRecipe)) selectedRecipe = stateRecipe;
                if (selectedRecipe == null || !recipes.Contains(selectedRecipe))
                    selectedRecipe = recipes.FirstOrDefault(recipe => recipe.RecipeId == "Recipe_SmeltIronBar" &&
                                                   discovery != null && discovery.IsDiscovered(recipe)) ??
                                     recipes.FirstOrDefault(recipe => discovery != null && discovery.IsDiscovered(recipe)) ??
                                     recipes.FirstOrDefault(recipe => recipe.RecipeId == "Recipe_SmeltIronBar") ??
                                     recipes.FirstOrDefault();

                if (building.Definition.station == CraftingStationType.Forja)
                {
                    RenderForge(building);
                }
                else
                {
                    ui.AddStationSection("Processamento atual");
                    ui.AddStationProgress(
                        () => CurrentProcessLabel(building),
                        () => CurrentProgress(building),
                        () => OutputBlocked(building));
                    ui.AddStationLabel(() => QueueLabel(building), 62);
                    ui.AddStationSection("Saída");
                    ui.AddStationProgress(
                        () => OutputLabel(building),
                        () => building.Definition.capacity > 0 ? (float)building.StoredCount / building.Definition.capacity : 0f,
                        () => building.StoredCount >= building.Definition.capacity);
                    ui.AddStationButton("COLETAR SAÍDA",
                        () => Send(new BuildingCommand { action = "collect", instance = building.State.instanceId }),
                        () => !busy && building.State.contents.Count > 0);
                    ui.AddStationSection("Receitas");
                    if (selectedRecipe != null)
                        ui.AddStationLabel(() => RecipeDetails(selectedRecipe, building), 106);
                    ui.AddStationButton(
                        () => selectedRecipe == null ? "FILA INDISPONÍVEL" : "ADICIONAR À FILA  •  " + selectedRecipe.RecipeName,
                        () => Queue(selectedRecipe, building),
                        () => selectedRecipe != null && CanQueue(selectedRecipe, building));

                    foreach (var recipe in recipes)
                    {
                        var captured = recipe;
                        ui.AddStationButton(
                            () => (selectedRecipe == captured ? "▶  " : string.Empty) + captured.RecipeName +
                                (discovery != null && discovery.IsDiscovered(captured) ? string.Empty : "  •  receita não descoberta") +
                                $"  •  {captured.ProcessingSeconds / building.Definition.processingSpeed:0}s",
                            () => { selectedRecipe = captured; RenderStation(building); },
                            () => !busy && discovery != null && discovery.IsDiscovered(captured));
                    }
                }
                if (!isForge)
                {
                    ui.AddStationSection("Fabricação");
                    ui.AddStationButton("FABRICAR EQUIPAMENTO", () =>
                    {
                        ui.HidePanels();
                        CraftingUIManager.EnsureInstance().Open(building.GetComponent<Workbench>());
                    });
                }
            }
            else
            {
                ui.AddStationSection("Armazenamento");
                ui.AddStationProgress(
                    () => $"CAPACIDADE  {building.StoredCount}/{building.Definition.capacity}",
                    () => building.Definition.capacity > 0 ? (float)building.StoredCount / building.Definition.capacity : 0f,
                    () => building.StoredCount >= building.Definition.capacity);
                ui.AddStationLabel(() => "JOGADOR                                      BAÚ\nUse + para guardar e − para retirar.", 54);
                var materialIds = resources.Counts.Keys
                    .Concat(building.State.contents.Select(stack => stack.id))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .OrderBy(MaterialName)
                    .ToArray();
                foreach (var id in materialIds)
                {
                    var captured = id;
                    ui.AddStorageRow(
                        MaterialName(captured), MaterialIcon(captured),
                        () => resources.GetCount(captured),
                        () => StoredAmount(building, captured),
                        () => Transfer(building, captured, 1, false),
                        () => Transfer(building, captured, Mathf.Min(resources.GetCount(captured), building.Definition.capacity - building.StoredCount), false),
                        () => Transfer(building, captured, 1, true),
                        () => Transfer(building, captured, StoredAmount(building, captured), true),
                        () => !busy && resources.GetCount(captured) > 0 && building.StoredCount < building.Definition.capacity,
                        () => !busy && StoredAmount(building, captured) > 0);
                }
            }

            // The forge is intentionally a focused, fixed-size furnace view. Recipe
            // selection is inferred from the material loaded through the inventory,
            // so it does not need the generic station action rows below.
            if (isForge) return;

            ui.AddStationSection("Estação");
            ui.AddStationButton("MOVER  •  grátis, mantém conteúdos",
                () => Begin(building.Definition, building),
                () => !busy && building.Definition.movable);
            ui.AddStationButton(
                () => building.Empty
                    ? "DESMONTAR  •  devolve 100% dos materiais"
                    : "DESMONTAR  •  retire materiais e conclua a fila",
                () => ConfirmDismantle(building),
                () => !busy && building.Definition.dismantlable && building.Empty,
                true);
        }

        private void RenderForge(PlacedBuilding building)
        {
            var recipe = selectedRecipe;
            ui.AddForgeProcessor(
                () => building.State.inputs.Count > 0 ? MaterialIcon(building.State.inputs[0].id) : null,
                () => building.State.fuel.Count > 0 ? MaterialIcon(building.State.fuel[0].id) : null,
                () => building.State.contents.Count > 0 ? MaterialIcon(building.State.contents[0].id) : null,
                () => building.State.inputs.Count == 0
                    ? string.Empty
                    : ForgeSlotSummary(recipe?.Ingredients, building.State.inputs, string.Empty),
                () => building.State.fuel.Count == 0
                    ? string.Empty
                    : ForgeSlotSummary(recipe?.FuelIngredients, building.State.fuel,
                        recipe != null && recipe.FuelIngredients.Count == 0 ? "não necessário" : string.Empty),
                () => building.State.contents.Count == 0
                    ? "vazio"
                    : string.Join("\n", building.State.contents.Select(stack => MaterialName(stack.id) + "  ×" + stack.amount)),
                () => ForgeStatus(building, recipe),
                () => CurrentProgress(building),
                () => building.State.jobs.Count > 0
                    ? Mathf.Max(.12f, 1f - CurrentProgress(building))
                    : building.FuelCharges > 0 ? .5f : 0f,
                () => OutputBlocked(building),
                () => ReturnForgeSlot(building, false),
                () => ReturnForgeSlot(building, true),
                () =>
                {
                    if (building.State.contents.Count > 0)
                        Send(new BuildingCommand { action = "collect", instance = building.State.instanceId });
                });

        }

        public bool TryAssignInventoryItem(string materialId)
        {
            if (busy || station == null || !MenuOpen || station.Definition.station != CraftingStationType.Forja ||
                string.IsNullOrEmpty(materialId) || resources == null) return false;
            var recipes = Resources.LoadAll<CraftingRecipe>("Crafting")
                .Where(recipe => recipe.RequiredStation == CraftingStationType.Forja && recipe.ProcessingSeconds > 0 &&
                    discovery != null && discovery.IsDiscovered(recipe))
                .ToArray();
            var recipe = selectedRecipe;
            bool compatible = recipe != null &&
                (IngredientAmount(recipe.Ingredients, materialId) > 0 || IngredientAmount(recipe.FuelIngredients, materialId) > 0);
            if (!compatible)
                recipe = recipes.FirstOrDefault(candidate => IngredientAmount(candidate.Ingredients, materialId) > 0);
            if (recipe == null)
            {
                ui.ShowToast("Este item não pode ser usado na forja.", true);
                return true;
            }
            bool incompatibleLoadedFuel = station.State.fuel.Any(stack =>
                IngredientAmount(recipe.FuelIngredients, stack.id) <= 0);
            if (!string.IsNullOrEmpty(station.State.selectedRecipe) && station.State.selectedRecipe != recipe.RecipeId &&
                (station.State.inputs.Count > 0 || incompatibleLoadedFuel || station.State.jobs.Count > 0))
            {
                ui.ShowToast("Esvazie a forja antes de trocar a receita.", true);
                return true;
            }
            bool fuel = IngredientAmount(recipe.FuelIngredients, materialId) > 0;
            int loaded = fuel ? station.FuelAmount(materialId) : station.InputAmount(materialId);
            int amount = Mathf.Min(resources.GetCount(materialId), PlacedBuilding.SlotStackCapacity - loaded);
            if (amount <= 0)
            {
                ui.ShowToast(loaded >= PlacedBuilding.SlotStackCapacity ? "O slot já está cheio." : "Você não possui esse material.", true);
                return true;
            }
            selectedRecipe = recipe;
            Send(new BuildingCommand
            {
                action = "load",
                instance = station.State.instanceId,
                recipe = recipe.RecipeId,
                material = materialId,
                amount = amount
            });
            return true;
        }

        private void ReturnForgeSlot(PlacedBuilding building, bool fuel)
        {
            var stacks = fuel ? building.State.fuel : building.State.inputs;
            if (busy || stacks.Count == 0) return;
            var stack = stacks[0];
            Send(new BuildingCommand
            {
                action = "unload",
                instance = building.State.instanceId,
                slot = fuel ? "fuel" : "input",
                material = stack.id,
                amount = stack.amount
            });
        }

        private static string ForgeSlotSummary(
            IReadOnlyList<CraftingIngredient> requirements,
            IReadOnlyList<MaterialStack> loaded,
            string empty)
        {
            if (requirements == null || requirements.Count == 0) return empty;
            return string.Join("\n", requirements.Select(requirement =>
            {
                int amount = loaded.FirstOrDefault(stack => stack.id == requirement.material.Id)?.amount ?? 0;
                return requirement.material.DisplayName + "  " + amount + "/" + requirement.amount;
            }));
        }

        private static string ForgeStatus(PlacedBuilding building, CraftingRecipe recipe)
        {
            if (recipe == null) return "Selecione uma receita.";
            if (OutputBlocked(building)) return "SAÍDA CHEIA  •  fundição pausada";
            if (building.State.jobs.Count > 0)
                return recipe.RecipeName + $"  •  {building.State.jobs[0].remaining / building.Definition.processingSpeed:0.0}s";
            bool inputs = HasIngredients(building.State.inputs, recipe.Ingredients);
            bool fuel = building.FuelCharges > 0 || HasIngredients(building.State.fuel, recipe.FuelIngredients);
            if (!inputs && !fuel) return "Aguardando recursos.";
            if (!inputs) return "Aguardando material.";
            if (!fuel) return "Aguardando combustível.";
            return "Pronto para acender.";
        }

        private static bool HasIngredients(IReadOnlyList<MaterialStack> stacks, IReadOnlyList<CraftingIngredient> requirements)
        {
            if (requirements == null) return true;
            foreach (var requirement in requirements)
                if ((stacks.FirstOrDefault(stack => stack.id == requirement.material.Id)?.amount ?? 0) < requirement.amount) return false;
            return true;
        }

        private static int IngredientAmount(IReadOnlyList<CraftingIngredient> ingredients, string materialId)
        {
            if (ingredients == null) return 0;
            int amount = 0;
            foreach (var ingredient in ingredients)
                if (ingredient.material != null && ingredient.material.Id == materialId) amount += ingredient.amount;
            return amount;
        }

        private bool CanQueue(CraftingRecipe recipe, PlacedBuilding building) =>
            !busy && discovery != null && discovery.IsDiscovered(recipe) &&
            MaterialCosts.CanPay(resources, recipe.Ingredients, recipe.FuelIngredients) &&
            building.State.jobs.Count < building.Definition.queueCapacity;

        private string RecipeDetails(CraftingRecipe recipe, PlacedBuilding building)
        {
            if (recipe == null) return "Selecione uma receita.";
            string output = recipe.OutputItem != null ? recipe.OutputAmount + " × " + recipe.OutputItem.DisplayName : "Saída inválida";
            string missing = MaterialCosts.CanPay(resources, recipe.Ingredients, recipe.FuelIngredients) ? "Materiais disponíveis." : "Faltam materiais.";
            string fuel = recipe.FuelIngredients.Count > 0 ? $"\nCOMBUSTÍVEL  {Costs(recipe.FuelIngredients)}" : string.Empty;
            return $"{recipe.RecipeName}\nENTRADA  {Costs(recipe.Ingredients)}{fuel}\nSAÍDA  {output}  •  {recipe.ProcessingSeconds / building.Definition.processingSpeed:0}s\n{missing}";
        }

        private static float CurrentProgress(PlacedBuilding building)
        {
            if (building.State.jobs.Count == 0) return 0f;
            var job = building.State.jobs[0];
            var recipe = BuildingWorld.Recipe(job.recipe);
            return recipe != null && recipe.ProcessingSeconds > 0 ? 1f - job.remaining / recipe.ProcessingSeconds : 0f;
        }

        private static bool OutputBlocked(PlacedBuilding building)
        {
            var recipe = building.State.jobs.Count > 0
                ? BuildingWorld.Recipe(building.State.jobs[0].recipe)
                : BuildingWorld.Recipe(building.State.selectedRecipe);
            return recipe != null && building.StoredCount + recipe.OutputAmount > building.Definition.capacity;
        }

        private static string CurrentProcessLabel(PlacedBuilding building)
        {
            if (building.State.jobs.Count == 0) return "Nenhum processo ativo.";
            var job = building.State.jobs[0];
            var recipe = BuildingWorld.Recipe(job.recipe);
            if (recipe == null) return "Processo inválido.";
            if (OutputBlocked(building)) return "SAÍDA CHEIA  •  processamento pausado";
            return recipe.RecipeName + $"  •  {job.remaining / building.Definition.processingSpeed:0}s restantes";
        }

        private static string QueueLabel(PlacedBuilding building)
        {
            if (building.State.jobs.Count <= 1) return $"FILA  {building.State.jobs.Count}/{building.Definition.queueCapacity}  •  nenhuma receita aguardando";
            return $"FILA  {building.State.jobs.Count}/{building.Definition.queueCapacity}\n" + string.Join("  •  ", building.State.jobs.Skip(1)
                .Select((job, index) => (index + 1) + ". " + (BuildingWorld.Recipe(job.recipe)?.RecipeName ?? job.recipe)));
        }

        private static string OutputLabel(PlacedBuilding building)
        {
            string contents = building.State.contents.Count == 0
                ? "vazio"
                : string.Join("  •  ", building.State.contents.Select(stack => MaterialName(stack.id) + " " + stack.amount));
            return $"SAÍDA  {building.StoredCount}/{building.Definition.capacity}  •  {contents}";
        }

        private void Transfer(PlacedBuilding building, string material, int amount, bool withdraw)
        {
            if (amount <= 0) return;
            Send(new BuildingCommand
            {
                action = withdraw ? "withdraw" : "deposit",
                instance = building.State.instanceId,
                material = material,
                amount = amount
            });
        }

        private static int StoredAmount(PlacedBuilding building, string material) =>
            building.State.contents.FirstOrDefault(stack => stack.id == material)?.amount ?? 0;

        private void ConfirmDismantle(PlacedBuilding building)
        {
            ui.ShowConfirmation(
                "DESMONTAR " + building.Definition.displayName.ToUpperInvariant() + "?",
                "A estação será removida e devolverá 100% do custo:\n" + Costs(building.Definition.costs),
                "DESMONTAR",
                () => Send(new BuildingCommand { action = "dismantle", instance = building.State.instanceId }));
        }

        private string Status(PlacedBuilding building)
        {
            string value = $"ARMAZENAMENTO  {building.StoredCount}/{building.Definition.capacity}    •    FILA  {building.State.jobs.Count}/{building.Definition.queueCapacity}\n";
            foreach (var stack in building.State.contents)
                value += MaterialName(stack.id) + ": " + stack.amount + "   ";
            if (building.State.jobs.Count > 0)
            {
                var job = building.State.jobs[0];
                var recipe = BuildingWorld.Recipe(job.recipe);
                if (recipe != null)
                {
                    value += "\n" + recipe.RecipeName + $"  •  {Mathf.Clamp01(1f - job.remaining / recipe.ProcessingSeconds):P0}";
                    if (building.StoredCount + recipe.OutputAmount > building.Definition.capacity)
                        value += "  •  SAÍDA CHEIA, PROCESSO PAUSADO";
                }
            }
            else if (building.State.contents.Count == 0) value += "\nNenhum processo ou material armazenado.";
            return value;
        }

        private string Costs(IReadOnlyList<CraftingIngredient> costs) => string.Join("  •  ", costs.Select(cost =>
            cost.material == null
                ? "Material inválido"
                : $"{cost.material.DisplayName}: {resources.GetCount(cost.material.Id)}/{cost.amount}" +
                  (resources.GetCount(cost.material.Id) < cost.amount
                      ? $" (faltam {cost.amount - resources.GetCount(cost.material.Id)})"
                      : string.Empty)));

        private static string MaterialName(string id)
        {
            if (BuildingWorld.Instance != null)
                foreach (var definition in BuildingWorld.Instance.Definitions)
                    foreach (var cost in definition.costs)
                        if (cost.material != null && cost.material.Id == id) return cost.material.DisplayName;
            foreach (var recipe in Resources.LoadAll<CraftingRecipe>("Crafting"))
            {
                if (recipe.OutputItem != null && recipe.OutputItem.Id == id) return recipe.OutputItem.DisplayName;
                foreach (var cost in recipe.Ingredients)
                    if (cost.material != null && cost.material.Id == id) return cost.material.DisplayName;
                foreach (var cost in recipe.FuelIngredients)
                    if (cost.material != null && cost.material.Id == id) return cost.material.DisplayName;
            }
            return id;
        }

        private static Texture2D MaterialIcon(string id)
        {
            if (BuildingWorld.Instance != null)
                foreach (var definition in BuildingWorld.Instance.Definitions)
                    foreach (var cost in definition.costs)
                        if (cost.material != null && cost.material.Id == id) return cost.material.Icon;
            foreach (var recipe in Resources.LoadAll<CraftingRecipe>("Crafting"))
            {
                if (recipe.OutputItem != null && recipe.OutputItem.Id == id) return recipe.OutputItem.Icon;
                foreach (var cost in recipe.Ingredients)
                    if (cost.material != null && cost.material.Id == id) return cost.material.Icon;
                foreach (var cost in recipe.FuelIngredients)
                    if (cost.material != null && cost.material.Id == id) return cost.material.Icon;
            }
            return null;
        }

        private void OpenCatalog()
        {
            station = null;
            var world = BuildingWorld.Instance;
            if (world == null)
            {
                ui.ShowToast("O catálogo de construção ainda não está disponível.", true);
                return;
            }
            ui.ShowCatalog(world.Definitions, Presentation, definition => Begin(definition), Cancel,
                () => Checkpoint(false), () => Checkpoint(true));
            UnlockCursor();
        }

        private static void UnlockCursor()
        {
            if (PlayerCameraController.LocalInstance != null)
                PlayerCameraController.LocalInstance.SetCursorLocked(false);
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private BuildablePresentation Presentation(BuildableDefinition definition)
        {
            bool alreadyPlaced = BuildingWorld.Instance != null && BuildingWorld.Instance.Buildings.Values
                .Any(building => building != null && building.Definition == definition);
            return BuildingPresentation.Create(definition, resources, discovery, alreadyPlaced);
        }

        private void Checkpoint(bool load)
        {
            if (!player.IsServerStarted || PlayerInteractor.BuildingPeers.Count != 1 || busy ||
                PlayerInteractor.BuildingPeers.Any(peer => peer.BuildingTransactionPending))
            {
                ui.ShowToast("Salvar/carregar requer host solo e nenhuma transação pendente.", true);
                return;
            }
            try
            {
                if (load) BuildingWorld.Instance.Load(resources); else BuildingWorld.Instance.Save(resources);
                ui.ShowToast(load ? "Infraestrutura carregada." : "Infraestrutura salva.");
            }
            catch (Exception exception)
            {
                ui.ShowToast("Falha no checkpoint: " + exception.Message, true);
                Debug.LogException(exception);
            }
        }

        private void OnResourceChanged(string _, int __) => ui?.RefreshCatalog();
        private void OnRecipeDiscovered(CraftingRecipe _) => ui?.RefreshCatalog();
    }
}
