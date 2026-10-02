using System;
using System.Collections.Generic;
using Duskborn.Core;
using Duskborn.Gameplay.Loot;
using UnityEngine;

namespace Duskborn.Gameplay.Crafting
{
    /// <summary>
    /// Tracks recipes the player has discovered during the run.
    /// T1 recipes are always discovered. Other recipes unlock
    /// automatically when the player first obtains a key material.
    /// </summary>
    public class RecipeDiscoveryTracker : MonoBehaviour
    {
        public event Action<CraftingRecipe> OnRecipeDiscovered;

        private readonly HashSet<string> _discoveredIds = new();
        private readonly HashSet<string> _knownMaterials = new();
        private CraftingRecipe[] _allRecipes;
        private ResourceInventory _resources;

        public List<string> CaptureDiscoveries() => new List<string>(_discoveredIds);
        public List<string> CaptureKnownMaterials() => new List<string>(_knownMaterials);
        public void RestoreDiscoveries(IEnumerable<string> recipes, IEnumerable<string> materials)
        {
            _discoveredIds.Clear(); _knownMaterials.Clear();
            if (recipes != null) foreach (var id in recipes) _discoveredIds.Add(id);
            if (materials != null) foreach (var id in materials) _knownMaterials.Add(id);
        }
        private void Start()
        {
            _resources = GetComponent<ResourceInventory>();
            if (_resources != null)
                _resources.ResourceChanged += OnResourceChanged;

            // Load all available recipes.
            _allRecipes = Resources.LoadAll<CraftingRecipe>("Crafting");

            // Discover all T1 recipes (always available) and recipes marked isAlwaysDiscovered.
            foreach (var recipe in _allRecipes)
            {
                if (recipe == null) continue;
                if (recipe.Tier == CraftingTier.Primitive || recipe.IsAlwaysDiscovered)
                    _discoveredIds.Add(recipe.RecipeId);
            }

            if (_resources != null) foreach (var entry in _resources.Counts) OnResourceChanged(entry.Key, entry.Value);
            DuskLog.Log(LogChannel.Inventory, $"RecipeDiscoveryTracker: {_discoveredIds.Count} starting recipes discovered.");
        }

        private void OnDestroy()
        {
            if (_resources != null)
                _resources.ResourceChanged -= OnResourceChanged;
        }

        /// <summary>
        /// Check whether the player has already discovered a recipe.
        /// </summary>
        public bool IsDiscovered(CraftingRecipe recipe)
        {
            if (recipe == null) return false;
            if (recipe.Tier == CraftingTier.Primitive || recipe.IsAlwaysDiscovered) return true;
            return _discoveredIds.Contains(recipe.RecipeId);
        }

        /// <summary>
        /// Check whether a recipe uses at least one known material
        /// (to show as a silhouette / hint in the UI).
        /// </summary>
        public bool HasPartialKnowledge(CraftingRecipe recipe)
        {
            if (recipe == null) return false;
            foreach (var ing in recipe.Ingredients)
            {
                if (ing.material != null && _knownMaterials.Contains(ing.material.Id))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Force discovery of a specific recipe (e.g. scroll drops, events).
        /// </summary>
        public void ForceDiscover(CraftingRecipe recipe)
        {
            if (recipe == null) return;
            if (_discoveredIds.Add(recipe.RecipeId))
            {
                DuskLog.Log(LogChannel.Inventory, $"Recipe discovered: {recipe.RecipeName}");
                OnRecipeDiscovered?.Invoke(recipe);
            }
        }

        private void OnResourceChanged(string resourceId, int newTotal)
        {
            if (newTotal <= 0 || !_knownMaterials.Add(resourceId)) return; // Already known.

            // Check all recipes for any newly discoverable recipe.
            if (_allRecipes == null) return;
            foreach (var recipe in _allRecipes)
            {
                if (recipe == null) continue;
                if (_discoveredIds.Contains(recipe.RecipeId)) continue;

                // A recipe is discovered when the player obtains any of its ingredients or fuels.
                bool discovered = false;
                foreach (var ing in recipe.Ingredients)
                {
                    if (ing.material != null && ing.material.Id == resourceId)
                    {
                        discovered = true;
                        break;
                    }
                }
                if (!discovered && recipe.FuelIngredients != null)
                {
                    foreach (var fuel in recipe.FuelIngredients)
                    {
                        if (fuel.material != null && fuel.material.Id == resourceId)
                        {
                            discovered = true;
                            break;
                        }
                    }
                }
                if (discovered)
                {
                    _discoveredIds.Add(recipe.RecipeId);
                    DuskLog.Log(LogChannel.Inventory, $"New recipe discovered: {recipe.RecipeName} (via {resourceId})");
                    OnRecipeDiscovered?.Invoke(recipe);
                }
            }
        }
    }
}
