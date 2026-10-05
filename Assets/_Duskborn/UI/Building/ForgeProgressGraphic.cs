using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Duskborn.UI.Building
{
    // Layered curved ribbons keep the flame pointed at every heat level. The old
    // clipped polygon produced a flat bowl and a jagged fan at low fuel levels.
    internal sealed class ForgeProgressGraphic : MaskableGraphic
    {
        private float heat = 1f;
        private float phase;
        public float Progress
        {
            get => heat;
            set { float next = Mathf.Clamp01(value); if (Mathf.Approximately(heat, next)) return; heat = next; SetVerticesDirty(); }
        }
        public float Phase
        {
            set { if (Mathf.Approximately(phase, value)) return; phase = value; if (heat > 0) SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (heat <= 0)
            {
                Ember(mesh, rect, .28f, .10f, .14f, .05f, new Color(.22f, .16f, .12f));
                Ember(mesh, rect, .50f, .09f, .16f, .06f, new Color(.30f, .18f, .11f));
                Ember(mesh, rect, .71f, .11f, .12f, .045f, new Color(.22f, .16f, .12f));
                return;
            }
            float height = .77f + heat * .20f;
            var orange = new Color(1f, .32f, .045f);
            var red = new Color(.84f, .16f, .025f);
            // The side tongues have independent bends and phase offsets.
            Tongue(mesh, rect, .35f, .13f, .17f, height * .72f, -.12f, phase + 2f, orange, red);
            Tongue(mesh, rect, .67f, .13f, .16f, height * .68f, .12f, phase + 4f, orange, red);
            Tongue(mesh, rect, .50f, .07f, .31f, height, .13f, phase, orange, red);
            Tongue(mesh, rect, .50f, .09f, .21f, height * .70f, -.08f, phase + 1f,
                new Color(1f, .84f, .24f), new Color(1f, .52f, .055f));
            Tongue(mesh, rect, .49f, .10f, .12f, height * .42f, .035f, phase + 3f,
                new Color(1f, .97f, .72f), new Color(1f, .83f, .28f));
        }

        private static void Tongue(VertexHelper mesh, Rect rect, float x, float bottom, float width, float height,
            float bend, float phase, Color baseColor, Color tipColor)
        {
            const int segments = 24;
            int start = mesh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float radius = width * Mathf.Pow(Mathf.Sin(Mathf.PI * t), .68f) * (1f - t * .54f);
                float center = x + bend * t * t + Mathf.Sin(phase * 3.2f + t * 4f) * .028f * t * t;
                float y = bottom + height * t;
                Color tint = Color.Lerp(baseColor, tipColor, t);
                mesh.AddVert(new Vector3(rect.xMin + rect.width * (center - radius), rect.yMin + rect.height * y), tint, Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMin + rect.width * (center + radius), rect.yMin + rect.height * y), tint, Vector2.zero);
                if (i == 0) continue;
                int n = start + i * 2;
                mesh.AddTriangle(n - 2, n - 1, n);
                mesh.AddTriangle(n - 1, n + 1, n);
            }
        }

        private static void Ember(VertexHelper mesh, Rect rect, float x, float y, float width, float height, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin + rect.width * x, rect.yMin + rect.height * y), tint, Vector2.zero);
            for (int i = 0; i <= 12; i++)
            {
                float angle = i * Mathf.PI / 6f;
                mesh.AddVert(new Vector3(rect.xMin + rect.width * (x + Mathf.Cos(angle) * width),
                    rect.yMin + rect.height * (y + Mathf.Sin(angle) * height)), tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }

    /// <summary>Lets a forge slot expose an explicit right-click action.</summary>
    public sealed class ForgeSlotClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public System.Action RightClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
                RightClick?.Invoke();
        }
    }
}
