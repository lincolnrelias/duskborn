using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Effects
{
    /// Pushes the active PixelationVolume settings onto the Pixelation material
    /// each frame, for use with URP's built-in Full Screen Pass Renderer Feature.
    public class PixelationEffectController : MonoBehaviour
    {
        private static readonly int PixelParamsId = Shader.PropertyToID("_PixelParams");

        [SerializeField] private Material pixelationMaterial;

        private void LateUpdate()
        {
            if (pixelationMaterial == null)
            {
                return;
            }

            var pixelation = VolumeManager.instance.stack.GetComponent<PixelationVolume>();
            if (pixelation == null || !pixelation.IsActive())
            {
                pixelationMaterial.SetVector(PixelParamsId, new Vector4(Screen.width, Screen.height, 0f, 0f));
                return;
            }

            float pixelSize = pixelation.pixelSize.value;
            float blocksX = Mathf.Max(1f, Mathf.Round(Screen.width / pixelSize));
            float blocksY = Mathf.Max(1f, Mathf.Round(Screen.height / pixelSize));

            pixelationMaterial.SetVector(PixelParamsId, new Vector4(blocksX, blocksY, pixelation.intensity.value, 0f));
        }
    }
}
