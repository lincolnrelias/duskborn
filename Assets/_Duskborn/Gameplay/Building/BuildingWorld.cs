using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Duskborn.Core;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Duskborn.Gameplay.Building
{
    [Serializable] public sealed class BuildingCommand
    {
        public string action, definition, instance, recipe, material, slot;
        public Vector3 position;
        public float yaw;
        public int amount;
    }
    public sealed class BuildingWorld : MonoBehaviour
    {
        public static BuildingWorld Instance { get; private set; }
        public readonly Dictionary<string, PlacedBuilding> Buildings = new();
        public BuildableDefinition[] Definitions { get; private set; }
        private static CraftingRecipe[] recipes;
        private float nextSync;
        private bool loadedCheckpoint;
        private int WorldSeed => FindAnyObjectByType<ChunkGridManager>() is ChunkGridManager terrain ? terrain.ActiveSeed : (GameSession.Instance != null ? GameSession.Instance.Seed : 0);
        public static BuildingWorld Ensure()
        {
            if (Instance == null) Instance = new GameObject("BuildingWorld").AddComponent<BuildingWorld>();
            return Instance;
        }
        private void Awake() { Instance = this; Definitions = Resources.LoadAll<BuildableDefinition>("Building"); recipes = Resources.LoadAll<CraftingRecipe>("Crafting"); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public static CraftingRecipe Recipe(string id)
        {
            recipes ??= Resources.LoadAll<CraftingRecipe>("Crafting");
            return Array.Find(recipes, r => r.RecipeId == id);
        }
        public BuildableDefinition Definition(string id) => Array.Find(Definitions, d => d.id == id);
        private void Update()
        {
            if (!FishNet.InstanceFinder.IsServerStarted) return;
            foreach (var b in Buildings.Values) b.Tick(Time.deltaTime);
            if (Time.unscaledTime >= nextSync) { nextSync = Time.unscaledTime + 1; Broadcast(); }
        }
        public void Broadcast()
        {
            string json = JsonUtility.ToJson(Capture());
            foreach (var player in PlayerInteractor.BuildingPeers) if (player != null) player.SendBuildingSnapshot(json);
        }
        public BuildingSnapshot Capture()
        {
            return new BuildingSnapshot { seed = WorldSeed,
                scene = SceneManager.GetActiveScene().name, buildings = Buildings.Values.Select(b => b.State).ToList() };
        }
        public void ApplySnapshot(BuildingSnapshot snapshot)
        {
            var ids = new HashSet<string>(snapshot.buildings.Select(b => b.instanceId));
            foreach (var id in Buildings.Keys.ToArray()) if (!ids.Contains(id)) Remove(id);
            foreach (var state in snapshot.buildings)
                if (Buildings.TryGetValue(state.instanceId, out var existing)) existing.Apply(state);
                else Create(state);
        }
        public PlacedBuilding Create(BuildingState state)
        {
            var d = Definition(state.definitionId);
            if (d == null) throw new InvalidDataException("Definição ausente: " + state.definitionId);
            if (!BuildableBounds.TryGet(d, out var bounds)) throw new InvalidDataException("Modelo da construção sem malha: " + d.displayName);
            var root = CreateVisual(d);
            root.name = d.displayName;
            var box = root.AddComponent<BoxCollider>(); box.size = bounds.size; box.center = bounds.center;
            var placed = root.AddComponent<PlacedBuilding>(); placed.Initialize(d, state);
            root.AddComponent<Workbench>().Configure(d.station, d.displayName);
            root.transform.SetParent(transform, true);
            Buildings.Add(state.instanceId, placed);
            Physics.SyncTransforms();
            return placed;
        }
        public void Remove(string id)
        {
            if (!Buildings.Remove(id, out var b)) return;
            b.gameObject.SetActive(false); Destroy(b.gameObject);
        }
        // Copy only graphics; prefab scripts, colliders, particles and network identities never run in previews.
        public static GameObject CreateVisual(BuildableDefinition d)
        {
            var root = new GameObject(d.displayName + " Visual");
            if (d.prefab != null) CopyVisual(d.prefab.transform, root.transform, true);
            if (root.GetComponentInChildren<Renderer>() == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(root.transform, false);
                var collider = cube.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            }
            return root;
        }
        private static void CopyVisual(Transform source, Transform target, bool root)
        {
            if (!root) { target.localPosition = source.localPosition; target.localRotation = source.localRotation; target.localScale = source.localScale; }
            else target.localScale = source.localScale;
            var mesh = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
            if (mesh != null && renderer != null)
            {
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                target.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            foreach (Transform child in source)
            {
                var go = new GameObject(child.name); go.transform.SetParent(target, false); CopyVisual(child, go.transform, false);
            }
        }
        public string Validate(BuildingCommand command, PlayerInteractor player, out Dictionary<string, int> costs)
        {
            costs = new();
            if (player == null || player.GetComponent<PlayerStats>()?.IsAlive != true) return "Jogador indisponível.";
            if (command == null || !float.IsFinite(command.yaw)) return "Pedido inválido.";
            if (command.action == "place")
            {
                var d = Definition(command.definition);
                if (d == null) return "Construção não encontrada no catálogo do servidor: " + command.definition;
                if (d.prefab == null) return "Modelo da construção ausente: " + d.displayName;
                if (!d.allowMultiple && Buildings.Values.Any(b => b.Definition == d)) return "Esta construção já existe.";
                // Discovery is client-authoritative, like the existing crafting inventory, checked at payment.
                var reason = PlacementValidator.Validate(d, command.position, Quaternion.Euler(0, command.yaw, 0), player.transform.position);
                if (reason != null) return reason;
                if (!MaterialCosts.Aggregate(d.costs, out costs)) return "Custo inválido.";
                return null;
            }
            if (string.IsNullOrEmpty(command.instance) || !Buildings.TryGetValue(command.instance, out var station)) return "Estação não encontrada.";
            if (Vector3.Distance(player.transform.position, station.transform.position) > 4) return "Aproxime-se da estação.";
            switch (command.action)
            {
                case "move":
                    if (!station.Definition.movable) return "Esta estação não pode ser movida.";
                    return PlacementValidator.Validate(station.Definition, command.position, Quaternion.Euler(0, command.yaw, 0), player.transform.position, station.transform);
                case "dismantle":
                    return !station.Definition.dismantlable ? "Desmontagem indisponível." : !station.Empty ? "Retire os materiais e conclua a fila antes de desmontar." : null;
                case "queue":
                    var recipe = Recipe(command.recipe);
                    if (recipe == null || recipe.ProcessingSeconds <= 0 || recipe.RequiredStation != station.Definition.station || !(recipe.OutputItem is InventorySystem.Data.MaterialDefinition)) return "Processo incompatível.";
                    if (station.State.jobs.Count >= station.Definition.queueCapacity) return "Fila cheia.";
                    if (!MaterialCosts.Aggregate(recipe.Ingredients, recipe.FuelIngredients, out costs)) return "Receita inválida.";
                    return null;
                case "load":
                    var loadRecipe = Recipe(command.recipe);
                    if (loadRecipe == null || loadRecipe.ProcessingSeconds <= 0 || loadRecipe.RequiredStation != station.Definition.station ||
                        !(loadRecipe.OutputItem is InventorySystem.Data.MaterialDefinition)) return "Processo incompatível.";
                    if (command.amount <= 0 || string.IsNullOrEmpty(command.material)) return "Material inválido.";
                    bool incompatibleLoadedFuel = station.State.fuel.Any(stack =>
                        IngredientAmount(loadRecipe.FuelIngredients, stack.id) <= 0);
                    if (!string.IsNullOrEmpty(station.State.selectedRecipe) && station.State.selectedRecipe != loadRecipe.RecipeId &&
                        (station.State.inputs.Count > 0 || incompatibleLoadedFuel || station.State.jobs.Count > 0))
                        return "Esvazie a forja antes de trocar a receita.";
                    bool isFuel = IngredientAmount(loadRecipe.FuelIngredients, command.material) > 0;
                    bool isInput = IngredientAmount(loadRecipe.Ingredients, command.material) > 0;
                    if (!isFuel && !isInput) return "Este material não pertence à receita selecionada.";
                    int loaded = isFuel ? station.FuelAmount(command.material) : station.InputAmount(command.material);
                    if (command.amount > PlacedBuilding.SlotStackCapacity - loaded) return "O slot aceita no máximo 64 unidades.";
                    costs[command.material] = command.amount;
                    return null;
                case "unload":
                    if (command.amount <= 0 || string.IsNullOrEmpty(command.material) || (command.slot != "input" && command.slot != "fuel")) return "Retirada inválida.";
                    int available = command.slot == "fuel" ? station.FuelAmount(command.material) : station.InputAmount(command.material);
                    return available < command.amount ? "Quantidade indisponível no slot." : null;
                case "collect": return station.State.contents.Count == 0 ? "Nenhum material disponível." : null;
                case "deposit":
                    if (!station.Definition.storage || command.amount <= 0 || command.amount > station.Definition.capacity - station.StoredCount || string.IsNullOrEmpty(command.material)) return "Depósito inválido ou armazenamento cheio.";
                    costs[command.material] = command.amount; return null;
                case "withdraw":
                    if (!station.Definition.storage || command.amount <= 0 || string.IsNullOrEmpty(command.material)) return "Retirada inválida.";
                    var stored = station.State.contents.Find(stack => stack.id == command.material);
                    return stored == null || stored.amount < command.amount ? "Quantidade indisponível no baú." : null;
                default: return "Ação desconhecida.";
            }
        }
        public Dictionary<string, int> Commit(BuildingCommand command)
        {
            var credits = new Dictionary<string, int>();
            if (command.action == "place")
            {
                Create(new BuildingState { instanceId = Guid.NewGuid().ToString("N"), definitionId = command.definition, position = command.position, yaw = command.yaw });
                return credits;
            }
            var b = Buildings[command.instance];
            switch (command.action)
            {
                case "move": b.State.position = command.position; b.State.yaw = command.yaw; b.Apply(b.State); Physics.SyncTransforms(); break;
                case "dismantle": MaterialCosts.Aggregate(b.Definition.costs, out credits); Remove(command.instance); break;
                case "queue": var recipe = Recipe(command.recipe); b.State.jobs.Add(new ProcessingJob { recipe = recipe.RecipeId, remaining = recipe.ProcessingSeconds }); break;
                case "load":
                    var selected = Recipe(command.recipe);
                    b.State.selectedRecipe = selected.RecipeId;
                    if (IngredientAmount(selected.FuelIngredients, command.material) > 0) b.AddFuel(command.material, command.amount);
                    else b.AddInput(command.material, command.amount);
                    break;
                case "unload":
                    bool removed = command.slot == "fuel"
                        ? b.TryRemoveFuel(command.material, command.amount)
                        : b.TryRemoveInput(command.material, command.amount);
                    if (!removed) throw new InvalidOperationException("Validated slot withdrawal became unavailable.");
                    credits[command.material] = command.amount;
                    break;
                case "collect": foreach (var s in b.State.contents) credits[s.id] = s.amount; b.State.contents.Clear(); break;
                case "deposit": b.Add(command.material, command.amount); break;
                case "withdraw":
                    if (!b.TryRemove(command.material, command.amount)) throw new InvalidOperationException("Validated withdrawal became unavailable.");
                    credits[command.material] = command.amount;
                    break;
            }
            return credits;
        }
        public string SavePath => Path.Combine(Application.persistentDataPath, "infrastructure-" + SceneManager.GetActiveScene().name + "-" + WorldSeed + ".json");
        public void Save(ResourceInventory inventory)
        {
            var snapshot = Capture();
            snapshot.hostResources = inventory.Counts.Select(p => new MaterialStack { id = p.Key, amount = p.Value }).ToList();
            var discovery = inventory.GetComponent<RecipeDiscoveryTracker>();
            if (discovery != null) { snapshot.discoveredRecipes = discovery.CaptureDiscoveries(); snapshot.knownMaterials = discovery.CaptureKnownMaterials(); }
            var json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(SavePath + ".tmp", json);
            if (File.Exists(SavePath)) File.Replace(SavePath + ".tmp", SavePath, SavePath + ".bak");
            else File.Move(SavePath + ".tmp", SavePath);
        }
        public void Load(ResourceInventory inventory)
        {
            if (loadedCheckpoint || Buildings.Count != 0 || inventory.Revision != 0) throw new InvalidOperationException("Carregue no início de uma sessão, antes de coletar ou gastar materiais.");
            var snapshot = JsonUtility.FromJson<BuildingSnapshot>(File.ReadAllText(SavePath));
            var current = Capture();
            if (snapshot == null || snapshot.version != 1 || snapshot.seed != current.seed || snapshot.scene != current.scene || snapshot.buildings == null || snapshot.hostResources == null) throw new InvalidDataException("Save incompatível com este mundo.");
            var ids = new HashSet<string>();
            foreach (var b in snapshot.buildings)
            {
                if (b == null || string.IsNullOrEmpty(b.instanceId) || !ids.Add(b.instanceId) || Definition(b.definitionId) == null || !PlacementValidator.Finite(b.position) || !float.IsFinite(b.yaw) || b.jobs == null || b.contents == null) throw new InvalidDataException("Construção inválida no save.");
                b.inputs ??= new List<MaterialStack>();
                b.fuel ??= new List<MaterialStack>();
                foreach (var job in b.jobs) if (job == null || Recipe(job.recipe) == null || !float.IsFinite(job.remaining) || job.remaining < 0) throw new InvalidDataException("Processo inválido no save.");
                ValidateStacks(b.contents);
                ValidateStacks(b.inputs);
                ValidateStacks(b.fuel);
                var definition = Definition(b.definitionId);
                if (b.fuelCharges < 0 || b.fuelCharges > PlacedBuilding.SlotStackCapacity * 2 ||
                    b.contents.Sum(s => (long)s.amount) > definition.capacity || b.jobs.Count > definition.queueCapacity ||
                    b.inputs.Any(s => s.amount > PlacedBuilding.SlotStackCapacity) || b.fuel.Any(s => s.amount > PlacedBuilding.SlotStackCapacity)) throw new InvalidDataException("Capacidade inválida no save.");
                if (!string.IsNullOrEmpty(b.selectedRecipe))
                {
                    var selected = Recipe(b.selectedRecipe);
                    if (selected == null || selected.RequiredStation != definition.station || selected.ProcessingSeconds <= 0 ||
                        b.inputs.Any(s => IngredientAmount(selected.Ingredients, s.id) == 0) ||
                        b.fuel.Any(s => IngredientAmount(selected.FuelIngredients, s.id) == 0)) throw new InvalidDataException("Slots de processamento incompatíveis no save.");
                }
                else if (b.inputs.Count > 0 || b.fuel.Count > 0)
                    throw new InvalidDataException("Slots de processamento sem receita no save.");
                foreach (var job in b.jobs)
                {
                    var recipe = Recipe(job.recipe);
                    if (recipe.RequiredStation != definition.station || recipe.ProcessingSeconds <= 0 || !(recipe.OutputItem is InventorySystem.Data.MaterialDefinition) || job.remaining > recipe.ProcessingSeconds) throw new InvalidDataException("Receita incompatível no save.");
                }
            }
            ValidateStacks(snapshot.hostResources);
            // Validate the complete file before touching live state. Load includes the material wallet so refunds cannot be duplicated.
            ApplySnapshot(snapshot);
            inventory.Restore(snapshot.hostResources.ToDictionary(s => s.id, s => s.amount));
            inventory.GetComponent<RecipeDiscoveryTracker>()?.RestoreDiscoveries(snapshot.discoveredRecipes, snapshot.knownMaterials);
            loadedCheckpoint = true;
            Broadcast();
        }
        private static void ValidateStacks(List<MaterialStack> stacks)
        {
            var ids = new HashSet<string>();
            foreach (var s in stacks) if (s == null || string.IsNullOrEmpty(s.id) || s.amount < 0 || !ids.Add(s.id)) throw new InvalidDataException("Materiais inválidos no save.");
        }

        private static int IngredientAmount(IReadOnlyList<CraftingIngredient> ingredients, string material)
        {
            if (ingredients == null || string.IsNullOrEmpty(material)) return 0;
            int amount = 0;
            foreach (var ingredient in ingredients)
                if (ingredient.material != null && ingredient.material.Id == material) amount += ingredient.amount;
            return amount;
        }
    }
}


