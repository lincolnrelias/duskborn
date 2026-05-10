using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.Enemies;

namespace Duskborn.Gameplay.Classes
{
    /// <summary>
    /// Warrior — melee frontliner.
    ///
    /// Passive: each consecutive hit on the same enemy stacks +8% damage (max 3×).
    ///          Resets on miss or target switch.
    ///
    /// Active skills are defined on the equipped weapon (see WeaponSkill / CleaveSkill).
    /// </summary>
    public class WarriorClass : ClassAbility
    {
        [Header("Passive")]
        [SerializeField] private float stackBonus = 0.08f;
        [SerializeField] private int   maxStacks  = 3;

        private EnemyBase _lastHitEnemy;
        private int       _hitStack;

        // ── Passive ──────────────────────────────────────────────────────────

        public override float ModifyDamage(float baseDamage, EnemyBase target)
        {
            int stack = (target == _lastHitEnemy) ? _hitStack : 0;
            return baseDamage * (1f + stackBonus * stack);
        }

        public override void OnAttackCompleted(List<EnemyBase> hitEnemies)
        {
            if (hitEnemies.Count == 0) return;

            EnemyBase primary = hitEnemies[0];
            if (primary == _lastHitEnemy)
                _hitStack = Mathf.Min(_hitStack + 1, maxStacks);
            else
            {
                _hitStack     = 1;
                _lastHitEnemy = primary;
            }
        }

        public override void OnAttackMissed()
        {
            _hitStack     = 0;
            _lastHitEnemy = null;
        }

        public override void TryUseAbility() { }
    }
}
