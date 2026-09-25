using System;
using System.Collections.Generic;
using Duskborn.Gameplay.Crafting;
using UnityEngine;
namespace Duskborn.Gameplay.Building
{
    [Serializable] public sealed class MaterialStack { public string id; public int amount; }
    [Serializable] public sealed class ProcessingJob { public string recipe; public float remaining; }
    [Serializable] public sealed class BuildingState
    {
        public string instanceId;
        public string definitionId;
        public Vector3 position;
        public float yaw;
        public int upgradeLevel;
        public List<MaterialStack> contents = new();
        public List<MaterialStack> inputs = new();
        public List<MaterialStack> fuel = new();
        // Fuel is consumed into two forge burns at a time. This persists the
        // remaining burn when the first 2-ore refinement finishes.
        public int fuelCharges;
        public List<ProcessingJob> jobs = new();
        public string selectedRecipe;
    }
    [Serializable] public sealed class BuildingSnapshot
    {
        public int version = 1;
        public int seed;
        public string scene;
        public List<BuildingState> buildings = new();
        public List<MaterialStack> hostResources = new();
        public List<string> discoveredRecipes = new();
        public List<string> knownMaterials = new();
    }
    public sealed class PlacedBuilding : MonoBehaviour
    {
        public const int SlotStackCapacity = 64;
        public BuildableDefinition Definition { get; private set; }
        public BuildingState State { get; private set; }
        public bool Empty => State.jobs.Count == 0 && State.contents.Count == 0 && State.inputs.Count == 0 && State.fuel.Count == 0;
        public void Initialize(BuildableDefinition definition, BuildingState state)
        {
            Definition = definition;
            Apply(state);
        }
        public void Apply(BuildingState state)
        {
            State = state;
            State.contents ??= new List<MaterialStack>();
            State.inputs ??= new List<MaterialStack>();
            State.fuel ??= new List<MaterialStack>();
            State.jobs ??= new List<ProcessingJob>();
            var groundOffset = BuildableBounds.TryGet(Definition, out var bounds)
                ? BuildableBounds.GroundOffset(bounds)
                : Vector3.zero;
            transform.SetPositionAndRotation(state.position + groundOffset, Quaternion.Euler(0, state.yaw, 0));
        }
        public int StoredCount
        {
            get { int n = 0; foreach (var s in State.contents) n += s.amount; return n; }
        }
        public void Add(string id, int amount)
        {
            AddTo(State.contents, id, amount);
        }
        public void AddInput(string id, int amount) => AddTo(State.inputs, id, amount);
        public void AddFuel(string id, int amount) => AddTo(State.fuel, id, amount);
        public int InputAmount(string id) => Amount(State.inputs, id);
        public int FuelAmount(string id) => Amount(State.fuel, id);
        public int FuelCharges => State.fuelCharges;
        public bool TryRemove(string id, int amount)
        {
            return TryRemoveFrom(State.contents, id, amount);
        }
        public bool TryRemoveInput(string id, int amount) => TryRemoveFrom(State.inputs, id, amount);
        public bool TryRemoveFuel(string id, int amount) => TryRemoveFrom(State.fuel, id, amount);

        // Tick only on the server. Legacy queued jobs were already paid. Slot-based
        // jobs consume their inputs and fuel atomically when a burn begins.
        public void Tick(float delta)
        {
            float budget = Mathf.Max(0, delta) * Definition.processingSpeed;
            while (budget > 0)
            {
                if (State.jobs.Count == 0 && !TryBeginSlottedProcess()) return;
                var job = State.jobs[0];
                var recipe = BuildingWorld.Recipe(job.recipe);
                if (recipe == null || recipe.OutputItem == null) return;
                if (StoredCount + recipe.OutputAmount > Definition.capacity) return;
                float used = Mathf.Min(budget, job.remaining);
                job.remaining -= used;
                budget -= used;
                if (job.remaining > 0) return;
                Add(recipe.OutputItem.Id, recipe.OutputAmount);
                State.jobs.RemoveAt(0);
                if (budget <= 0) return;
            }
        }

        private bool TryBeginSlottedProcess()
        {
            if (string.IsNullOrEmpty(State.selectedRecipe)) return false;
            var recipe = BuildingWorld.Recipe(State.selectedRecipe);
            if (recipe == null || recipe.OutputItem == null || recipe.ProcessingSeconds <= 0) return false;
            if (StoredCount + recipe.OutputAmount > Definition.capacity) return false;
            if (!Contains(State.inputs, recipe.Ingredients)) return false;
            if (State.fuelCharges <= 0)
            {
                if (!Contains(State.fuel, recipe.FuelIngredients)) return false;
                Consume(State.fuel, recipe.FuelIngredients);
                State.fuelCharges = FuelBurnsPerLoadedFuel(recipe);
            }
            Consume(State.inputs, recipe.Ingredients);
            State.fuelCharges--;
            State.jobs.Add(new ProcessingJob { recipe = recipe.RecipeId, remaining = recipe.ProcessingSeconds });
            return true;
        }

        private int FuelBurnsPerLoadedFuel(CraftingRecipe recipe) =>
            Definition.station == CraftingStationType.Forja && recipe.FuelIngredients.Count > 0 ? 2 : 1;

        private static int Amount(List<MaterialStack> stacks, string id) =>
            stacks.Find(value => value.id == id)?.amount ?? 0;

        private static void AddTo(List<MaterialStack> stacks, string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;
            var stack = stacks.Find(value => value.id == id);
            if (stack == null) stacks.Add(new MaterialStack { id = id, amount = amount });
            else stack.amount += amount;
        }

        private static bool TryRemoveFrom(List<MaterialStack> stacks, string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return false;
            var stack = stacks.Find(value => value.id == id);
            if (stack == null || stack.amount < amount) return false;
            stack.amount -= amount;
            if (stack.amount == 0) stacks.Remove(stack);
            return true;
        }

        private static bool Contains(List<MaterialStack> stacks, IReadOnlyList<CraftingIngredient> requirements)
        {
            if (requirements == null) return true;
            foreach (var requirement in requirements)
                if (requirement.material == null || requirement.amount <= 0 ||
                    Amount(stacks, requirement.material.Id) < requirement.amount) return false;
            return true;
        }

        private static void Consume(List<MaterialStack> stacks, IReadOnlyList<CraftingIngredient> requirements)
        {
            if (requirements == null) return;
            foreach (var requirement in requirements)
                TryRemoveFrom(stacks, requirement.material.Id, requirement.amount);
        }
    }
}

