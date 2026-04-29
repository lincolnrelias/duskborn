using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    public class EnemyPool : MonoBehaviour
    {
        private EnemyBase _prefab;

        private readonly List<EnemyBase> _active = new();

        public event Action<EnemyBase> OnAnyEnemyDied;

        // Inspector-configured path (prefab + size set in Editor) — not used in the networked path.
        [SerializeField] private EnemyBase inspectorPrefab;
        [SerializeField] private int       inspectorSize = 20;

        private void Awake()
        {
            if (inspectorPrefab != null && _prefab == null)
                _prefab = inspectorPrefab;
        }

        // Called by WaveManager when constructing pools at runtime via AddComponent.
        public void Initialize(EnemyBase enemyPrefab, int size)
        {
            _prefab = enemyPrefab;
            // No pre-fill: enemies are instantiated on demand and registered with FishNet on spawn.
        }

        public EnemyBase Spawn(Vector3 position, int playerCount = 1)
        {
            if (_prefab == null)
            {
                Debug.LogError("[EnemyPool] No prefab set. Call Initialize first.");
                return null;
            }

            EnemyBase e = Instantiate(_prefab, position, Quaternion.identity);
            e.OnDied += HandleEnemyDied;
            e.ApplyPlayerCountScaling(playerCount);
            InstanceFinder.ServerManager.Spawn(e.NetworkObject);
            _active.Add(e);
            return e;
        }

        private void HandleEnemyDied(EnemyBase enemy)
        {
            _active.Remove(enemy);
            // Despawn is called inside EnemyBase.Die() — destruction is already handled there.
            OnAnyEnemyDied?.Invoke(enemy);
        }

        public void DespawnAll()
        {
            foreach (var e in new List<EnemyBase>(_active))
            {
                if (e != null && e.IsSpawned)
                    InstanceFinder.ServerManager.Despawn(e.NetworkObject, DespawnType.Destroy);
            }
            _active.Clear();
        }

        public int ActiveCount => _active.Count;
    }
}
