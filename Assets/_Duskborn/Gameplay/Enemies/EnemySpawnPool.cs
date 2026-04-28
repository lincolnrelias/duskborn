using System;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    [Serializable]
    public struct EnemySpawnPoolEntry
    {
        [Tooltip("Which enemy type this entry represents.")]
        public EnemyType Type;

        [Tooltip("Budget cost to include one of this enemy in the night.")]
        [Min(1)] public int Cost;

        [Tooltip("Relative probability of this enemy being selected when the budget picker runs.")]
        [Range(0.01f, 1f)] public float Weight;
    }

    /// <summary>
    /// A named set of enemy types available for budget spending.
    /// Multiple pools can be associated to a single night — pools are combined before selection.
    /// Example pools: "Basic Melee", "Ranged Threats", "Heavy Hitters".
    /// </summary>
    [CreateAssetMenu(fileName = "Pool_Name", menuName = "Duskborn/Enemy Spawn Pool")]
    public class EnemySpawnPool : ScriptableObject
    {
        public string PoolName;
        public EnemySpawnPoolEntry[] Entries;

        /// <summary>Cheapest enemy in this pool — used to detect when budget is exhausted.</summary>
        public int MinCost
        {
            get
            {
                int min = int.MaxValue;
                foreach (var e in Entries) if (e.Cost < min) min = e.Cost;
                return min == int.MaxValue ? 1 : min;
            }
        }
    }
}
