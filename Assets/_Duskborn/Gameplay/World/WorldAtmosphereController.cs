using UnityEngine;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Component retained only to avoid missing references in legacy assets.
    /// Particles have been completely removed.
    /// </summary>
    public class WorldAtmosphereController : MonoBehaviour
    {
        private void Awake()
        {
            enabled = false;
        }

        public void EnsureParticleSystem()
        {
            enabled = false;
        }
    }
}
