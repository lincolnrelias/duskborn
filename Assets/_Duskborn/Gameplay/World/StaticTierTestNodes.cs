using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;
using Duskborn.Gameplay.Loot;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Manage static resource node spawning to test the tier system:
    /// Common -> Uncommon -> Rare -> Epic -> Legendary (+ Mixed Node).
    /// Placed cleanly and visibly near the initial spawn zone.
    /// </summary>
    public static class StaticTierTestNodes
    {
        private const string NodeStonePrefabPath = "Assets/_Duskborn/Prefabs/World/Node_Stone.prefab";

        public struct NodeTestConfig
        {
            public string     id;
            public string     displayName;
            public ItemRarity rarity;
            public string     tablePath;
            public Vector3    localOffset;
        }

        public static readonly NodeTestConfig[] TestConfigs = new NodeTestConfig[]
        {
            // === ROW 1 (INNER ARC / FRONT RANK) ===
            new() {
                id = "TestNode_Common_1",
                displayName = "Test Node [Common #1]",
                rarity = ItemRarity.Common,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_common.asset",
                localOffset = new Vector3(7.5f, 0f, 5.0f)
            },
            new() {
                id = "TestNode_Uncommon_1",
                displayName = "Test Node [Uncommon #1]",
                rarity = ItemRarity.Uncommon,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_uncommon.asset",
                localOffset = new Vector3(8.8f, 0f, 2.8f)
            },
            new() {
                id = "TestNode_Rare_1",
                displayName = "Test Node [Rare #1]",
                rarity = ItemRarity.Rare,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_rare.asset",
                localOffset = new Vector3(9.5f, 0f, 0.0f)
            },
            new() {
                id = "TestNode_Epic_1",
                displayName = "Test Node [Epic #1]",
                rarity = ItemRarity.Epic,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_epic.asset",
                localOffset = new Vector3(8.8f, 0f, -2.8f)
            },
            new() {
                id = "TestNode_Legendary_1",
                displayName = "Test Node [Legendary #1]",
                rarity = ItemRarity.Legendary,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_legendary.asset",
                localOffset = new Vector3(7.5f, 0f, -5.0f)
            },
            new() {
                id = "TestNode_Mixed_1",
                displayName = "Test Node [Mixed #1]",
                rarity = ItemRarity.Legendary,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_mixed.asset",
                localOffset = new Vector3(13.5f, 0f, 2.0f)
            },

            // === ROW 2 (OUTER ARC / BACK RANK) ===
            new() {
                id = "TestNode_Common_2",
                displayName = "Test Node [Common #2]",
                rarity = ItemRarity.Common,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_common.asset",
                localOffset = new Vector3(10.5f, 0f, 6.2f)
            },
            new() {
                id = "TestNode_Uncommon_2",
                displayName = "Test Node [Uncommon #2]",
                rarity = ItemRarity.Uncommon,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_uncommon.asset",
                localOffset = new Vector3(11.8f, 0f, 3.5f)
            },
            new() {
                id = "TestNode_Rare_2",
                displayName = "Test Node [Rare #2]",
                rarity = ItemRarity.Rare,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_rare.asset",
                localOffset = new Vector3(12.5f, 0f, 0.0f)
            },
            new() {
                id = "TestNode_Epic_2",
                displayName = "Test Node [Epic #2]",
                rarity = ItemRarity.Epic,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_epic.asset",
                localOffset = new Vector3(11.8f, 0f, -3.5f)
            },
            new() {
                id = "TestNode_Legendary_2",
                displayName = "Test Node [Legendary #2]",
                rarity = ItemRarity.Legendary,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_legendary.asset",
                localOffset = new Vector3(10.5f, 0f, -6.2f)
            },
            new() {
                id = "TestNode_Mixed_2",
                displayName = "Test Node [Mixed #2]",
                rarity = ItemRarity.Legendary,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_mixed.asset",
                localOffset = new Vector3(13.5f, 0f, -2.0f)
            }
        };

        public static List<GameObject> SpawnNodes(Transform container, Vector3 center, Func<Vector3, Vector3> snapToGround = null)
        {
            var spawned = new List<GameObject>();

            GameObject nodePrefab = null;
#if UNITY_EDITOR
            nodePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NodeStonePrefabPath);
#endif
            if (nodePrefab == null)
            {
                nodePrefab = Resources.Load<GameObject>("World/Node_Stone");
            }

            if (nodePrefab == null)
            {
                DuskLog.Warn(LogChannel.World, "StaticTierTestNodes: Node_Stone prefab not found.");
                return spawned;
            }

            // Remove old test nodes if present in the container.
            if (container != null)
            {
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    Transform child = container.GetChild(i);
                    if (child.name.StartsWith("TestNode_"))
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(child.gameObject);
#if UNITY_EDITOR
                        else Undo.DestroyObjectImmediate(child.gameObject);
#endif
                    }
                }
            }

            foreach (var cfg in TestConfigs)
            {
                Vector3 worldPos = center + cfg.localOffset;
                if (snapToGround != null)
                {
                    worldPos = snapToGround(worldPos);
                }
                else
                {
                    if (Physics.Raycast(new Vector3(worldPos.x, 150f, worldPos.z), Vector3.down, out RaycastHit hit, 300f))
                    {
                        worldPos.y = hit.point.y;
                    }
                }

                GameObject go;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(nodePrefab, container);
                    if (go == null) go = UnityEngine.Object.Instantiate(nodePrefab, worldPos, Quaternion.identity, container);
                    go.transform.position = worldPos;
                    Undo.RegisterCreatedObjectUndo(go, $"Spawn {cfg.displayName}");
                }
                else
