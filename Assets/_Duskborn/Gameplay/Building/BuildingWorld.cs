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
        private readonly Dictionary<string, string> initialSceneFurnaces = new();
        private int WorldSeed => FindAnyObjectByType<ChunkGridManager>() is ChunkGridManager terrain ? terrain.ActiveSeed : (GameSession.Instance != null ? GameSession.Instance.Seed : 0);
        public static BuildingWorld Ensure()
        {
            if (Instance == null) Instance = new GameObject("BuildingWorld").AddComponent<BuildingWorld>();
            return Instance;
        }
        private void Awake()
        {
            Instance = this;
            Definitions = Resources.LoadAll<BuildableDefinition>("Building");
            recipes = Resources.LoadAll<CraftingRecipe>("Crafting");
            RegisterSceneFurnaces();
        }

        // Scene-authored stations bypass Create. Register them on both peers using
        // a deterministic scene hierarchy id, so snapshots update the existing model.
        private void RegisterSceneFurnaces()
        {
            var definition = Array.Find(Definitions, d => d.station == CraftingStationType.Forge && !d.storage);
            if (definition == null || !BuildableBounds.TryGet(definition, out var bounds)) return;
            foreach (var station in FindObjectsByType<Workbench>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (station.StationType != CraftingStationType.Forge || station.GetComponent<PlacedBuilding>() != null ||
                    !station.gameObject.scene.IsValid() || station.gameObject.scene != gameObject.scene) continue;
                string path = "";
                for (var node = station.transform; node != null; node = node.parent)
                    path = "/" + node.GetSiblingIndex() + ":" + node.name + path;
                var state = new BuildingState
                {
                    instanceId = "scene-forge:" + station.gameObject.scene.path + path,
                    definitionId = definition.id,
                    position = station.transform.position - BuildableBounds.GroundOffset(bounds),
                    yaw = station.transform.eulerAngles.y
                };
                var placed = station.gameObject.AddComponent<PlacedBuilding>();
                placed.Initialize(definition, state);
                Buildings.Add(state.instanceId, placed);
                initialSceneFurnaces.Add(state.instanceId, JsonUtility.ToJson(state));
                AttachOperatingEffect(placed);
            }
        }

        private static void AttachOperatingEffect(PlacedBuilding placed)
        {
            var effect = placed.Definition.operatingEffect;
            if (effect == null) return;
            effect.PrepareModel(placed.gameObject);
            // Batch mode can render (tests/captures). Only skip genuinely headless rendering.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var existing = placed.GetComponentInChildren<Duskborn.Effects.FurnaceEffects>(true);
            if (existing == null) existing = Instantiate(effect, placed.transform, false);
            existing.Bind(placed);
        }

        private bool HasChangedBuildings()
        {
            if (Buildings.Count != initialSceneFurnaces.Count) return true;
            foreach (var entry in Buildings)
                if (!initialSceneFurnaces.TryGetValue(entry.Key, out var initial) ||
                    JsonUtility.ToJson(entry.Value.State) != initial) return true;
            return false;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public static CraftingRecipe Recipe(string id)
        {
            recipes ??= Resources.LoadAll<CraftingRecipe>("Crafting");
            // This lookup is also polled by local operating effects; avoid a captured
            // predicate allocation for every furnace on every frame.
            foreach (var recipe in recipes) if (recipe.RecipeId == id) return recipe;
            return null;
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
            if (d == null) throw new InvalidDataException("Missing definition: " + state.definitionId);
            if (!BuildableBounds.TryGet(d, out var bounds)) throw new InvalidDataException("Building model has no mesh: " + d.displayName);
            var root = CreateVisual(d);
            root.name = d.displayName;
            var box = root.AddComponent<BoxCollider>(); box.size = bounds.size; box.center = bounds.center;
            var placed = root.AddComponent<PlacedBuilding>(); placed.Initialize(d, state);
            root.AddComponent<Workbench>().Configure(d.station, d.displayName);
            root.transform.SetParent(transform, true);
            Buildings.Add(state.instanceId, placed);
            // Attach after Workbench.Awake caches the model outline renderers.
            // Both restore and late-join snapshots use this same creation path.
            AttachOperatingEffect(placed);
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
            if (d.operatingEffect != null) d.operatingEffect.PrepareModel(root);
            foreach (var anim in root.GetComponentsInChildren<Animator>(true))
            {
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.Rebind();
                anim.Update(0f);
            }
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
            var skinned = source.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                var targetSkinned = target.gameObject.AddComponent<SkinnedMeshRenderer>();
                targetSkinned.sharedMesh = skinned.sharedMesh;
                targetSkinned.sharedMaterials = skinned.sharedMaterials;
                targetSkinned.updateWhenOffscreen = true;
                if (skinned.sharedMesh != null)
                {
                    for (int i = 0; i < skinned.sharedMesh.blendShapeCount; i++)
                        targetSkinned.SetBlendShapeWeight(i, skinned.GetBlendShapeWeight(i));
                }
            }
            var anim = source.GetComponent<Animator>();
            if (anim != null)
            {
                var targetAnim = target.gameObject.AddComponent<Animator>();
                targetAnim.runtimeAnimatorController = anim.runtimeAnimatorController;
                targetAnim.avatar = anim.avatar;
                targetAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                targetAnim.applyRootMotion = false;
                targetAnim.updateMode = anim.updateMode;
            }
            if (source.GetComponent<Duskborn.Gameplay.Crafting.MoonwellStationAnimator>() != null)
            {
                target.gameObject.AddComponent<Duskborn.Gameplay.Crafting.MoonwellStationAnimator>();
            }
            foreach (Transform child in source)
            {
                var go = new GameObject(child.name); go.transform.SetParent(target, false); CopyVisual(child, go.transform, false);
            }
        }
        public string Validate(BuildingCommand command, PlayerInteractor player, out Dictionary<string, int> costs)
        {
            costs = new();
            if (player == null || player.GetComponent<PlayerStats>()?.IsAlive != true) return "Player unavailable.";
            if (command == null || !float.IsFinite(command.yaw)) return "Invalid request.";
            if (command.action == "place")
            {
                var d = Definition(command.definition);
                if (d == null) return "Building not found in the server catalog: " + command.definition;
                if (d.prefab == null) return "Building model missing: " + d.displayName;
                if (!d.allowMultiple && Buildings.Values.Any(b => b.Definition == d)) return "This building already exists.";
                // Discovery is client-authoritative, like the existing crafting inventory, checked at payment.
                var reason = PlacementValidator.Validate(d, command.position, Quaternion.Euler(0, command.yaw, 0), player.transform.position);
                if (reason != null) return reason;
                if (!MaterialCosts.Aggregate(d.costs, out costs)) return "Invalid cost.";
                return null;
            }
            if (string.IsNullOrEmpty(command.instance) || !Buildings.TryGetValue(command.instance, out var station)) return "Station not found.";
            if (Vector3.Distance(player.transform.position, station.transform.position) > 4) return "Move closer to the station.";
            switch (command.action)
            {
                case "move":
                    if (!station.Definition.movable) return "This station cannot be moved.";
                    return PlacementValidator.Validate(station.Definition, command.position, Quaternion.Euler(0, command.yaw, 0), player.transform.position, station.transform);
                case "dismantle":
                    return !station.Definition.dismantlable ? "Dismantling unavailable." : !station.Empty ? "Withdraw materials and finish the queue before dismantling." : null;
                case "queue":
                    var recipe = Recipe(command.recipe);
                    if (recipe == null || recipe.ProcessingSeconds <= 0 || recipe.RequiredStation != station.Definition.station || !(recipe.OutputItem is InventorySystem.Data.MaterialDefinition)) return "Incompatible process.";
                    if (station.State.jobs.Count >= station.Definition.queueCapacity) return "Queue full.";
                    if (!MaterialCosts.Aggregate(recipe.Ingredients, recipe.FuelIngredients, out costs)) return "Invalid recipe.";
                    return null;
                case "load":
                    var loadRecipe = Recipe(command.recipe);
                    if (loadRecipe == null || loadRecipe.ProcessingSeconds <= 0 || loadRecipe.RequiredStation != station.Definition.station ||
                        !(loadRecipe.OutputItem is InventorySystem.Data.MaterialDefinition)) return "Incompatible process.";
                    if (command.amount <= 0 || string.IsNullOrEmpty(command.material)) return "Invalid material.";
                    bool incompatibleLoadedFuel = station.State.fuel.Any(stack =>
                        IngredientAmount(loadRecipe.FuelIngredients, stack.id) <= 0);
                    if (!string.IsNullOrEmpty(station.State.selectedRecipe) && station.State.selectedRecipe != loadRecipe.RecipeId &&
                        (station.State.inputs.Count > 0 || incompatibleLoadedFuel || station.State.jobs.Count > 0))
                        return "Empty the forge before changing the recipe.";
                    bool isFuel = IngredientAmount(loadRecipe.FuelIngredients, command.material) > 0;
                    bool isInput = IngredientAmount(loadRecipe.Ingredients, command.material) > 0;
                    if (!isFuel && !isInput) return "This material does not belong to the selected recipe.";
                    int loaded = isFuel ? station.FuelAmount(command.material) : station.InputAmount(command.material);
                    if (command.amount > PlacedBuilding.SlotStackCapacity - loaded) return "The slot accepts at most 64 units.";
                    costs[command.material] = command.amount;
                    return null;
                case "unload":
                    if (command.amount <= 0 || string.IsNullOrEmpty(command.material) || (command.slot != "input" && command.slot != "fuel")) return "Invalid withdrawal.";
                    int available = command.slot == "fuel" ? station.FuelAmount(command.material) : station.InputAmount(command.material);
                    return available < command.amount ? "Quantity unavailable in the slot." : null;
                case "collect": return station.State.contents.Count == 0 ? "No materials available." : null;
                case "deposit":
                    if (!station.Definition.storage || command.amount <= 0 || command.amount > station.Definition.capacity - station.StoredCount || string.IsNullOrEmpty(command.material)) return "Invalid deposit or storage full.";
                    costs[command.material] = command.amount; return null;
                case "withdraw":
                    if (!station.Definition.storage || command.amount <= 0 || string.IsNullOrEmpty(command.material)) return "Invalid withdrawal.";
                    var stored = station.State.contents.Find(stack => stack.id == command.material);
                    return stored == null || stored.amount < command.amount ? "Quantity unavailable in the chest." : null;
                default: return "Unknown action.";
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
            if (loadedCheckpoint || HasChangedBuildings() || inventory.Revision != 0) throw new InvalidOperationException("Load at the start of a session, before gathering or spending materials.");
            var snapshot = JsonUtility.FromJson<BuildingSnapshot>(File.ReadAllText(SavePath));
            var current = Capture();
            if (snapshot == null || snapshot.version != 1 || snapshot.seed != current.seed || snapshot.scene != current.scene || snapshot.buildings == null || snapshot.hostResources == null) throw new InvalidDataException("Save is incompatible with this world.");
            var ids = new HashSet<string>();
            foreach (var b in snapshot.buildings)
            {
                if (b == null || string.IsNullOrEmpty(b.instanceId) || !ids.Add(b.instanceId) || Definition(b.definitionId) == null || !PlacementValidator.Finite(b.position) || !float.IsFinite(b.yaw) || b.jobs == null || b.contents == null) throw new InvalidDataException("Invalid building in save.");
                b.inputs ??= new List<MaterialStack>();
                b.fuel ??= new List<MaterialStack>();
                foreach (var job in b.jobs) if (job == null || Recipe(job.recipe) == null || !float.IsFinite(job.remaining) || job.remaining < 0) throw new InvalidDataException("Invalid process in save.");
                ValidateStacks(b.contents);
                ValidateStacks(b.inputs);
                ValidateStacks(b.fuel);
                var definition = Definition(b.definitionId);
                if (b.fuelCharges < 0 || b.fuelCharges > PlacedBuilding.SlotStackCapacity * 2 ||
                    b.contents.Sum(s => (long)s.amount) > definition.capacity || b.jobs.Count > definition.queueCapacity ||
                    b.inputs.Any(s => s.amount > PlacedBuilding.SlotStackCapacity) || b.fuel.Any(s => s.amount > PlacedBuilding.SlotStackCapacity)) throw new InvalidDataException("Invalid capacity in save.");
                if (!string.IsNullOrEmpty(b.selectedRecipe))
                {
                    var selected = Recipe(b.selectedRecipe);
                    if (selected == null || selected.RequiredStation != definition.station || selected.ProcessingSeconds <= 0 ||
                        b.inputs.Any(s => IngredientAmount(selected.Ingredients, s.id) == 0) ||
                        b.fuel.Any(s => IngredientAmount(selected.FuelIngredients, s.id) == 0)) throw new InvalidDataException("Incompatible processing slots in save.");
                }
                else if (b.inputs.Count > 0 || b.fuel.Count > 0)
                    throw new InvalidDataException("Processing slots have no recipe in save.");
                foreach (var job in b.jobs)
                {
                    var recipe = Recipe(job.recipe);
                    if (recipe.RequiredStation != definition.station || recipe.ProcessingSeconds <= 0 || !(recipe.OutputItem is InventorySystem.Data.MaterialDefinition) || job.remaining > recipe.ProcessingSeconds) throw new InvalidDataException("Incompatible recipe in save.");
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
            foreach (var s in stacks) if (s == null || string.IsNullOrEmpty(s.id) || s.amount < 0 || !ids.Add(s.id)) throw new InvalidDataException("Invalid materials in save.");
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


