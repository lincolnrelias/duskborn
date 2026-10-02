using UnityEngine;
using Duskborn.Audio;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Component attachable to any GameObject or collider to explicitly define the surface type
    /// for footstep detection and impact effects.
    /// </summary>
    [DisallowMultipleComponent]
    public class GroundSurface : MonoBehaviour
    {
        [Header("Surface Type")]
        [Tooltip("This collider's physical surface for footstep sounds.")]
        [SerializeField] private SurfaceType surfaceType = SurfaceType.Grass;

        public SurfaceType SurfaceType
        {
            get => surfaceType;
            set => surfaceType = value;
        }
    }
}
