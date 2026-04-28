using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Defines one night's spawn parameters. Pure data — no scene references.
    /// TimelineGenerator reads this to produce a SpawnTimeline at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "Night_X_Definition", menuName = "Duskborn/Night Definition")]
    public class NightDefinition : ScriptableObject
    {
        [Tooltip("Night number this definition applies to (1–6). Night 7 = boss, handled separately.")]
        public int NightNumber;

        [Tooltip("Total spawn budget for 1 player. Scaled up by WaveManager for more players.")]
        [Min(1)] public float BaseBudget = 100f;

        [Tooltip("Enemy pools available for this night. All pools are merged before budget selection.")]
        public EnemySpawnPool[] Pools;
    }
}
