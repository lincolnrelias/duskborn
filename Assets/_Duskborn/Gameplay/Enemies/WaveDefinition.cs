using System;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    [Serializable]
    public struct EnemySpawnEntry
    {
        public EnemyPool Pool;
        [Range(0f, 1f)] public float Weight; // relative spawn probability
    }

    /// <summary>
    /// Defines the composition of a single night's enemy wave.
    /// One asset per night (Night 1 through Night 6 — Night 7 is boss).
    /// </summary>
    [CreateAssetMenu(fileName = "Night_X_Wave", menuName = "Duskborn/Wave Definition")]
    public class WaveDefinition : ScriptableObject
    {
        [Tooltip("Night number this definition applies to (1–6).")]
        public int NightNumber;

        [Tooltip("Base enemy count for 1 player. Scaled by WaveManager for more players.")]
        public int BaseEnemyCount = 12;

        [Tooltip("Entries define which enemy types appear and their relative weights.")]
        public EnemySpawnEntry[] Entries;

        [Tooltip("Seconds before dawn regardless of enemy count (safety timer).")]
        public float NightDuration = 120f;
    }
}
