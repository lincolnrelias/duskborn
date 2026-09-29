using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Projectiles;
using Duskborn.Gameplay.Player;
using FishNet.Object;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    public abstract partial class EnemyBase
    {
        private bool rangedShotReleased;
        [ObserversRpc]
        private void ShowEnemyRangedWindupRpc()
        {
            if (!IsServerStarted && _weaponActionPlayer != null && _weaponItem != null)
                _weaponActionPlayer.PlayAction(0, _weaponItem, BuildContext());
        }

        private bool CanStartRangedAttack()
        {
            if (_weaponActionPlayer == null || CurrentTarget == null) return false;
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            Vector3 delta = CurrentTarget.position + Vector3.up - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(CurrentTarget)) return false;
            return true;
        }

        public void ReleaseRangedAttack()
        {
            if (!IsServerStarted || !IsSpawned || !IsAlive || rangedShotReleased || _staggerTimer > 0f || CurrentTarget == null ||
                (_currentTargetStats != null && !_currentTargetStats.IsAlive) ||
                !(weapon?.Behaviour is RangedWeaponBehaviour ranged) || ranged.projectile == null) return;
            rangedShotReleased = true;
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            Vector3 delta = CurrentTarget.position + Vector3.up - origin;
            Vector3 direction = delta.normalized;
            // Aim a low ballistic arc so an archer at its preferred range can actually
            // hit chest height instead of consistently burying arrows in the ground.
            float gravity = Physics.gravity.magnitude * ranged.projectile.gravityScale;
            if (gravity > 0.001f)
            {
                Vector3 up = -Physics.gravity.normalized;
                float height = Vector3.Dot(delta, up);
                Vector3 planar = delta - up * height;
                float distance = planar.magnitude;
                float speedSquared = ranged.projectile.speed * ranged.projectile.speed;
                float discriminant = speedSquared * speedSquared - gravity * (gravity * distance * distance + 2f * height * speedSquared);
                if (distance > 0.001f && discriminant >= 0f)
                {
                    float tangent = (speedSquared - Mathf.Sqrt(discriminant)) / (gravity * distance);
                    direction = (planar.normalized + up * tangent).normalized;
                }
            }
            bool crit = Random.value < CritChance;
            float amount = Damage * ranged.projectile.damageMultiplier * (crit ? CritMultiplier : 1f);
            int shot = ProjectileFlight.NextId();
            ShowEnemyProjectileRpc(shot, origin, direction);
            ProjectileFlight.Launch(shot, ranged.projectile, transform, false, origin, direction, true,
                (col, point, forward) => ProjectileDamage.Apply(col, point, forward, amount, crit, this, _weaponItem),
                PlayerCombat.BroadcastProjectileImpact);
        }

        [ObserversRpc]
        private void ShowEnemyProjectileRpc(int shot, Vector3 origin, Vector3 direction)
        {
            if (!IsServerStarted && weapon?.Behaviour is RangedWeaponBehaviour ranged)
                ProjectileFlight.Launch(shot, ranged.projectile, transform, false, origin, direction, false);
        }

    }
}
