using System;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    [Serializable]
    public struct EnemySpawnEntry
    {
        public EnemyType Type;
        [Range(0f, 1f)] public float Weight;
    }

    /// <summary>
    /// Pure data asset defining one night's wave composition.
    /// References enemy TYPES (enum), not scene objects — ScriptableObjects cannot hold scene refs.
    /// WaveManager owns the EnemyType → EnemyPool mapping at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "Night_X_Wave", menuName = "Duskborn/Wave Definition")]
    public class WaveDefinition : ScriptableObject
    {
        [Tooltip("Night number this definition applies to (1–6).")]
        public int NightNumber;

        [Tooltip("Base enemy count for 1 player. Scaled by WaveManager for more players.")]
        public int BaseEnemyCount = 12;

        [Tooltip("Enemy type weights for this night.")]
        public EnemySpawnEntry[] Entries;
    }
}
