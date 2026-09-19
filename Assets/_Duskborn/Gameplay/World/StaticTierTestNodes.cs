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
    /// Gerencia o spawn estático de nós de recursos para teste do sistema de tiers:
    /// Comum -> Incomum -> Raro -> Épico -> Lendário (+ Nó Misto).
    /// Posicionados de forma limpa e visível próximos à zona de spawn inicial.
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
            // === FILA 1 (ARCO INTERNO / FRONT RANK) ===
            new() {
                id = "TestNode_Common_1",
                displayName = "Nó de Teste [Comum #1]",
                rarity = ItemRarity.Common,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_common.asset",
                localOffset = new Vector3(7.5f, 0f, 5.0f)
            },
            new() {
                id = "TestNode_Uncommon_1",
                displayName = "Nó de Teste [Incomum #1]",
                rarity = ItemRarity.Uncommon,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_uncommon.asset",
                localOffset = new Vector3(8.8f, 0f, 2.8f)
            },
            new() {
                id = "TestNode_Rare_1",
                displayName = "Nó de Teste [Raro #1]",
                rarity = ItemRarity.Rare,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_rare.asset",
                localOffset = new Vector3(9.5f, 0f, 0.0f)
            },
            new() {
                id = "TestNode_Epic_1",
                displayName = "Nó de Teste [Épico #1]",
                rarity = ItemRarity.Epic,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_epic.asset",
                localOffset = new Vector3(8.8f, 0f, -2.8f)
            },
            new() {
                id = "TestNode_Legendary_1",
                displayName = "Nó de Teste [Lendário #1]",
                rarity = ItemRarity.Legendary,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_legendary.asset",
                localOffset = new Vector3(7.5f, 0f, -5.0f)
            },
            new() {
                id = "TestNode_Mixed_1",
                displayName = "Nó de Teste [Misto #1]",
                rarity = ItemRarity.Legendary,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_mixed.asset",
                localOffset = new Vector3(13.5f, 0f, 2.0f)
            },

            // === FILA 2 (ARCO EXTERNO / BACK RANK) ===
            new() {
                id = "TestNode_Common_2",
                displayName = "Nó de Teste [Comum #2]",
                rarity = ItemRarity.Common,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_common.asset",
                localOffset = new Vector3(10.5f, 0f, 6.2f)
            },
            new() {
                id = "TestNode_Uncommon_2",
                displayName = "Nó de Teste [Incomum #2]",
                rarity = ItemRarity.Uncommon,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_uncommon.asset",
                localOffset = new Vector3(11.8f, 0f, 3.5f)
            },
            new() {
                id = "TestNode_Rare_2",
                displayName = "Nó de Teste [Raro #2]",
                rarity = ItemRarity.Rare,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_rare.asset",
                localOffset = new Vector3(12.5f, 0f, 0.0f)
            },
            new() {
                id = "TestNode_Epic_2",
                displayName = "Nó de Teste [Épico #2]",
                rarity = ItemRarity.Epic,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_epic.asset",
                localOffset = new Vector3(11.8f, 0f, -3.5f)
            },
            new() {
                id = "TestNode_Legendary_2",
                displayName = "Nó de Teste [Lendário #2]",
                rarity = ItemRarity.Legendary,
                tablePath = "Assets/_Duskborn/ScriptableObjects/Loot/table_test_legendary.asset",
                localOffset = new Vector3(10.5f, 0f, -6.2f)
            },
            new() {
                id = "TestNode_Mixed_2",
                displayName = "Nó de Teste [Misto #2]",
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
                DuskLog.Warn(LogChannel.World, "StaticTierTestNodes: Prefab de Node_Stone não encontrado.");
                return spawned;
            }

            // Remove nós de teste antigos se existirem no contêiner
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

                // Configura LootDropper com a tabela de drop correspondente ao tier
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

                // Ajusta HP baixo (25 HP) para facilitar testes rápidos com 1-2 golpes
                if (go.TryGetComponent<ResourceNode>(out var node))
                {
                    // Usa reflexão segura para sobrescrever maxHP no teste se privado
                    var hpField = typeof(ResourceNode).GetField("maxHP", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (hpField != null) hpField.SetValue(node, 25f);
                }

                // Adiciona indicador visual de tier no nó (luz suave e cor temática)
                var indicator = go.GetComponent<TierNodeVisualIndicator>();
                if (indicator == null) indicator = go.AddComponent<TierNodeVisualIndicator>();
                indicator.Setup(cfg.rarity, cfg.displayName);

                // Spawn de rede no FishNet durante runtime no servidor
                if (Application.isPlaying && InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started)
                {
                    if (go.TryGetComponent<NetworkObject>(out var nob) && !nob.IsSpawned)
                    {
                        InstanceFinder.ServerManager.Spawn(go);
                    }
                }

                spawned.Add(go);
            }

            DuskLog.Log(LogChannel.World, $"StaticTierTestNodes: {spawned.Count} nós de teste de tiers instanciados com sucesso.");
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
            Debug.Log($"<color=#55FF55><b>[StaticTierTestNodes] {TestConfigs.Length} Nós de recursos de teste (2 de cada tier) gerados com sucesso próximos ao Spawn!</b></color>");
        }
#endif
    }

    /// <summary>
    /// Componente que adiciona um identificador visual no topo do nó de teste (luz suave na cor do tier e nome).
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
