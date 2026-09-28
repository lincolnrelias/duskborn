using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Uses the existing EnemyBase melee/projectile damage path, without enemy AI.</summary>
    public sealed class HollowWardenRoot : EnemyBase
    {
        public override void OnStartServer()
        {
            // Stationary objective; never enable agent movement or ordinary attacks.
            Agent.enabled = false;
        }

        protected override void Update() { }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Agent.enabled = false;
        }

        protected override void Die()
        {
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            base.Die();
        }
    }
}
