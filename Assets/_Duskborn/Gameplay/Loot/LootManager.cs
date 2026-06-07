using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Loot
{
    public class LootManager : MonoBehaviour
    {
        public static LootManager Instance { get; private set; }

        [Header("Throw Physics")]
        [SerializeField] private float throwRadialSpeed    = 3.5f;
        [SerializeField] private float throwUpSpeed        = 5.0f;
        [SerializeField] private float throwRadialVariance = 1.0f;
        [SerializeField] private float throwUpVariance     = 1.5f;

        [Header("Gold")]
        [SerializeField] private GameObject worldGoldPickupPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ServerDropLoot(DropLootTable table, Vector3 origin)
        {
            if (!InstanceFinder.IsServerStarted || table == null) return;

            SeededRNG rng         = GameSession.Instance?.RNG;
            int       currentNight = DayNightCycle.Instance?.CurrentNight ?? 0;

            var hits       = table.Roll(rng, currentNight);
            int goldAmount = table.RollGold(rng);
            bool spawnGold = goldAmount > 0;

            // Expand entries into individual physical items so each gets its own prefab.
            var spawnList = new List<(GameObject prefab, string id)>();
            foreach (var entry in hits)
            {
                if (string.IsNullOrEmpty(entry.itemDefinition.Id))
                {
                    DuskLog.Warn(LogChannel.Loot,
                        $"LootManager: entry '{entry.itemDefinition.name}' has no id set on its asset — skipping.");
                    continue;
                }
                var prefab = entry.itemDefinition.dropPrefab;
                if (prefab == null)
                {
                    DuskLog.Warn(LogChannel.Loot,
                        $"LootManager: entry '{entry.itemDefinition.name}' has no dropPrefab — skipping.");
                    continue;
                }
                int count = rng != null
                    ? rng.Range(entry.minAmount, entry.maxAmount + 1)
                    : UnityEngine.Random.Range(entry.minAmount, entry.maxAmount + 1);
                for (int j = 0; j < count; j++)
                    spawnList.Add((prefab, entry.itemDefinition.Id));
            }

            int total = spawnList.Count + (spawnGold ? 1 : 0);

            for (int i = 0; i < spawnList.Count; i++)
            {
                var (prefab, id) = spawnList[i];
                var go = Instantiate(prefab, origin + Vector3.up * 0.1f, Quaternion.identity);
                InstanceFinder.ServerManager.Spawn(go);
                var pickup = go.GetComponent<WorldItemPickup>();
                if (pickup != null)
                {
                    pickup.ServerInitialize(id, 1);
                    pickup.ServerThrow(ComputeThrowDirection(i, total, rng));
                }
            }

            if (spawnGold)
                SpawnGoldPickup(origin, goldAmount, spawnList.Count, total, rng);

            DuskLog.Log(LogChannel.Loot,
                $"LootManager: dropped {spawnList.Count} item(s) at {origin} (night {currentNight}).");
        }

        // Evenly spaces items radially from the origin with per-item random angle variance.
        private Vector3 ComputeThrowDirection(int index, int total, SeededRNG rng)
        {
            float baseAngleDeg = total > 1 ? (360f / total) * index : 0f;
            float variance     = rng != null
                ? rng.Range(-30f, 30f)
                : UnityEngine.Random.Range(-30f, 30f);

            float angleRad = (baseAngleDeg + variance) * Mathf.Deg2Rad;

            float radial = throwRadialSpeed + (rng != null
                ? rng.Range(-throwRadialVariance, throwRadialVariance)
                : UnityEngine.Random.Range(-throwRadialVariance, throwRadialVariance));

            float up = throwUpSpeed + (rng != null
                ? rng.Range(-throwUpVariance, throwUpVariance)
                : UnityEngine.Random.Range(-throwUpVariance, throwUpVariance));

            return new Vector3(
                Mathf.Cos(angleRad) * radial,
                up,
                Mathf.Sin(angleRad) * radial
            );
        }

        private void SpawnGoldPickup(Vector3 origin, int amount, int throwIndex, int throwTotal, SeededRNG rng)
        {
            if (worldGoldPickupPrefab == null)
            {
                DuskLog.Warn(LogChannel.Loot, "LootManager: worldGoldPickupPrefab not assigned — adding gold directly.");
                GoldManager.Instance?.AddGold(amount);
                return;
            }

            var go = Instantiate(worldGoldPickupPrefab, origin + Vector3.up * 0.1f, Quaternion.identity);
            InstanceFinder.ServerManager.Spawn(go);

            var goldPickup = go.GetComponent<WorldGoldPickup>();
            if (goldPickup == null)
            {
                DuskLog.Warn(LogChannel.Loot,
                    $"LootManager: '{worldGoldPickupPrefab.name}' has no WorldGoldPickup component — swap WorldItemPickup for WorldGoldPickup on that prefab.");
                InstanceFinder.ServerManager.Despawn(go.GetComponent<NetworkObject>(), DespawnType.Destroy);
                GoldManager.Instance?.AddGold(amount);
                return;
            }

            goldPickup.ServerInitialize(amount);
            goldPickup.ServerThrow(ComputeThrowDirection(throwIndex, throwTotal, rng));
        }

        // Adjusts a single entry's baseChance at runtime (mutates in-memory ScriptableObject only — not saved to disk).
        public void ModifyDropChance(DropLootTable table, int entryIndex, float delta)
        {
            if (table?.entries == null) return;
            if ((uint)entryIndex >= (uint)table.entries.Length) return;
            table.entries[entryIndex].baseChance = Mathf.Clamp01(table.entries[entryIndex].baseChance + delta);
        }
    }
}
