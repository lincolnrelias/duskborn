using UnityEngine;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.World
{
    [CreateAssetMenu(fileName = "WorldPropsConfig", menuName = "Duskborn/World/World Props Config")]
    public class WorldPropsConfig : ScriptableObject
    {
        [Header("Central Clearing (Safe Spawn Zone)")]
        [Tooltip("Radius around (0,0) where dense trees or rocks are not generated.")]
        public float centerClearingRadius = 10f;

        [Tooltip("Starting workbench prefab placed in the central clearing.")]
        public GameObject workbenchPrefab;

        [Tooltip("Smelting forge prefab placed in the central clearing.")]
        public GameObject forgePrefab;

        [Tooltip("Alchemical cauldron prefab placed in the central clearing.")]
        public GameObject cauldronPrefab;

        [Tooltip("Arcane table prefab placed in the central clearing.")]
        public GameObject arcaneTablePrefab;

        [Tooltip("Optional prefab for instantiating player spawn points in the clearing if absent from the scene.")]
        public GameObject playerSpawnPointPrefab;

        [Tooltip("Number of player spawn points generated procedurally in the central clearing.")]
        [Range(1, 10)] public int playerSpawnPointsCount = 5;

        [Tooltip("Spawn circle radius in meters around the clearing center.")]
        [Range(2f, 15f)] public float playerSpawnRadius = 6.5f;

        [Header("Natural Resources (Resource Nodes)")]
        [Tooltip("Ecological tree definition (Wood).")]
        public PropDefinition treeProp;

        [Tooltip("Ecological common rock definition (Stone).")]
        public PropDefinition stoneProp;

        [Tooltip("Ecological iron ore definition.")]
        public PropDefinition ironProp;

        [Tooltip("Ecological fiber shrub definition.")]
        public PropDefinition fiberProp;

        [Tooltip("Additional decorative or natural props (optional).")]
        public PropDefinition[] extraProps;

        [Header("Chest Configuration")]
        [Tooltip("Interactive chest prefab containing Chest.cs.")]
        public GameObject chestPrefab;

        [Tooltip("Total number of chests distributed across the map.")]
        [Range(1, 50)] public int totalChests = 8;

        [Tooltip("Loot table for basic chests (near the center / radius <= 50%).")]
        public LootTable basicLootTable;

        [Tooltip("Loot table for rare chests (far from the center / radius > 50%).")]
        public LootTable rareLootTable;

        [Tooltip("Minimum gold cost for chests closest to the center.")]
        public int minChestCost = 35;

        [Tooltip("Maximum gold cost for the farthest or boundary chests.")]
        public int maxChestCost = 150;
    }
}
