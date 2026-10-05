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
        private static LootManager _instance;
        public static LootManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<LootManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[LootManager]");
                        _instance = go.AddComponent<LootManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

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
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            ConfigureCollisionLayers();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
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

        /// <summary>One guaranteed progression item, using the existing network pickup and rarity effects.</summary>
        public void ServerDropItem(ItemDefinitionBase item, Vector3 origin)
        {
            if (!InstanceFinder.IsServerStarted) return;
            if (item == null || item.dropPrefab == null || string.IsNullOrEmpty(item.Id))
            {
                Debug.LogError("[LootManager] Guaranteed item is missing its definition or pickup prefab.");
                return;
            }
            WorldDropRegistry.Instance.Register(item);
            SpawnItem(item.dropPrefab, item.Id, item.Rarity, origin, 0, 1, 0,
                GameSession.Instance?.RNG, true);
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

            // Expand entries into individual physical items so each gets its own prefab and rarity.
            var spawnList = new List<(GameObject prefab, string id, ItemRarity rarity)>();
            ItemRarity rarestItemRarity = ItemRarity.Common;
            bool hasItems = false;

            foreach (var entry in hits)
            {
                if (entry.itemDefinition == null) continue;

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

                ItemRarity itemRarity = entry.itemDefinition.Rarity;
                if (!hasItems || itemRarity > rarestItemRarity)
                {
                    rarestItemRarity = itemRarity;
                    hasItems = true;
                }

                float bonus = GetResourceBonus(harvester, targetType, entry.itemDefinition);
                int count = CalculateDropCount(entry.minAmount, entry.maxAmount, bonus, rng);

                for (int j = 0; j < count; j++)
                    spawnList.Add((prefab, entry.itemDefinition.Id, itemRarity));
            }

            int total = spawnList.Count + (spawnGold ? 1 : 0);
            if (total == 0) return;

            // Find the first item with the highest rarity and play the drop sound only for that item.
            int rarestItemIndex = -1;
            for (int i = 0; i < spawnList.Count; i++)
            {
                if (spawnList[i].rarity == rarestItemRarity)
                {
                    rarestItemIndex = i;
                    break;
                }
            }

            // Release an irregular, short cascade with precomputed server launch plans.
            SpawnLootCascade(spawnList, spawnGold, goldAmount, origin, total, rng, rarestItemIndex, rarestItemRarity, harvester);

            DuskLog.Log(LogChannel.Loot,
                $"LootManager: dropping {spawnList.Count} item(s) at {origin} (rarest: {rarestItemRarity}, night {currentNight}).");
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

        public struct DropLaunch
        {
            public Vector3 Velocity, Position;
            public float Azimuth, Torque, ReleaseDelay;
        }

        public DropLaunch CreateDropLaunch(Vector3 origin, int index, int total, float baseAngle, SeededRNG rng)
        {
            Vector3 velocity = ComputeThrowDirection(index, total, baseAngle, rng, out float azimuth);
            return new DropLaunch { Velocity = velocity,
                Position = CalculateSpawnPosition(origin, azimuth, total, rng), Azimuth = azimuth,
                Torque = throwTorque * RandRange(rng, .65f, 1.4f),
                ReleaseDelay = SampleReleaseDelay(index, total, rng) };
        }

        public static float SampleReleaseDelay(int index, int total, SeededRNG rng) =>
            index == 0 || total <= 1 ? 0f : RandRange(rng, .025f, Mathf.Min(.32f, .12f + total * .022f));

        private void SpawnLootCascade(
            List<(GameObject prefab, string id, ItemRarity rarity)> spawnList,
            bool spawnGold, int goldAmount, Vector3 origin, int total, SeededRNG rng,
            int rarestItemIndex, ItemRarity rarestRarity, PlayerStats harvester = null)
        {
            float baseAngle = RandRange(rng, 0f, 360f);
            var releases = new List<(GameObject prefab, string id, ItemRarity rarity, bool gold, bool rarest, DropLaunch launch)>();
            for (int i = 0; i < spawnList.Count; i++)
            {
                var entry = spawnList[i];
                releases.Add((entry.prefab, entry.id, entry.rarity, false, i == rarestItemIndex,
                    CreateDropLaunch(origin, i, total, baseAngle, rng)));
            }
            if (spawnGold)
                releases.Add((null, null, ItemRarity.Common, true, false,
                    CreateDropLaunch(origin, spawnList.Count, total, baseAngle, rng)));
            releases.Sort((a, b) => a.launch.ReleaseDelay.CompareTo(b.launch.ReleaseDelay));
            // Sample the whole burst before yielding: timing must not change seeded
            // loot randomness when another node is harvested during the cascade.
            StartCoroutine(ReleaseLoot());

            IEnumerator ReleaseLoot()
            {
                float started = Time.time;
                var spawnedColliders = new List<Collider>();
                foreach (var release in releases)
                {
                    while (Time.time - started < release.launch.ReleaseDelay)
                    {
                        if (!InstanceFinder.IsServerStarted) yield break;
                        yield return null;
                    }
                    if (!InstanceFinder.IsServerStarted) yield break;
                    GameObject go = release.gold
                        ? SpawnGoldPickup(origin, goldAmount, 0, total, baseAngle, null, harvester, release.launch)
                        : SpawnItem(release.prefab, release.id, release.rarity, origin, 0, total,
                            baseAngle, null, release.rarest, harvester, release.launch);
                    if (go != null) RegisterAndIgnoreCollisions(go, spawnedColliders);
                }
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

        private GameObject SpawnItem(
            GameObject prefab,
            string id,
            ItemRarity rarity,
            Vector3 origin,
            int index,
            int total,
            float baseAngle,
            SeededRNG rng,
            bool isRarest,
            PlayerStats harvester = null, DropLaunch? scheduledLaunch = null)
        {
            DropLaunch launch = scheduledLaunch ?? CreateDropLaunch(origin, index, total, baseAngle, rng);
            Vector3 throwVelocity = launch.Velocity;
            Vector3 spawnPos = launch.Position;
            float azimuth = launch.Azimuth;

            var go = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, azimuth * Mathf.Rad2Deg, 0f));
            InstanceFinder.ServerManager.Spawn(go);
            var pickup = go.GetComponent<WorldItemPickup>();
            if (pickup != null)
            {
                NetworkObject targetPlayerNob = harvester != null ? harvester.GetComponent<NetworkObject>() : null;
                pickup.ServerInitialize(id, 1, rarity, targetPlayerNob);
                pickup.ServerThrow(throwVelocity, launch.Torque);
                bool rawCrystal = !string.IsNullOrEmpty(id) &&
                    id.StartsWith("material_", System.StringComparison.Ordinal) &&
                    id.EndsWith("_crystal", System.StringComparison.Ordinal);
                if (isRarest && !rawCrystal)
                {
                    // The sound always comes from the highest-rarity item dropped by that object!
                    pickup.RpcPlayDropSound(rarity);
                }
            }
            return go;
        }

        private GameObject SpawnGoldPickup(Vector3 origin, int amount, int throwIndex, int throwTotal, float baseAngle, SeededRNG rng, PlayerStats harvester = null, DropLaunch? scheduledLaunch = null)
        {
            if (worldGoldPickupPrefab == null)
            {
                DuskLog.Warn(LogChannel.Loot, "LootManager: worldGoldPickupPrefab not assigned — adding gold directly.");
                GoldManager.Instance?.AddGold(amount);
                return null;
            }

            DropLaunch launch = scheduledLaunch ?? CreateDropLaunch(origin, throwIndex, throwTotal, baseAngle, rng);
            Vector3 throwVelocity = launch.Velocity;
            Vector3 spawnPos = launch.Position;
            float azimuth = launch.Azimuth;

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

            goldPickup.ServerInitialize(amount, harvester != null ? harvester.GetComponent<NetworkObject>() : null);
            goldPickup.ServerThrow(throwVelocity, launch.Torque);
            return go;
        }

        private Vector3 CalculateSpawnPosition(Vector3 origin, float azimuth, int total, SeededRNG rng)
        {
            // Sample the disk, not its perimeter: drops no longer begin on a ring.
            float radius = Mathf.Max(0f, spawnSpreadRadius) * Mathf.Sqrt(RandRange(rng, 0f, 1f));
            return origin + new Vector3(Mathf.Cos(azimuth) * radius,
                spawnHeightOffset + RandRange(rng, -.06f, .1f), Mathf.Sin(azimuth) * radius);
        }

        private Vector3 ComputeThrowDirection(int index, int total, float baseAngle, SeededRNG rng, out float azimuth)
        {
            // Independent azimuths allow clusters and gaps instead of regular polygons.
            azimuth = (baseAngle + RandRange(rng, 0f, 360f)) * Mathf.Deg2Rad;
            float cone = Mathf.Clamp(coneAngle + RandRange(rng, -Mathf.Max(18f, coneAngleVariance),
                Mathf.Max(18f, coneAngleVariance)), 5f, 80f) * Mathf.Deg2Rad;
            float variance = Mathf.Max(throwSpeedVariance, throwSpeed * .35f);
            float speed = Mathf.Max(.1f, throwSpeed + RandRange(rng, -variance, variance));
            // Vary lift and outward speed independently so the apexes don't line up.
            float radial = speed * Mathf.Sin(cone) * RandRange(rng, .65f, 1.3f);
            float up = speed * Mathf.Cos(cone) * RandRange(rng, .75f, 1.2f);
            return new Vector3(Mathf.Cos(azimuth) * radial, up, Mathf.Sin(azimuth) * radial);
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
