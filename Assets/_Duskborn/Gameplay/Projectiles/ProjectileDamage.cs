using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Player;
using Duskborn.Gameplay.Classes;
using Duskborn.Gameplay.Equipment;
using UnityEngine;

namespace Duskborn.Gameplay.Projectiles
{
    public static class ProjectileDamage
    {
        public static void Apply(Collider col, Vector3 point, Vector3 direction, float amount,
            bool crit, ICombatEntity source, WeaponItem weapon)
        {
            var enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy != null)
            {
                if (!enemy.IsAlive || !(source is PlayerCombat player) || player == null) return;
                var stats = player.GetComponent<PlayerStats>();
                var ability = player.GetComponent<ClassAbility>();
                if (ability != null) amount = ability.ModifyDamage(amount, enemy);
                amount *= weapon?.GetTypeDamageMultiplier(enemy.Types) ?? 1f;
                enemy.TakeDamage(amount, crit, point, direction, stats);
                if (amount > 0f) player.ConfirmRangedHit(crit);
                ability?.OnAttackCompleted(new System.Collections.Generic.List<EnemyBase> { enemy });
                if (stats != null && stats.IsAlive && stats.EffectiveLifesteal > 0f)
                    stats.Heal(amount * stats.EffectiveLifesteal);
                return;
            }
            var targetPlayer = col.GetComponentInParent<PlayerStats>();
            if (targetPlayer != null)
            {
                if (targetPlayer.IsAlive && source is EnemyBase attacker)
                    targetPlayer.TakeDamage(amount, attacker, crit);
                return;
            }
            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive) damageable.TakeDamage(amount, crit);
        }
    }
}
