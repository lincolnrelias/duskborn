using UnityEngine;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Componente mantido apenas para evitar referências ausentes em assets legados.
    /// As partículas foram completamente descartadas.
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
