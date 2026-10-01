using UnityEngine;

namespace Duskborn.Gameplay.Projectiles
{
    [CreateAssetMenu(menuName = "Duskborn/Combat/Projectile")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        public GameObject visualPrefab;
        [Tooltip("Visual scale multiplier relative to the prefab's authored scale. Collision radius is configured separately.")]
        public Vector3 visualScale = Vector3.one;
        public ProjectileImpactBehaviour impact;
        public Material trailMaterial;
        public Material impactParticleMaterial;
        [Min(0.1f)] public float speed = 28f;
        [Min(0f)] public float gravityScale = 1f;
        [Min(0.001f)] public float radius = 0.025f;
        [Min(0.1f)] public float lifetime = 8f;
        [Min(0.1f)] public float embeddedLifetime = 15f;
        [Min(0f)] public float damageMultiplier = 1f;
        [Min(0f)] public float impactImpulse = 1f;
        public LayerMask collisionMask = ~0;

        public void ApplyVisualScale(Transform visual)
        {
            if (visual != null && visualPrefab != null)
                visual.localScale = Vector3.Scale(visualPrefab.transform.localScale, visualScale);
        }
    }
}
