using UnityEngine;

namespace Duskborn.Gameplay.Projectiles
{
    [CreateAssetMenu(menuName = "Duskborn/Combat/Projectile")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        public GameObject visualPrefab;
        [Tooltip("Visual scale multiplier relative to the prefab's authored scale. Collision radius is configured separately.")]
        public Vector3 visualScale = Vector3.one;
        [Tooltip("Nock position in the visual prefab's local space. Keeps the arrow's tail in the drawing hand regardless of visual scale.")]
        public Vector3 nockLocalPosition = new Vector3(0f, 0f, -0.81f);
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

        public Vector3 TipPositionFromNock(Vector3 nockPosition, Quaternion rotation)
        {
            Vector3 scale = visualPrefab != null
                ? Vector3.Scale(visualPrefab.transform.localScale, visualScale) : visualScale;
            return nockPosition - rotation * Vector3.Scale(nockLocalPosition, scale);
        }

        public void ApplyVisualScale(Transform visual)
        {
            if (visual != null && visualPrefab != null)
                visual.localScale = Vector3.Scale(visualPrefab.transform.localScale, visualScale);
        }
    }
}
