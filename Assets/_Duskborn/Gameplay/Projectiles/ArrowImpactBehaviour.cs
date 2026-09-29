using UnityEngine;

namespace Duskborn.Gameplay.Projectiles
{
    [CreateAssetMenu(menuName = "Duskborn/Combat/Impacts/Embed Arrow")]
    public sealed class ArrowImpactBehaviour : ProjectileImpactBehaviour
    {
        public override void OnImpact(ProjectileFlight flight, Transform surface, Vector3 point)
            => flight.Embed(surface, point);
    }
}
