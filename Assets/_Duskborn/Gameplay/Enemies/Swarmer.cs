using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Night 1 enemy. Fast, low HP, high count. No special behavior beyond EnemyBase.
    /// </summary>
    public class Swarmer : EnemyBase
    {
        // Swarmer uses base behavior: find nearest player, close distance, melee.
        // Stats configured on the prefab via the serialized fields on EnemyBase.
    }
}
