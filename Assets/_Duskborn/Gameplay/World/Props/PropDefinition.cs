using Duskborn.Core;
using UnityEngine;

namespace Duskborn.Gameplay.World
{
    [System.Serializable]
    public class ResourceClusterSettings
    {
        [Tooltip("When enabled, this resource spawns in clustered pockets.")]
        public bool enableClustering = true;

        [Tooltip("Number of resource clusters / pockets per chunk.")]
        [Range(0, 8)] public int clustersPerChunk = 2;

        [Tooltip("Number of nodes generated within one cluster.")]
        public Vector2Int nodesPerCluster = new Vector2Int(3, 6);

        [Tooltip("Cluster spread radius around its center.")]
        [Range(2f, 15f)] public float clusterRadius = 6.0f;

        [Tooltip("Minimum distance between nodes in the same cluster (prevents physical overlap).")]
        [Range(1.2f, 5f)] public float intraClusterSpacing = 2.4f;

        [Tooltip("Minimum isolation distance from other resource types.")]
        [Range(2f, 10f)] public float interClusterSpacing = 5.0f;
    }

    [CreateAssetMenu(fileName = "Prop_Name", menuName = "Duskborn/World/Prop Definition")]
    public class PropDefinition : ScriptableObject
    {
        [Tooltip("Prop identifier name (e.g. Tree, Rock, Iron Ore, Fiber).")]
        public string propName = "Tree";

        [Tooltip("Prefab base a ser instanciado.")]
        public GameObject prefab;

        [Tooltip("Model / prefab variations for this prop (when populated, randomly selects the base or a variation).")]
        public GameObject[] prefabVariations;

        /// <summary>
        /// Deterministically select a prefab from the base and its variations.
        /// </summary>
        public GameObject GetRandomPrefab(SeededRNG rng = null)
        {
            if (prefabVariations != null && prefabVariations.Length > 0)
            {
                int total = 1 + prefabVariations.Length;
                int choice = rng != null ? rng.Range(0, total) : UnityEngine.Random.Range(0, total);
                if (choice > 0)
                {
                    var variant = prefabVariations[choice - 1];
                    if (variant != null) return variant;
                }
            }
            return prefab;
        }

        [Header("Cluster Configuration")]
        public ResourceClusterSettings clusterSettings = new ResourceClusterSettings();

        [Header("Spatial Occupancy & Foliage")]
        [Tooltip("Ground physical blocking radius (prevents foliage and other objects from colliding / intersecting).")]
        public float solidRadius = 1.0f;

        [Tooltip("Canopy / influence radius (shrubs and shade tufts cluster in this ring). 0 if there is no canopy.")]
        public float canopyRadius = 0.0f;

        [Header("Radial Zoning")]
        [Tooltip("Minimum radial distance (from center 0,0) where this resource starts appearing.")]
        public float minRadialDistance = 0f;

        [Tooltip("Maximum radial distance (from center 0,0) for this resource. 0 = no limit.")]
        public float maxRadialDistance = 0f;

        [Header("Density per Chunk (Fallback without Clustering)")]
        [Tooltip("Minimum number of this prop per chunk.")]
        [Range(0, 50)] public int minPerChunk = 2;

        [Tooltip("Maximum number of this prop per chunk.")]
        [Range(0, 50)] public int maxPerChunk = 6;

        [Header("Organic Clustering & Clearings")]
        [Tooltip("When enabled, use density noise to form organic groves / veins, preserving open combat clearings.")]
        public bool useClustering = true;

        [Tooltip("Clustering noise spatial frequency. Smaller values (e.g. 0.035) create larger clusters.")]
        [Range(0.01f, 0.2f)] public float clusterFrequency = 0.04f;

        [Tooltip("Minimum density threshold (0 to 1). Below this value, leave an open combat clearing.")]
        [Range(0.0f, 0.8f)] public float clusterThreshold = 0.38f;

        [Header("Terrain Conditions")]
        [Tooltip("Minimum terrain altitude (Y) permitting spawn.")]
        public float minHeight = 2.5f;

        [Tooltip("Maximum terrain altitude (Y) permitting spawn.")]
        public float maxHeight = 20.0f;

        [Tooltip("Maximum terrain slope (degrees) permitting spawn.")]
        [Range(0f, 60f)] public float maxSlopeAngle = 25f;

        [Header("Scale and Rotation Variation")]
        [Tooltip("Uniform random scale range (minimum, maximum).")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.25f);

        [Tooltip("Random height multiplier (Y axis). (1, 1) preserves perfectly uniform proportions.")]
        public Vector2 heightScaleMultiplier = new Vector2(1f, 1f);

        [Tooltip("Apply random rotation on the vertical Y axis (0 to 360 degrees).")]
        public bool randomYRotation = true;

        [Tooltip("Align the prop's vertical orientation with the terrain face normal.")]
        public bool alignToNormal = false;

        [Header("Spacing")]
        [Tooltip("Minimum distance radius from other props to avoid overlap.")]
        public float exclusionRadius = 2.0f;
    }
}
