using System.Collections.Generic;
using UnityEngine;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Classes
{
    /// <summary>
    /// Base class for all class-specific ability components.
    /// PlayerCombat calls into this for damage modification, hit notification, and active ability.
    /// Add one concrete subclass (WarriorClass / RangerClass / MageClass) to the player GameObject.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public abstract class ClassAbility : MonoBehaviour
    {
        protected PlayerStats Stats;

        protected virtual void Awake() => Stats = GetComponent<PlayerStats>();

        /// <summary>Optionally modify outgoing damage before it is applied to a target.</summary>
        public virtual float ModifyDamage(float baseDamage, EnemyBase target) => baseDamage;

        /// <summary>Called after a full attack swing resolves, with every enemy that was hit.</summary>
        public virtual void OnAttackCompleted(List<EnemyBase> hitEnemies) { }

        /// <summary>Called when a swing found no valid targets.</summary>
        public virtual void OnAttackMissed() { }

        /// <summary>Called when the player presses the ability key (Q).</summary>
        public abstract void TryUseAbility();
    }
}
