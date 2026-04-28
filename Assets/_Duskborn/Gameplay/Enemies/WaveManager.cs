using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.World;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Generates and observes a SpawnTimeline each night.
    /// Spawns enemies when elapsed night time crosses their scheduled timestamp.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Night Definitions (index 0 = Night 1, length = 6)")]
        [SerializeField] private NightDefinition[] nightDefinitions;

        [Header("Enemy Pools — one per EnemyType, same order as EnemyType enum")]
        [SerializeField] private EnemyPool swarmerPool;
        [SerializeField] private EnemyPool runnerPool;
        [SerializeField] private EnemyPool spitterPool;
        [SerializeField] private EnemyPool brutePool;
        [SerializeField] private EnemyPool elitePool;

        [Header("Spawn")]
        [SerializeField] private SpawnPerimeter spawnPerimeter;

        [Header("Night Duration (seconds) — passed to TimelineGenerator")]
        [SerializeField] private float nightDuration = 120f;

        private Dictionary<EnemyType, EnemyPool> _pools;

        // Active timeline state
        private SpawnTimeline _activeTimeline;
        private int  _nextEventIndex;
        private float _elapsedNightTime;
        private int   _aliveCount;
        private bool  _waveActive;
        private int   _currentPlayerCount;

        // Read-only access for debug / UI
        public SpawnTimeline ActiveTimeline => _activeTimeline;
        public int AliveEnemyCount => _aliveCount;
        public int RemainingEvents => _activeTimeline == null
            ? 0 : _activeTimeline.TotalEnemies - _nextEventIndex;

        // -------------------------------------------------------------------------

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

        // -------------------------------------------------------------------------

        private void OnNightStart(int nightNumber)
        {
            if (nightNumber > nightDefinitions.Length) return; // Night 7 = boss

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

        private void OnNightEnd(int nightNumber)
        {
            _waveActive = false;
            UnsubscribeAllPools();
            DespawnAll();
        }

        // -------------------------------------------------------------------------

        private void Update()
        {
            if (!_waveActive || _activeTimeline == null) return;

            _elapsedNightTime += Time.deltaTime;

            // Fire all events whose timestamp has been reached.
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

            Vector3 pos = spawnPerimeter != null
                ? spawnPerimeter.GetRandomSpawnPoint(GameSession.Instance?.RNG)
                : Vector3.zero;

            EnemyBase enemy = pool.Spawn(pos, _currentPlayerCount);
            pool.OnAnyEnemyDied += HandleEnemyDied;
            _aliveCount++;
        }

        private void HandleEnemyDied(EnemyBase _)
        {
            _aliveCount--;

            bool allEventsDispatched = _nextEventIndex >= _activeTimeline.TotalEnemies;
            if (_waveActive && allEventsDispatched && _aliveCount <= 0)
            {
                Debug.Log("[WaveManager] All enemies dead and timeline exhausted — ending night.");
                UnsubscribeAllPools();
                DayNightCycle.Instance?.ForceEndNight();
            }
        }

        // -------------------------------------------------------------------------

        private void DespawnAll()
        {
            foreach (var pool in _pools.Values)
                pool?.DespawnAll();
            _aliveCount = 0;
        }

        private void UnsubscribeAllPools()
        {
            foreach (var pool in _pools.Values)
                if (pool != null) pool.OnAnyEnemyDied -= HandleEnemyDied;
        }
    }
}
