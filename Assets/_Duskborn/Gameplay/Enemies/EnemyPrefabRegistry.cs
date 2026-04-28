using System;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Single source of truth mapping EnemyType → prefab.
    /// WaveManager reads this at Awake and creates its own internal pools —
    /// no pool GameObjects need to exist in the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyPrefabRegistry", menuName = "Duskborn/Enemy Prefab Registry")]
    public class EnemyPrefabRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public EnemyType Type;
            public EnemyBase Prefab;
            [Min(5)] public int InitialPoolSize;
        }

        public Entry[] Entries;
    }
}
