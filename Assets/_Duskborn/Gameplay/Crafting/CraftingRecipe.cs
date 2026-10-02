using System;
using System.Collections.Generic;
using InventorySystem.Core;
using InventorySystem.Data;
using Duskborn.Gameplay.Loot;
using UnityEngine;

namespace Duskborn.Gameplay.Crafting
{
    [Serializable]
    public struct CraftingIngredient
    {
        [Tooltip("Definition of the consumed resource / material.")]
        public MaterialDefinition material;

        [Tooltip("Required amount of this material.")]
        [Min(1)]
        public int amount;
    }

    /// <summary>
    /// ScriptableObject defining a crafting recipe in Duskborn.
    /// Specifies required resources (wood, stone, etc.) and the resulting item (weapon, armor, tool).
    /// </summary>
    [CreateAssetMenu(fileName = "CraftingRecipe", menuName = "Duskborn/Crafting/Recipe")]
    public class CraftingRecipe : ScriptableObject
    {
        [Header("Recipe Identification")]
        [SerializeField] private string recipeId;
        [SerializeField] private string recipeName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string category = "Tools";
        [SerializeField] private CraftingTier tier = CraftingTier.Primitive;
        [SerializeField] private CraftingStationType requiredStation = CraftingStationType.Workbench;
        [SerializeField] private bool isAlwaysDiscovered = false;

        [Header("Output")]
        [SerializeField] private ItemDefinitionBase outputItem;
        [SerializeField, Min(1)] private int outputAmount = 1;

        [Header("Required Ingredients")]
        [SerializeField] private List<CraftingIngredient> ingredients = new();

        [Header("Processing Fuel")]
        [SerializeField] private List<CraftingIngredient> fuelIngredients = new();

        public string RecipeId => string.IsNullOrEmpty(recipeId) ? (outputItem != null ? outputItem.Id : name) : recipeId;
        public string RecipeName => string.IsNullOrEmpty(recipeName) ? (outputItem != null ? outputItem.DisplayName : name) : recipeName;
        public string Description => string.IsNullOrEmpty(description) ? (outputItem != null ? outputItem.Description : string.Empty) : description;
        public string Category => category;
        public CraftingTier Tier => tier;
        public CraftingStationType RequiredStation => requiredStation;
        public bool IsAlwaysDiscovered => isAlwaysDiscovered;
        public ItemDefinitionBase OutputItem => outputItem;
        public int OutputAmount => outputAmount;
        public IReadOnlyList<CraftingIngredient> Ingredients => ingredients;
        public IReadOnlyList<CraftingIngredient> FuelIngredients => fuelIngredients;

        [SerializeField, Min(0)] private float processingSeconds;
        public float ProcessingSeconds => processingSeconds;
        public Texture2D Icon => outputItem != null ? outputItem.Icon : null;

        /// <summary>
        /// Check whether the player's resource inventory has all required ingredients.
        /// </summary>
        public bool CanCraft(ResourceInventory resources) => Building.MaterialCosts.CanPay(resources, ingredients, fuelIngredients);
        public bool TrySpendIngredients(ResourceInventory resources) => Building.MaterialCosts.Spend(resources, ingredients, fuelIngredients);
        /// <summary>
        /// Instantiate the runtime item for placement in inventory or the action bar.
        /// </summary>
        public IInventoryItem CreateOutputItem()
        {
            if (outputItem == null) return null;
            return outputItem.CreateRuntimeItem();
        }
    }
}
