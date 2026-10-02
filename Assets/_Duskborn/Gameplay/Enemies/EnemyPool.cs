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
                DuskLog.Error(LogChannel.Enemy, "No prefab set. Call Initialize first.");
                return null;
            }

            NetworkObject nob = InstanceFinder.NetworkManager != null
                ? InstanceFinder.NetworkManager.GetPooledInstantiated(_prefab.NetworkObject, position, Quaternion.identity, asServer: true)
                : Instantiate(_prefab.NetworkObject, position, Quaternion.identity);

            EnemyBase e = nob.GetComponent<EnemyBase>();
            e.ResetEnemy(position);
            e.OnDied -= HandleEnemyDied;
            e.OnDied += HandleEnemyDied;
            e.ApplyPlayerCountScaling(playerCount);

            if (!nob.IsSpawned)
                InstanceFinder.ServerManager.Spawn(nob);

            _active.Add(e);
            return e;
        }

        private void HandleEnemyDied(EnemyBase enemy)
        {
            enemy.OnDied -= HandleEnemyDied;
            _active.Remove(enemy);
            OnAnyEnemyDied?.Invoke(enemy);
        }

        public void DespawnAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                if (e != null) e.OnDied -= HandleEnemyDied;
                if (e != null && e.IsSpawned)
                {
                    InstanceFinder.ServerManager.Despawn(e.NetworkObject, DespawnType.Pool);
                }
            }
            _active.Clear();
        }

        private void OnDestroy()
        {
            // FishNet owns the objects. Release only this pool's listeners.
            foreach (var enemy in _active)
                if (enemy != null) enemy.OnDied -= HandleEnemyDied;
            _active.Clear();
        }

        public int ActiveCount => _active.Count;
    }
}
