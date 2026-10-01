using Duskborn.Gameplay.Player;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Projectiles;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(menuName = "Duskborn/Weapon Behaviours/Ranged")]
    public sealed class RangedWeaponBehaviour : WeaponBehaviour
    {
        public ProjectileDefinition projectile;
        [Min(1f)] public float preferredRange = 15f;

        [Header("Projectile Spawn Override")]
        [Tooltip("When enabled, overrides the player's spawn offset for this specific weapon.")]
        public bool useCustomSpawnOffset;
        [Tooltip("Position offset relative to the player (X = Right, Y = Up, Z = Forward).")]
        public Vector3 spawnPositionOffset = new Vector3(0f, 1.3f, 0f);
        [Tooltip("Rotation offset in Euler angles (Pitch, Yaw, Roll) relative to player or aim direction.")]
        public Vector3 spawnRotationOffset = Vector3.zero;

        public override void OnActionEvent(WeaponEventType type, int actionIndex, CombatContext ctx)
        {
            if (type != WeaponEventType.SpawnProjectile || actionIndex != 0 || projectile == null) return;
            if (ctx.Caster is PlayerCombat player) player.ReleaseRangedAttack();
            else if (ctx.Caster is EnemyBase enemy) enemy.ReleaseRangedAttack();
        }

        // The same authored timeline drives animation and server-side release validation.
        public static bool TryGetTiming(WeaponActionData[] actions, out float release, out float duration)
        {
            release = duration = 0f;
            if (actions == null || actions.Length == 0 || actions[0] == null) return false;
            var action = actions[0];
            action.EnsureMigrated();
            if (!action.HasEntries || action.Entries.Length != 1) return false;
            var entry = action.Entries[0];
            if (entry?.Clip == null || entry.Events == null) return false;
            duration = entry.Clip.length / Mathf.Max(0.01f, action.BaseSpeed);
            foreach (var e in entry.Events)
                if (e.Type == WeaponEventType.SpawnProjectile)
                {
                    release = Mathf.Clamp01(e.NormalizedTime) * duration;
                    return true;
                }
            return false;
        }
    }
}
