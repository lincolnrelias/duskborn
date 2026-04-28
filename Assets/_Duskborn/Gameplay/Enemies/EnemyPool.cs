using System;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Generic enemy pool. One pool per enemy type, managed by WaveManager.
    /// ResetEnemy clears all OnDied subscribers; the pool re-adds its own handler after reset.
    /// </summary>
    public class EnemyPool : MonoBehaviour
    {
        [SerializeField] private EnemyBase prefab;
        [SerializeField] private int initialSize = 20;

        private readonly Queue<EnemyBase> _pool = new Queue<EnemyBase>();
        private readonly List<EnemyBase> _active = new List<EnemyBase>();

        // Callers can subscribe here to be notified when any enemy from this pool dies.
        public event Action<EnemyBase> OnAnyEnemyDied;

        private void Awake()
        {
            for (int i = 0; i < initialSize; i++)
                Create();
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

            // ResetEnemy nulls all event subscribers; re-add pool's handler cleanly.
            e.ResetEnemy(position);
            e.OnDied += HandleEnemyDied;

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
                _active.Remove(e);
                _pool.Enqueue(e);
            }
        }

        public int ActiveCount => _active.Count;
    }
}