#endif
                {
                    go = UnityEngine.Object.Instantiate(nodePrefab, worldPos, Quaternion.identity, container);
                }

                go.name = cfg.id;

                // Configure LootDropper with the tier's corresponding drop table.
                DropLootTable lootTable = null;
#if UNITY_EDITOR
                lootTable = AssetDatabase.LoadAssetAtPath<DropLootTable>(cfg.tablePath);
#endif
                if (lootTable == null)
                {
                    lootTable = Resources.Load<DropLootTable>(cfg.tablePath.Replace("Assets/_Duskborn/Resources/", "").Replace(".asset", ""));
                }

                if (go.TryGetComponent<LootDropper>(out var dropper))
                {
                    dropper.SetLootTable(lootTable);
                }

                // Set low HP (25 HP) for quick tests with 1-2 hits.
                if (go.TryGetComponent<ResourceNode>(out var node))
                {
                    // Use safe reflection to override maxHP in the test if private.
                    var hpField = typeof(ResourceNode).GetField("maxHP", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (hpField != null) hpField.SetValue(node, 25f);
                }

                // Add a visual tier indicator to the node (soft light and themed color).
                var indicator = go.GetComponent<TierNodeVisualIndicator>();
                if (indicator == null) indicator = go.AddComponent<TierNodeVisualIndicator>();
                indicator.Setup(cfg.rarity, cfg.displayName);

                // FishNet network spawning during server runtime.
                if (Application.isPlaying && InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started)
                {
                    if (go.TryGetComponent<NetworkObject>(out var nob) && !nob.IsSpawned)
                    {
                        InstanceFinder.ServerManager.Spawn(go);
                    }
                }

                spawned.Add(go);
            }

            DuskLog.Log(LogChannel.World, $"StaticTierTestNodes: {spawned.Count} tier test nodes instantiated successfully.");
            return spawned;
        }

#if UNITY_EDITOR
        [MenuItem("Duskborn/Loot/Spawn Static Tier Test Nodes in Scene", false, 110)]
        public static void MenuSpawnTestNodes()
        {
            GameObject container = GameObject.Find("WorldPropsContainer") ?? GameObject.Find("PropsContainer");
            if (container == null)
            {
                container = new GameObject("WorldPropsContainer");
                Undo.RegisterCreatedObjectUndo(container, "Create WorldPropsContainer");
            }

            Vector3 center = Vector3.zero;
            var spawners = UnityEngine.Object.FindObjectsByType<Duskborn.Gameplay.World.PlayerSpawnPoint>(FindObjectsSortMode.None);
            if (spawners != null && spawners.Length > 0)
            {
                center = spawners[0].transform.position;
                center.y = 0f;
            }

            SpawnNodes(container.transform, center);
            Debug.Log($"<color=#55FF55><b>[StaticTierTestNodes] {TestConfigs.Length} Test resource nodes (2 of each tier) generated successfully near Spawn!</b></color>");
        }
#endif
    }

    /// <summary>
    /// Component adding a visual identifier above a test node (soft tier-colored light and name).
    /// </summary>
    public class TierNodeVisualIndicator : MonoBehaviour
    {
        [SerializeField] private ItemRarity tierRarity;
        [SerializeField] private string     tierLabel;

        private Light _indicatorLight;

        public void Setup(ItemRarity rarity, string label)
        {
            tierRarity = rarity;
            tierLabel  = label;

            Color color = ItemTierHelper.GetColor(rarity);

            if (_indicatorLight == null)
            {
                var lightObj = new GameObject("TierIndicatorLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                _indicatorLight = lightObj.AddComponent<Light>();
            }

            _indicatorLight.type = LightType.Point;
            _indicatorLight.color = color;
            _indicatorLight.range = 1.6f;
            _indicatorLight.intensity = 0.35f;
            _indicatorLight.shadows = LightShadows.None;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = ItemTierHelper.GetColor(tierRarity);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.4f);
        }
    }
}
