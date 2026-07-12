using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Effects
{
    // Shared factory for translucent unlit URP materials (invulnerability ghost, afterimages).
    public static class GhostMaterial
    {
        public static Material Create()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;

            var m = new Material(shader);
            m.SetFloat("_Surface", 1f); // transparent
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }
    }
}
