using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.Player;
using InventorySystem.Data;

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

        [Tooltip("Random tumbling torque applied to items upon launch.")]
        [SerializeField] private float throwTorque = 4.0f;

        [Header("Spawn Separation")]
        [Tooltip("Initial radial distance from origin where items appear, spreading them around the body to prevent overlapping.")]
        [SerializeField] private float spawnSpreadRadius = 0.25f;
        [Tooltip("Vertical spawn offset above the origin.")]
        [SerializeField] private float spawnHeightOffset = 0.15f;


        [Header("Gold")]
        [SerializeField] private GameObject worldGoldPickupPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            ConfigureCollisionLayers();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ConfigureCollisionLayers()
        {
            int resourceLayer = LayerMask.NameToLayer("Resource");
            if (resourceLayer < 0) return;

            // Resource drops should never collide with each other
            Physics.IgnoreLayerCollision(resourceLayer, resourceLayer, true);

            // Resource drops should never collide with enemies or ragdoll bodies
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                Physics.IgnoreLayerCollision(resourceLayer, enemyLayer, true);

            // Resource drops should never collide with resource nodes
            int resourceNodeLayer = LayerMask.NameToLayer("ResourceNode");
            if (resourceNodeLayer >= 0)
                Physics.IgnoreLayerCollision(resourceLayer, resourceNodeLayer, true);

            // Resource drops should never collide with players
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                Physics.IgnoreLayerCollision(resourceLayer, playerLayer, true);
        }

        public void ServerDropLoot(
            DropLootTable table,
            Vector3 origin,
            PlayerStats harvester = null,
            TargetType targetType = TargetType.None)
        {
            if (!InstanceFinder.IsServerStarted || table == null) return;

            SeededRNG rng          = GameSession.Instance?.RNG;
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

                float bonus = GetResourceBonus(harvester, targetType, entry.itemDefinition);
                int count = CalculateDropCount(entry.minAmount, entry.maxAmount, bonus, rng);

                for (int j = 0; j < count; j++)
                    spawnList.Add((prefab, entry.itemDefinition.Id));
            }

            int total = spawnList.Count + (spawnGold ? 1 : 0);
            if (total == 0) return;

            // Instantly burst-throw all items from the body simultaneously
            SpawnAllLootInstant(spawnList, spawnGold, goldAmount, origin, total, rng);

            DuskLog.Log(LogChannel.Loot,
                $"LootManager: dropping {spawnList.Count} item(s) at {origin} (night {currentNight}).");
        }

        public static float GetResourceBonus(PlayerStats harvester, TargetType targetType, ItemDefinitionBase itemDef)
        {
            if (harvester == null) return 0f;

            // Never apply gathering bonus to enemy loot
            if ((targetType & TargetTypeMasks.EnemyTypes) != 0) return 0f;

            string id = itemDef != null && !string.IsNullOrEmpty(itemDef.Id) ? itemDef.Id.ToLowerInvariant() : string.Empty;

            bool isStoneOrOre = id.Contains("stone") || id.Contains("ore") || id.Contains("iron");
            bool isWoodOrFoliage = id.Contains("wood") || id.Contains("fiber") || id.Contains("bush") || id.Contains("herb");

            bool isMiningNode = (targetType & (TargetType.MiningNode | TargetType.Stone | TargetType.Ore)) != 0;
            bool isWoodcuttingNode = (targetType & (TargetType.Tree | TargetType.Bush)) != 0;

            if (isMiningNode || isStoneOrOre)
                return Mathf.Max(0f, harvester.EffectiveMiningResourceBonus);

            if (isWoodcuttingNode || isWoodOrFoliage)
                return Mathf.Max(0f, harvester.EffectiveWoodcuttingResourceBonus);

            return 0f;
        }

        public static int CalculateDropCount(int minAmount, int maxAmount, float bonus, SeededRNG rng)
        {
            int baseCount = rng != null
                ? rng.Range(minAmount, maxAmount + 1)
                : UnityEngine.Random.Range(minAmount, maxAmount + 1);

            if (bonus <= 0f)
                return baseCount;

            float extraFloat = baseCount * bonus;
            int guaranteedExtra = (int)extraFloat;
            float remainder = extraFloat - guaranteedExtra;

            bool extraOne = rng != null
                ? rng.Chance(remainder)
                : (UnityEngine.Random.value < remainder);

            return baseCount + guaranteedExtra + (extraOne ? 1 : 0);
        }

        private void SpawnAllLootInstant(
            List<(GameObject prefab, string id)> spawnList,
            bool spawnGold,
            int goldAmount,
            Vector3 origin,
            int total,
            SeededRNG rng)
        {
            float baseAngle = RandRange(rng, 0f, 360f);
            var spawnedColliders = new List<Collider>();

            for (int i = 0; i < spawnList.Count; i++)
            {
                var (prefab, id) = spawnList[i];
                var go = SpawnItem(prefab, id, origin, i, total, baseAngle, rng);
                if (go != null)
                    RegisterAndIgnoreCollisions(go, spawnedColliders);
            }

            if (spawnGold)
            {
                var go = SpawnGoldPickup(origin, goldAmount, spawnList.Count, total, baseAngle, rng);
                if (go != null)
                    RegisterAndIgnoreCollisions(go, spawnedColliders);
            }
        }

        private void RegisterAndIgnoreCollisions(GameObject go, List<Collider> collidersList)
        {
            var newCols = go.GetComponentsInChildren<Collider>();
            foreach (var newCol in newCols)
            {
                if (newCol == null) continue;
                foreach (var existingCol in collidersList)
                {
                    if (existingCol != null)
                        Physics.IgnoreCollision(newCol, existingCol, true);
                }
                collidersList.Add(newCol);
            }
        }

        private GameObject SpawnItem(GameObject prefab, string id, Vector3 origin, int index, int total, float baseAngle, SeededRNG rng)
        {
            Vector3 throwVelocity = ComputeThrowDirection(index, total, baseAngle, rng, out float azimuth);
            Vector3 spawnPos       = CalculateSpawnPosition(origin, azimuth, total);

            var go = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, azimuth * Mathf.Rad2Deg, 0f));
            InstanceFinder.ServerManager.Spawn(go);
            var pickup = go.GetComponent<WorldItemPickup>();
            if (pickup != null)
            {
                pickup.ServerInitialize(id, 1);
                pickup.ServerThrow(throwVelocity, throwTorque);
            }
            return go;
        }

        private GameObject SpawnGoldPickup(Vector3 origin, int amount, int throwIndex, int throwTotal, float baseAngle, SeededRNG rng)
        {
            if (worldGoldPickupPrefab == null)
            {
                DuskLog.Warn(LogChannel.Loot, "LootManager: worldGoldPickupPrefab not assigned — adding gold directly.");
                GoldManager.Instance?.AddGold(amount);
                return null;
            }

            Vector3 throwVelocity = ComputeThrowDirection(throwIndex, throwTotal, baseAngle, rng, out float azimuth);
            Vector3 spawnPos       = CalculateSpawnPosition(origin, azimuth, throwTotal);

            var go = Instantiate(worldGoldPickupPrefab, spawnPos, Quaternion.Euler(0f, azimuth * Mathf.Rad2Deg, 0f));
            InstanceFinder.ServerManager.Spawn(go);

            var goldPickup = go.GetComponent<WorldGoldPickup>();
            if (goldPickup == null)
            {
                DuskLog.Warn(LogChannel.Loot,
                    $"LootManager: '{worldGoldPickupPrefab.name}' has no WorldGoldPickup component — swap WorldItemPickup for WorldGoldPickup on that prefab.");
                InstanceFinder.ServerManager.Despawn(go.GetComponent<NetworkObject>(), DespawnType.Destroy);
                GoldManager.Instance?.AddGold(amount);
                return null;
            }

            goldPickup.ServerInitialize(amount);
            goldPickup.ServerThrow(throwVelocity, throwTorque);
            return go;
        }

        private Vector3 CalculateSpawnPosition(Vector3 origin, float azimuth, int total)
        {
            Vector3 spawnPos = origin + Vector3.up * spawnHeightOffset;
            if (total > 1 && spawnSpreadRadius > 0f)
            {
                Vector3 radialOffset = new Vector3(Mathf.Cos(azimuth), 0f, Mathf.Sin(azimuth)) * spawnSpreadRadius;
                spawnPos += radialOffset;
            }
            return spawnPos;
        }

        // Spaces items evenly around a full circle, each launched along a cone surface
        // (fixed angle from straight up) so the burst forms a cone widening toward the top.
        private Vector3 ComputeThrowDirection(int index, int total, float baseAngle, SeededRNG rng, out float azimuth)
        {
            float slotDeg  = total > 0 ? 360f / total : 0f;
            float jitter   = slotDeg * 0.5f * azimuthJitter;
            float angleDeg = total == 1
                ? baseAngle
                : (baseAngle + slotDeg * index + RandRange(rng, -jitter, jitter));

            azimuth = angleDeg * Mathf.Deg2Rad;

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

        // Adjusts a single entry's baseChance at runtime (mutates in-memory ScriptableObject only — not saved to disk).
        public void ModifyDropChance(DropLootTable table, int entryIndex, float delta)
        {
            if (table?.entries == null) return;
            if ((uint)entryIndex >= (uint)table.entries.Length) return;
            table.entries[entryIndex].baseChance = Mathf.Clamp01(table.entries[entryIndex].baseChance + delta);
        }
    }
}
