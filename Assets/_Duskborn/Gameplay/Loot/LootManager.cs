using System.Collections;
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
        [Tooltip("Launch speed magnitude — controls how far/high items fly.")]
        [SerializeField] private float throwSpeed = 4.0f;
        [Tooltip("Per-item random variance applied to throwSpeed.")]
        [SerializeField] private float throwSpeedVariance = 1.0f;
        [Tooltip("Angle from straight up (degrees) — the cone's half-angle. 0 = straight up, 90 = flat outward.")]
        [Range(0f, 90f)]
        [SerializeField] private float coneAngle = 35f;
        [Tooltip("Per-item random variance applied to coneAngle.")]
        [SerializeField] private float coneAngleVariance = 8f;
        [Tooltip("Fraction (0-1) of each item's angular slot used for random azimuth jitter, so items stay evenly spaced regardless of count.")]
        [Range(0f, 1f)]
        [SerializeField] private float azimuthJitter = 0.4f;

        [Header("Timing")]
        [Tooltip("Delay in seconds between each item spawn — forms a sequential 'barrage' rather than spawning everything at once.")]
        [SerializeField] private float dropInterval = 0.2f;

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

            StartCoroutine(DropSequence(spawnList, spawnGold, goldAmount, origin, total, rng));

            DuskLog.Log(LogChannel.Loot,
                $"LootManager: dropping {spawnList.Count} item(s) at {origin} (night {currentNight}).");
        }

        // Spawns and throws items one at a time with a delay between each, forming a sequential barrage.
        private IEnumerator DropSequence(List<(GameObject prefab, string id)> spawnList, bool spawnGold, int goldAmount, Vector3 origin, int total, SeededRNG rng)
        {
            for (int i = 0; i < spawnList.Count; i++)
            {
                var (prefab, id) = spawnList[i];
                SpawnItem(prefab, id, origin, i, total, rng);
                yield return new WaitForSeconds(dropInterval);
            }

            if (spawnGold)
                SpawnGoldPickup(origin, goldAmount, spawnList.Count, total, rng);
        }

        private void SpawnItem(GameObject prefab, string id, Vector3 origin, int index, int total, SeededRNG rng)
        {
            var go = Instantiate(prefab, origin + Vector3.up * 0.1f, Quaternion.identity);
            InstanceFinder.ServerManager.Spawn(go);
            var pickup = go.GetComponent<WorldItemPickup>();
            if (pickup != null)
            {
                pickup.ServerInitialize(id, 1);
                pickup.ServerThrow(ComputeThrowDirection(index, total, rng));
            }
        }

        // Spaces items evenly around a full circle, each launched along a cone surface
        // (fixed angle from straight up) so the burst forms a cone widening toward the top.
        private Vector3 ComputeThrowDirection(int index, int total, SeededRNG rng)
        {
            float slotDeg  = total > 0 ? 360f / total : 0f;
            float jitter   = slotDeg * 0.5f * azimuthJitter;
            float azimuth  = (slotDeg * index + RandRange(rng, -jitter, jitter)) * Mathf.Deg2Rad;

            float cone  = Mathf.Clamp(coneAngle + RandRange(rng, -coneAngleVariance, coneAngleVariance), 0f, 90f) * Mathf.Deg2Rad;
            float speed = Mathf.Max(0f, throwSpeed + RandRange(rng, -throwSpeedVariance, throwSpeedVariance));

            float radial = speed * Mathf.Sin(cone);
            float up     = speed * Mathf.Cos(cone);

            return new Vector3(
                Mathf.Cos(azimuth) * radial,
                up,
                Mathf.Sin(azimuth) * radial
            );
        }

        private static float RandRange(SeededRNG rng, float min, float max) =>
            rng != null ? rng.Range(min, max) : UnityEngine.Random.Range(min, max);

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
