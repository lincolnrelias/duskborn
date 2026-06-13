using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Duskborn.Effects
{
    [Serializable, VolumeComponentMenu("Duskborn/Pixelation")]
    public sealed class PixelationVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Size of each pixel block, in screen pixels. Higher = chunkier.")]
        public ClampedFloatParameter pixelSize = new ClampedFloatParameter(4f, 1f, 64f);

        [Tooltip("Blend strength of the pixelation effect.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        public bool IsActive() => intensity.value > 0f && pixelSize.value > 1f;

        public bool IsTileCompatible() => false;
    }
}
