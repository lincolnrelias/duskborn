using System.Collections.Generic;
using System.Linq;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Loot;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Building
{
    public sealed class BuildablePresentation
    {
        public BuildableDefinition Definition;
        public bool IsUnlocked;
        public bool IsAffordable;
        public bool IsAvailable;
        public IReadOnlyList<CostPresentation> Costs;
        public string DisabledReason;
        public string Capability;
        public bool CanPlace => IsUnlocked && IsAffordable && IsAvailable;
    }

    public sealed class CostPresentation
    {
        public string MaterialId;
        public string DisplayName;
        public Texture2D Icon;
        public int Owned;
        public int Required;
        public int Missing;
    }

    public static class BuildingPresentation
    {
        public static BuildablePresentation Create(
            BuildableDefinition definition,
            ResourceInventory inventory,
            RecipeDiscoveryTracker discovery,
            bool alreadyPlaced)
        {
            var costs = BuildCosts(definition, inventory);
            bool unlocked = definition != null && definition.IsUnlocked(discovery);
            bool affordable = costs.Count > 0 && costs.All(cost => cost.Missing == 0);
            bool available = definition != null && definition.prefab != null &&
                (definition.allowMultiple || !alreadyPlaced);

            string disabledReason = null;
            if (!unlocked)
                disabledReason = definition != null && definition.unlockRecipe != null
                    ? "Discover " + definition.unlockRecipe.RecipeName + " to unlock."
                    : "Building is still locked.";
            else if (definition == null || definition.prefab == null)
                disabledReason = "Building model unavailable.";
            else if (!definition.allowMultiple && alreadyPlaced)
                disabledReason = "This building already exists in this world.";
            else if (!affordable)
                disabledReason = "Missing " + string.Join(", ", costs.Where(cost => cost.Missing > 0)
                    .Select(cost => cost.Missing + " " + cost.DisplayName)) + ".";

            return new BuildablePresentation
            {
                Definition = definition,
                IsUnlocked = unlocked,
                IsAffordable = affordable,
                IsAvailable = available,
                Costs = costs,
                DisabledReason = disabledReason,
                Capability = Capability(definition)
            };
        }

        private static List<CostPresentation> BuildCosts(BuildableDefinition definition, ResourceInventory inventory)
        {
            var result = new List<CostPresentation>();
            if (definition == null || definition.costs == null) return result;

            foreach (var group in definition.costs
                .Where(cost => cost.material != null && cost.amount > 0)
                .GroupBy(cost => cost.material.Id))
            {
                MaterialDefinition material = group.First().material;
                int required = group.Sum(cost => cost.amount);
                int owned = inventory != null ? inventory.GetCount(material.Id) : 0;
                result.Add(new CostPresentation
                {
                    MaterialId = material.Id,
                    DisplayName = material.DisplayName,
                    Icon = material.Icon,
                    Owned = owned,
                    Required = required,
                    Missing = Mathf.Max(0, required - owned)
                });
            }
            return result;
        }

        private static string Capability(BuildableDefinition definition)
        {
            if (definition == null) return string.Empty;
            if (definition.storage) return "Safely stores stacks of materials.";
            return definition.station switch
            {
                CraftingStationType.Forge => "Processes metals and enables heavy equipment crafting.",
                CraftingStationType.Cauldron => "Prepares alchemy, oils, and organic materials.",
                CraftingStationType.ArcaneTable => "Processes crystals and enables arcane crafting.",
                _ => "General crafting and basic material preparation."
            };
        }
    }
}
