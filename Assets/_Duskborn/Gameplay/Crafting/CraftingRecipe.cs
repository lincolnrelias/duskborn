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
        [Tooltip("Definição do recurso/material consumido.")]
        public MaterialDefinition material;

        [Tooltip("Quantidade requerida deste material.")]
        [Min(1)]
        public int amount;
    }

    /// <summary>
    /// ScriptableObject que define uma receita de fabricação no Duskborn.
    /// Especifica os recursos requeridos (madeira, pedra, etc.) e o item resultante (arma, armadura, ferramenta).
    /// </summary>
    [CreateAssetMenu(fileName = "CraftingRecipe", menuName = "Duskborn/Crafting/Recipe")]
    public class CraftingRecipe : ScriptableObject
    {
        [Header("Identificação da Receita")]
        [SerializeField] private string recipeId;
        [SerializeField] private string recipeName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string category = "Ferramentas";
        [SerializeField] private CraftingTier tier = CraftingTier.Primitivo;
        [SerializeField] private CraftingStationType requiredStation = CraftingStationType.Bancada;
        [SerializeField] private bool isAlwaysDiscovered = false;

        [Header("Resultado")]
        [SerializeField] private ItemDefinitionBase outputItem;
        [SerializeField, Min(1)] private int outputAmount = 1;

        [Header("Ingredientes Necessários")]
        [SerializeField] private List<CraftingIngredient> ingredients = new();

        [Header("Combustível de Processamento")]
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
        /// Verifica se o inventário de recursos do jogador possui todos os ingredientes necessários.
        /// </summary>
        public bool CanCraft(ResourceInventory resources) => Building.MaterialCosts.CanPay(resources, ingredients, fuelIngredients);
        public bool TrySpendIngredients(ResourceInventory resources) => Building.MaterialCosts.Spend(resources, ingredients, fuelIngredients);
        /// <summary>
        /// Instancia o item em tempo de execução para colocação no inventário ou action bar.
        /// </summary>
        public IInventoryItem CreateOutputItem()
        {
            if (outputItem == null) return null;
            return outputItem.CreateRuntimeItem();
        }
    }
}
