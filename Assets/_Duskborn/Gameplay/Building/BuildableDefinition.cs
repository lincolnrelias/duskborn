using System;
using System.Collections.Generic;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Loot;
using UnityEngine;

namespace Duskborn.Gameplay.Building
{
    [CreateAssetMenu(menuName = "Duskborn/Building/Definition")]
    public sealed class BuildableDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public string category = "Estações";
        public GameObject prefab;
        public Texture2D icon;
        public CraftingStationType station;
        public List<CraftingIngredient> costs = new();
        public CraftingRecipe unlockRecipe;
        [Min(0)] public float clearance = .1f;
        [Range(0, 45)] public float maxSlope = 15;
        [Min(.01f)] public float groundTolerance = .25f;
        [Min(1)] public float reach = 6;
        [Min(1)] public float rotationStep = 15;
        public LayerMask groundLayers = 1;
        public LayerMask blockingLayers = ~((1 << 2) | (1 << 4) | (1 << 5));
        public bool allowMultiple = true;
        public bool movable = true;
        public bool dismantlable = true;
        public bool storage;
        [Min(1)] public int capacity = 200;
        [Min(1)] public int queueCapacity = 5;
        [Min(.1f)] public float processingSpeed = 1;
        public PlacementRule[] rules;

        public bool IsUnlocked(RecipeDiscoveryTracker discovery) => unlockRecipe == null ||
            (discovery != null && discovery.IsDiscovered(unlockRecipe));
    }

    /// <summary>Calculates a prefab's occupied local-space bounds from its render meshes.</summary>
    public static class BuildableBounds
    {
        public static bool TryGet(BuildableDefinition definition, out Bounds bounds)
        {
            bounds = default;
            if (definition == null || definition.prefab == null) return false;

            var root = definition.prefab.transform;
            bool hasBounds = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Encapsulate(root, filter.transform, filter.sharedMesh.bounds, ref bounds, ref hasBounds);
            }
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Encapsulate(root, renderer.transform, renderer.localBounds, ref bounds, ref hasBounds);
            if (!hasBounds) return false;

            // CreateVisual carries the prefab root's scale onto its placement root, so
            // include it here as well for physics queries and the generated collider.
            var scale = root.localScale;
            bounds.center = Vector3.Scale(bounds.center, scale);
            bounds.size = Vector3.Scale(bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return bounds.size.sqrMagnitude > 0f;
        }

        /// <summary>Moves a prefab's pivot so its lowest mesh point rests on the placement surface.</summary>
        public static Vector3 GroundOffset(Bounds bounds) => Vector3.up * -bounds.min.y;

        private static void Encapsulate(Transform root, Transform source, Bounds sourceBounds, ref Bounds result, ref bool hasBounds)
        {
            var min = sourceBounds.min;
            var max = sourceBounds.max;
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                var point = root.InverseTransformPoint(source.TransformPoint(new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z)));
                if (hasBounds) result.Encapsulate(point);
                else { result = new Bounds(point, Vector3.zero); hasBounds = true; }
            }
        }
    }

    // Extend by adding rule assets, without adding station-specific branches to placement.
    public abstract class PlacementRule : ScriptableObject
    {
        public abstract string Validate(BuildableDefinition definition, Vector3 position, Quaternion rotation);
    }

    public static class MaterialCosts
    {
        public static bool Aggregate(IReadOnlyList<CraftingIngredient> ingredients, out Dictionary<string, int> totals)
        {
            totals = new Dictionary<string, int>();
            if (ingredients == null || ingredients.Count == 0) return false;
            foreach (var ingredient in ingredients)
            {
                if (ingredient.material == null || ingredient.amount <= 0 || string.IsNullOrEmpty(ingredient.material.Id)) return false;
                totals.TryGetValue(ingredient.material.Id, out int previous);
                if ((long)previous + ingredient.amount > int.MaxValue) return false;
                totals[ingredient.material.Id] = previous + ingredient.amount;
            }
            return true;
        }
        public static bool CanPay(ResourceInventory inventory, IReadOnlyList<CraftingIngredient> ingredients) =>
            inventory != null && Aggregate(ingredients, out var totals) && inventory.CanSpendBatch(totals);
        public static bool Spend(ResourceInventory inventory, IReadOnlyList<CraftingIngredient> ingredients) =>
            inventory != null && Aggregate(ingredients, out var totals) && inventory.TrySpendBatch(totals);

        public static bool Aggregate(
            IReadOnlyList<CraftingIngredient> ingredients,
            IReadOnlyList<CraftingIngredient> fuel,
            out Dictionary<string, int> totals)
        {
            totals = new Dictionary<string, int>();
            bool any = false;
            if (!Append(ingredients, totals, ref any) || !Append(fuel, totals, ref any)) return false;
            return any;
        }

        public static bool CanPay(
            ResourceInventory inventory,
            IReadOnlyList<CraftingIngredient> ingredients,
            IReadOnlyList<CraftingIngredient> fuel) =>
            inventory != null && Aggregate(ingredients, fuel, out var totals) && inventory.CanSpendBatch(totals);

        public static bool Spend(
            ResourceInventory inventory,
            IReadOnlyList<CraftingIngredient> ingredients,
            IReadOnlyList<CraftingIngredient> fuel) =>
            inventory != null && Aggregate(ingredients, fuel, out var totals) && inventory.TrySpendBatch(totals);

        private static bool Append(
            IReadOnlyList<CraftingIngredient> ingredients,
            Dictionary<string, int> totals,
            ref bool any)
        {
            if (ingredients == null) return true;
            foreach (var ingredient in ingredients)
            {
                if (ingredient.material == null || ingredient.amount <= 0 || string.IsNullOrEmpty(ingredient.material.Id)) return false;
                totals.TryGetValue(ingredient.material.Id, out int previous);
                if ((long)previous + ingredient.amount > int.MaxValue) return false;
                totals[ingredient.material.Id] = previous + ingredient.amount;
                any = true;
            }
            return true;
        }
    }
}
