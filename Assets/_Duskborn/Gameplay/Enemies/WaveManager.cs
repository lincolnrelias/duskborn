using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.World;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Spawns enemy waves on NightStart. Listens to DayNightCycle events.
    /// Tracks alive enemies; tells DayNightCycle when all enemies are dead.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Wave Data")]
        [SerializeField] private WaveDefinition[] nightDefinitions; // index 0 = Night 1

        [Header("Spawn")]
        [SerializeField] private SpawnPerimeter spawnPerimeter;

        private readonly List<EnemyBase> _aliveEnemies = new List<EnemyBase>();
        private bool _waveActive;

        private void Start()
        {
            DayNightCycle.Instance.OnNightStart += OnNightStart;
            DayNightCycle.Instance.OnNightEnd += OnNightEnd;
        }

        private void OnDestroy()
        {
            if (DayNightCycle.Instance == null) return;
            DayNightCycle.Instance.OnNightStart -= OnNightStart;
            DayNightCycle.Instance.OnNightEnd -= OnNightEnd;
        }

        private void OnNightStart(int nightNumber)
        {
            if (nightNumber > nightDefinitions.Length) return; // Night 7 = boss, handled separately
            WaveDefinition def = nightDefinitions[nightNumber - 1];
            SpawnWave(def);
        }

        private void OnNightEnd(int nightNumber) => _waveActive = false;

        private void SpawnWave(WaveDefinition def)
        {
            _aliveEnemies.Clear();
            _waveActive = true;

            int playerCount = GameSession.Instance != null ? GameSession.Instance.PlayerCount : 1;
            int count = Mathf.RoundToInt(def.BaseEnemyCount * (1f + (playerCount - 1) * 0.4f));

            // Build weight table for weighted random selection
            float[] weights = new float[def.Entries.Length];
            for (int i = 0; i < def.Entries.Length; i++)
                weights[i] = def.Entries[i].Weight;

            SeededRNG rng = GameSession.Instance?.RNG;

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = spawnPerimeter != null
                    ? spawnPerimeter.GetRandomSpawnPoint(rng)
                    : Vector3.zero;

                int entryIndex = rng != null
                    ? rng.WeightedIndex(weights)
                    : Random.Range(0, def.Entries.Length);

                EnemyPool pool = def.Entries[entryIndex].Pool;
                if (pool == null) continue;

                EnemyBase enemy = pool.Spawn(spawnPos, playerCount);
                enemy.OnDied += HandleEnemyDied;
                _aliveEnemies.Add(enemy);
            }

            Debug.Log($"[WaveManager] Night {def.NightNumber}: spawned {count} enemies.");
        }

        private void HandleEnemyDied(EnemyBase enemy)
        {
            _aliveEnemies.Remove(enemy);
            if (_waveActive && _aliveEnemies.Count == 0)
            {
                Debug.Log("[WaveManager] All enemies dead — ending night early.");
                DayNightCycle.Instance?.ForceEndNight();
            }
        }

        public int AliveEnemyCount => _aliveEnemies.Count;
    }
}
