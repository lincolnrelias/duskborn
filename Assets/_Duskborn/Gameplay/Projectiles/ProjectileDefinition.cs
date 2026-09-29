using UnityEngine;

namespace Duskborn.Gameplay.Projectiles
{
    [CreateAssetMenu(menuName = "Duskborn/Combat/Projectile")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        public GameObject visualPrefab;
        public ProjectileImpactBehaviour impact;
        [Min(0.1f)] public float speed = 28f;
        [Min(0f)] public float gravityScale = 1f;
        [Min(0.001f)] public float radius = 0.025f;
        [Min(0.1f)] public float lifetime = 8f;
        [Min(0.1f)] public float embeddedLifetime = 15f;
        [Min(0f)] public float damageMultiplier = 1f;
        [Min(0f)] public float impactImpulse = 1f;
        public LayerMask collisionMask = ~0;
    }
}
