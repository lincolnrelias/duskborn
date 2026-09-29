using UnityEngine;

namespace Duskborn.Gameplay.Projectiles
{
    // Damage is resolved once by the flight before this runs. New projectile types
    // implement their own impact reaction here (explosion, ricochet, piercing, etc.).
    public abstract class ProjectileImpactBehaviour : ScriptableObject
    {
        public abstract void OnImpact(ProjectileFlight flight, Transform surface, Vector3 point);
    }
}
