using System;
using System.Collections.Generic;
using Duskborn.Core;
using Duskborn.Gameplay.Loot;
using UnityEngine;

namespace Duskborn.Gameplay.Crafting
{
    /// <summary>
    /// Rastreia quais receitas o jogador já descobriu durante a run.
    /// Receitas T1 são sempre descobertas. Demais receitas são desbloqueadas
    /// automaticamente quando o jogador obtém pela primeira vez um material-chave.
    /// </summary>
    public class RecipeDiscoveryTracker : MonoBehaviour
    {
        public event Action<CraftingRecipe> OnRecipeDiscovered;

        private readonly HashSet<string> _discoveredIds = new();
        private readonly HashSet<string> _knownMaterials = new();
        private CraftingRecipe[] _allRecipes;
        private ResourceInventory _resources;

        private void Start()
        {
            _resources = GetComponent<ResourceInventory>();
            if (_resources != null)
                _resources.ResourceChanged += OnResourceChanged;

            // Carrega todas as receitas disponíveis
            _allRecipes = Resources.LoadAll<CraftingRecipe>("Crafting");

            // Descobre todas as receitas T1 (sempre disponíveis) e receitas marcadas como isAlwaysDiscovered
            foreach (var recipe in _allRecipes)
            {
                if (recipe == null) continue;
                if (recipe.Tier == CraftingTier.Primitivo || recipe.IsAlwaysDiscovered)
                    _discoveredIds.Add(recipe.RecipeId);
            }

            DuskLog.Log(LogChannel.Inventory, $"RecipeDiscoveryTracker: {_discoveredIds.Count} receitas iniciais descobertas.");
        }

        private void OnDestroy()
        {
            if (_resources != null)
                _resources.ResourceChanged -= OnResourceChanged;
        }

        /// <summary>
        /// Verifica se uma receita já foi descoberta pelo jogador.
        /// </summary>
        public bool IsDiscovered(CraftingRecipe recipe)
        {
            if (recipe == null) return false;
            if (recipe.Tier == CraftingTier.Primitivo || recipe.IsAlwaysDiscovered) return true;
            return _discoveredIds.Contains(recipe.RecipeId);
        }

        /// <summary>
        /// Verifica se uma receita usa pelo menos um material conhecido
        /// (para mostrar como silhueta/dica na UI).
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
        /// Força a descoberta de uma receita específica (ex: drops de scroll, eventos).
        /// </summary>
        public void ForceDiscover(CraftingRecipe recipe)
        {
            if (recipe == null) return;
            if (_discoveredIds.Add(recipe.RecipeId))
            {
                DuskLog.Log(LogChannel.Inventory, $"Receita descoberta: {recipe.RecipeName}");
                OnRecipeDiscovered?.Invoke(recipe);
            }
        }

        private void OnResourceChanged(string resourceId, int newTotal)
        {
            if (!_knownMaterials.Add(resourceId)) return; // Já conhecido

            // Verifica todas as receitas para ver se alguma nova pode ser descoberta
            if (_allRecipes == null) return;
            foreach (var recipe in _allRecipes)
            {
                if (recipe == null) continue;
                if (_discoveredIds.Contains(recipe.RecipeId)) continue;

                // Receita é descoberta quando o jogador obtém qualquer um dos seus ingredientes
                foreach (var ing in recipe.Ingredients)
                {
                    if (ing.material != null && ing.material.Id == resourceId)
                    {
                        _discoveredIds.Add(recipe.RecipeId);
                        DuskLog.Log(LogChannel.Inventory, $"Nova receita descoberta: {recipe.RecipeName} (via {resourceId})");
                        OnRecipeDiscovered?.Invoke(recipe);
                        break;
                    }
                }
            }
        }
    }
}
