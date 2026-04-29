using System.Collections.Generic;
using FishNet;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Enemies
{
    public class WaveManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private EnemyPrefabRegistry enemyRegistry;
        [SerializeField] private NightDefinition[]   nightDefinitions;

        [Header("Spawn Radius")]
        [SerializeField] private Transform spawnCenter;
        [SerializeField] private float     spawnRadiusMin = 30f;
        [SerializeField] private float     spawnRadiusMax = 50f;

        [Header("Night Duration (seconds)")]
        [SerializeField] private float nightDuration = 120f;

        private readonly Dictionary<EnemyType, EnemyPool> _pools = new();

        private SpawnTimeline _activeTimeline;
        private int   _nextEventIndex;
        private float _elapsedNightTime;
        private int   _aliveCount;
        private bool  _waveActive;
        private int   _currentPlayerCount;

        public SpawnTimeline ActiveTimeline  => _activeTimeline;
        public int           AliveEnemyCount => _aliveCount;
        public int           RemainingEvents => _activeTimeline == null
                                                ? 0 : _activeTimeline.TotalEnemies - _nextEventIndex;

        private void Awake()
        {
            BuildPools();
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

        private void BuildPools()
        {
            if (enemyRegistry == null)
            {
                Debug.LogError("[WaveManager] No EnemyPrefabRegistry assigned.");
                return;
            }

            foreach (var entry in enemyRegistry.Entries)
            {
                if (entry.Prefab == null) continue;

                var poolGO = new GameObject($"Pool_{entry.Type}");
                poolGO.transform.SetParent(transform);
                var pool = poolGO.AddComponent<EnemyPool>();
                pool.Initialize(entry.Prefab, entry.InitialPoolSize);
                pool.OnAnyEnemyDied += HandleEnemyDied;
                _pools[entry.Type] = pool;
            }
        }

        private Vector3 GetSpawnPosition()
        {
            SeededRNG rng    = GameSession.Instance?.RNG;
            Vector3   center = spawnCenter != null ? spawnCenter.position : Vector3.zero;

            float angle  = rng != null ? rng.Range(0f, 360f)                      : Random.Range(0f, 360f);
            float radius = rng != null ? rng.Range(spawnRadiusMin, spawnRadiusMax) : Random.Range(spawnRadiusMin, spawnRadiusMax);
            float rad    = angle * Mathf.Deg2Rad;

            return center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
        }

        private void OnNightStart(int nightNumber)
        {
            if (!InstanceFinder.IsServerStarted) return;
            if (nightNumber > nightDefinitions.Length) return;

            NightDefinition def = nightDefinitions[nightNumber - 1];
            _currentPlayerCount = GameSession.Instance != null ? GameSession.Instance.PlayerCount : 1;

            _activeTimeline   = TimelineGenerator.Generate(def, _currentPlayerCount, nightDuration,
                                                           GameSession.Instance?.RNG);
            _nextEventIndex   = 0;
            _elapsedNightTime = 0f;
            _aliveCount       = 0;
            _waveActive       = true;

            Debug.Log($"[WaveManager] {_activeTimeline}");
        }

        private void OnNightEnd(int _)
        {
            if (!InstanceFinder.IsServerStarted) return;
            _waveActive = false;
            DespawnAll();
        }

        private void Update()
        {
            if (!InstanceFinder.IsServerStarted) return;
            if (!_waveActive || _activeTimeline == null) return;

            _elapsedNightTime += Time.deltaTime;

            while (_nextEventIndex < _activeTimeline.Events.Count &&
                   _activeTimeline.Events[_nextEventIndex].Timestamp <= _elapsedNightTime)
            {
                SpawnFromEvent(_activeTimeline.Events[_nextEventIndex]);
                _nextEventIndex++;
            }
        }

        private void SpawnFromEvent(SpawnEvent evt)
        {
            if (!_pools.TryGetValue(evt.EnemyType, out EnemyPool pool) || pool == null)
            {
                Debug.LogWarning($"[WaveManager] No pool for {evt.EnemyType}.");
                return;
            }

            pool.Spawn(GetSpawnPosition(), _currentPlayerCount);
            _aliveCount++;
        }

        private void HandleEnemyDied(EnemyBase _)
        {
            if (!_waveActive) return;
            _aliveCount--;
        }

        private void DespawnAll()
        {
            foreach (var pool in _pools.Values) pool?.DespawnAll();
            _aliveCount = 0;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = spawnCenter != null ? spawnCenter.position : Vector3.zero;
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
            Gizmos.DrawWireSphere(center, spawnRadiusMin);
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
            Gizmos.DrawWireSphere(center, spawnRadiusMax);
        }
    }
}
