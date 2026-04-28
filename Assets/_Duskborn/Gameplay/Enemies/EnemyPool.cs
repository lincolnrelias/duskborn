using System;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Generic enemy pool. Created at runtime by WaveManager via AddComponent + Initialize —
    /// no prefab field or Awake pre-fill needed when constructed this way.
    /// Can still be placed as a scene component and configured via Inspector if desired.
    /// </summary>
    public class EnemyPool : MonoBehaviour
    {
        [SerializeField] private EnemyBase prefab;
        [SerializeField] private int initialSize = 20;

        private readonly Queue<EnemyBase> _pool   = new();
        private readonly List<EnemyBase>  _active = new();

        private bool _initialized;

        public event Action<EnemyBase> OnAnyEnemyDied;

        // Called by Inspector-configured pools (prefab + size set in Editor).
        private void Awake()
        {
            if (prefab != null && !_initialized)
                Fill(initialSize);
        }

        // Called by WaveManager when constructing pools at runtime via AddComponent.
        public void Initialize(EnemyBase enemyPrefab, int size)
        {
            prefab       = enemyPrefab;
            initialSize  = size;
            _initialized = true;
            Fill(size);
        }

        private void Fill(int count)
        {
            for (int i = 0; i < count; i++) Create();
        }

        private EnemyBase Create()
        {
            EnemyBase e = Instantiate(prefab, transform);
            e.gameObject.SetActive(false);
            _pool.Enqueue(e);
            return e;
        }

        public EnemyBase Spawn(Vector3 position, int playerCount = 1)
        {
            if (_pool.Count == 0) Create();
            EnemyBase e = _pool.Dequeue();

            e.ResetEnemy(position);          // nulls all prior event subscribers
            e.OnDied += HandleEnemyDied;     // re-add pool's handler

            e.ApplyPlayerCountScaling(playerCount);
            _active.Add(e);
            return e;
        }

        private void HandleEnemyDied(EnemyBase enemy)
        {
            _active.Remove(enemy);
            _pool.Enqueue(enemy);
            OnAnyEnemyDied?.Invoke(enemy);
        }

        public void DespawnAll()
        {
            foreach (var e in new List<EnemyBase>(_active))
            {
                e.gameObject.SetActive(false);
                _pool.Enqueue(e);
            }
            _active.Clear();
        }

        public int ActiveCount => _active.Count;
    }
}
