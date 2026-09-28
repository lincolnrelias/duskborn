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
        private HollowWardenNight _wardenNight;
        private int   _currentPlayerCount;
        private NightDefinition _nightDefinition;
        private float _timelineDuration;
        private int _waveNight;

        public SpawnTimeline ActiveTimeline  => _activeTimeline;
        public int           AliveEnemyCount => _aliveCount;
        public int           RemainingEvents => _activeTimeline == null
                                                ? 0 : _activeTimeline.TotalEnemies - _nextEventIndex;

        private void Awake()
        {
            _wardenNight = GetComponent<HollowWardenNight>();
            if (_wardenNight == null) _wardenNight = gameObject.AddComponent<HollowWardenNight>();
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
                DuskLog.Error(LogChannel.Wave, "No EnemyPrefabRegistry assigned.");
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
            if (nightNumber == 3) _wardenNight.TryBegin();
            int definitionIndex = HollowWardenNightRules.DefinitionIndex(nightNumber, nightDefinitions?.Length ?? 0);
            if (definitionIndex < 0 || nightDefinitions[definitionIndex] == null) return;

            NightDefinition def = nightDefinitions[definitionIndex];
            _waveNight = nightNumber;
            _nightDefinition = def;
            _timelineDuration = Mathf.Max(1f, nightNumber == 3 && DayNightCycle.Instance != null
                ? DayNightCycle.Instance.PhaseDuration : nightDuration);
            _currentPlayerCount = GameSession.Instance != null ? GameSession.Instance.PlayerCount : 1;

            _activeTimeline   = TimelineGenerator.Generate(def, _currentPlayerCount, _timelineDuration,
                                                           GameSession.Instance?.RNG);
            _nextEventIndex   = 0;
            _elapsedNightTime = 0f;
            _aliveCount       = 0;
            _waveActive       = true;

            DuskLog.Log(LogChannel.Wave, $"{_activeTimeline}");
        }

        private void OnNightEnd(int _)
        {
            if (!InstanceFinder.IsServerStarted) return;
            _wardenNight.Cancel();
            _waveActive = false;
            DespawnAll();
        }

        private void Update()
        {
            if (!InstanceFinder.IsServerStarted) return;
            if (!_waveActive || _activeTimeline == null) return;
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Running)
            {
                _wardenNight.Cancel();
                _waveActive = false;
                DespawnAll();
                return;
            }

            _elapsedNightTime += Time.deltaTime;

            while (_nextEventIndex < _activeTimeline.Events.Count &&
                   _activeTimeline.Events[_nextEventIndex].Timestamp <= _elapsedNightTime)
            {
                SpawnFromEvent(_activeTimeline.Events[_nextEventIndex]);
                _nextEventIndex++;
            }
            // A held night outlives its finite budget. Continue normal night-three batches
            // until dawn, preserving living mobs and co-op scaling (including after the kill).
            if (DayNightCycle.Instance != null && HollowWardenNightRules.RepeatWaves(
                _waveNight, DayNightCycle.Instance.IsNight, _elapsedNightTime, _timelineDuration))
            {
                _elapsedNightTime = 0f; // Do not backfill entire missed batches after a hitch.
                _nextEventIndex = 0;
                _activeTimeline = TimelineGenerator.Generate(_nightDefinition, _currentPlayerCount,
                    _timelineDuration, GameSession.Instance?.RNG);
            }
        }

        private void SpawnFromEvent(SpawnEvent evt)
        {
            if (!_pools.TryGetValue(evt.EnemyType, out EnemyPool pool) || pool == null)
            {
                DuskLog.Warn(LogChannel.Wave, $"No pool for {evt.EnemyType}.");
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
