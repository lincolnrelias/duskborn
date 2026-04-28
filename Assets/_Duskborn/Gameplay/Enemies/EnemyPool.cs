using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Generic enemy pool. One pool per enemy type, managed by WaveManager.
    /// Avoids Instantiate/Destroy per enemy during dense night waves.
    /// </summary>
    public class EnemyPool : MonoBehaviour
    {
        [SerializeField] private EnemyBase prefab;
        [SerializeField] private int initialSize = 20;

        private readonly Queue<EnemyBase> _pool = new Queue<EnemyBase>();
        private readonly List<EnemyBase> _active = new List<EnemyBase>();

        private void Awake()
        {
            for (int i = 0; i < initialSize; i++)
                CreateAndStore();
        }

        private EnemyBase CreateAndStore()
        {
            EnemyBase e = Instantiate(prefab, transform);
            e.gameObject.SetActive(false);
            e.OnDied += HandleEnemyDied;
            _pool.Enqueue(e);
            return e;
        }

        public EnemyBase Spawn(Vector3 position, int playerCount = 1)
        {
            if (_pool.Count == 0) CreateAndStore();
            EnemyBase e = _pool.Dequeue();
            e.ApplyPlayerCountScaling(playerCount);
            e.ResetEnemy(position);
            _active.Add(e);
            return e;
        }

        private void HandleEnemyDied(EnemyBase enemy)
        {
            _active.Remove(enemy);
            _pool.Enqueue(enemy);
        }

        public void DespawnAll()
        {
            foreach (var e in _active)
            {
                e.gameObject.SetActive(false);
                _pool.Enqueue(e);
            }
            _active.Clear();
        }

        public int ActiveCount => _active.Count;
    }
}
