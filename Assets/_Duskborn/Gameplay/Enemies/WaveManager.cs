using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.World;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Spawns enemy waves on NightStart and tracks alive count.
    /// Owns the EnemyType → EnemyPool mapping so WaveDefinition stays pure data.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Wave Data")]
        [SerializeField] private WaveDefinition[] nightDefinitions; // index 0 = Night 1, length = 6

        [Header("Pools — assign one EnemyPool per EnemyType in the same order as EnemyType enum")]
        [SerializeField] private EnemyPool swarmerPool;
        [SerializeField] private EnemyPool runnerPool;
        [SerializeField] private EnemyPool spitterPool;
        [SerializeField] private EnemyPool brutePool;
        [SerializeField] private EnemyPool elitePool;

        [Header("Spawn")]
        [SerializeField] private SpawnPerimeter spawnPerimeter;

        private Dictionary<EnemyType, EnemyPool> _pools;
        private int _aliveCount;
        private bool _waveActive;

        private void Awake()
        {
            _pools = new Dictionary<EnemyType, EnemyPool>
            {
                { EnemyType.Swarmer, swarmerPool },
                { EnemyType.Runner,  runnerPool  },
                { EnemyType.Spitter, spitterPool },
                { EnemyType.Brute,   brutePool   },
                { EnemyType.Elite,   elitePool   },
            };
        }

        private void Start()
        {
            DayNightCycle.Instance.OnNightStart += OnNightStart;
            DayNightCycle.Instance.OnNightEnd   += OnNightEnd;
        }

        private void OnDestroy()
        {
            if (DayNightCycle.Instance == null) return;
            DayNightCycle.Instance.OnNightStart -= OnNightStart;
            DayNightCycle.Instance.OnNightEnd   -= OnNightEnd;
        }

        private void OnNightStart(int nightNumber)
        {
            if (nightNumber > nightDefinitions.Length) return; // Night 7 = boss
            SpawnWave(nightDefinitions[nightNumber - 1]);
        }

        private void OnNightEnd(int nightNumber)
        {
            _waveActive = false;
            DespawnAll();
        }

        private void SpawnWave(WaveDefinition def)
        {
            _aliveCount = 0;
            _waveActive = true;

            int playerCount = GameSession.Instance != null ? GameSession.Instance.PlayerCount : 1;
            int count = Mathf.RoundToInt(def.BaseEnemyCount * (1f + (playerCount - 1) * 0.4f));

            float[] weights = new float[def.Entries.Length];
            for (int i = 0; i < def.Entries.Length; i++)
                weights[i] = def.Entries[i].Weight;

            SeededRNG rng = GameSession.Instance?.RNG;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = spawnPerimeter != null
                    ? spawnPerimeter.GetRandomSpawnPoint(rng)
                    : Vector3.zero;

                int entryIdx = rng != null
                    ? rng.WeightedIndex(weights)
                    : Random.Range(0, def.Entries.Length);

                EnemyType type = def.Entries[entryIdx].Type;
                if (!_pools.TryGetValue(type, out EnemyPool pool) || pool == null) continue;

                EnemyBase enemy = pool.Spawn(pos, playerCount);
                // Subscribe to this pool's aggregate event — no per-enemy leak.
                pool.OnAnyEnemyDied += HandleEnemyDied;
                _aliveCount++;
            }

            Debug.Log($"[WaveManager] Night {def.NightNumber}: spawned {count} enemies.");
        }

        private void HandleEnemyDied(EnemyBase _)
        {
            _aliveCount--;
            if (_waveActive && _aliveCount <= 0)
            {
                Debug.Log("[WaveManager] All enemies dead — ending night early.");
                // Unsubscribe before forcing end to avoid double-calls
                UnsubscribeAllPools();
                DayNightCycle.Instance?.ForceEndNight();
            }
        }

        private void DespawnAll()
        {
            UnsubscribeAllPools();
            foreach (var pool in _pools.Values)
                pool?.DespawnAll();
            _aliveCount = 0;
        }

        private void UnsubscribeAllPools()
        {
            foreach (var pool in _pools.Values)
                if (pool != null) pool.OnAnyEnemyDied -= HandleEnemyDied;
        }

        public int AliveEnemyCount => _aliveCount;
    }
}
