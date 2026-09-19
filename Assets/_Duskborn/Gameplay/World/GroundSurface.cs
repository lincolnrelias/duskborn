using UnityEngine;
using Duskborn.Audio;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Componente anexável a qualquer GameObject ou colisor para definir explicitamente o tipo
    /// de superfície para detecção de passos e efeitos de impacto.
    /// </summary>
    [DisallowMultipleComponent]
    public class GroundSurface : MonoBehaviour
    {
        [Header("Tipo de Superfície")]
        [Tooltip("Superfície física deste colisor para sons de passos.")]
        [SerializeField] private SurfaceType surfaceType = SurfaceType.Grass;

        public SurfaceType SurfaceType
        {
            get => surfaceType;
            set => surfaceType = value;
        }
    }
}
