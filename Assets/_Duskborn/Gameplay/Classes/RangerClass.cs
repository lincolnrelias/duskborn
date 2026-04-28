using System.Collections.Generic;
using UnityEngine;
using Duskborn.Gameplay.Enemies;

namespace Duskborn.Gameplay.Classes
{
    /// <summary>
    /// Ranger — ranged DPS. SKELETON — abilities implemented after items/crafting.
    ///
    /// Passive (stub): first shot on a new target = guaranteed crit.
    ///                 2s of continuous movement = +10% attack speed.
    /// Active  (stub): Rain of Arrows — wide arc volley.
    /// </summary>
    public class RangerClass : ClassAbility
    {
        private EnemyBase _lastTargetHit;

        // ── Passive ──────────────────────────────────────────────────────────

        public override float ModifyDamage(float baseDamage, EnemyBase target)
        {
            // First shot on a new target is always a crit (1.5×).
            if (target != _lastTargetHit)
                return baseDamage * 1.5f;

            return baseDamage;
        }

        public override void OnAttackCompleted(List<EnemyBase> hitEnemies)
        {
            if (hitEnemies.Count > 0)
                _lastTargetHit = hitEnemies[0];
        }

        public override void OnAttackMissed() => _lastTargetHit = null;

        // ── Rain of Arrows (stub) ─────────────────────────────────────────────

        public override void TryUseAbility()
        {
            Debug.Log("[Ranger] Rain of Arrows — not yet implemented.");
        }
    }
}
