using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Duskborn.UI.Building
{
    // A small code-native flame silhouette so the forge progress indicator does
    // not depend on a placeholder texture. Progress reveals it bottom-to-top.
    internal sealed class ForgeProgressGraphic : MaskableGraphic
    {
        [SerializeField, Range(0, 1)] private float progress = 1f;
        private float phase;
        public float Progress
        {
            get => progress;
            set
            {
                float next = Mathf.Clamp01(value);
                if (Mathf.Approximately(progress, next)) return;
                progress = next;
                SetVerticesDirty();
            }
        }

        public float Phase
        {
            set
            {
                if (Mathf.Approximately(phase, value)) return;
                phase = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (progress <= 0) return;
            var r = GetPixelAdjustedRect();
            float sway = Mathf.Sin(phase * 5.1f) * .035f;
            float shoulder = Mathf.Sin(phase * 3.7f + 1.2f) * .018f;
            var shape = new List<Vector2>
            {
                Point(r, .50f + sway, 1f), Point(r, .63f + shoulder, .82f),
                Point(r, .60f, .66f), Point(r, .76f, .77f),
                Point(r, .89f - shoulder, .53f), Point(r, .84f, .25f),
                Point(r, .70f, .07f), Point(r, .50f, 0f), Point(r, .30f, .07f),
                Point(r, .16f, .25f), Point(r, .11f + shoulder, .54f),
                Point(r, .31f, .39f), Point(r, .27f, .68f), Point(r, .42f - shoulder, .57f),
                Point(r, .41f, .79f)
            };
            var clipped = ClipBelow(shape, r.yMin + r.height * progress);
            if (clipped.Count < 3) return;
            Vector2 center = Vector2.zero;
            foreach (var point in clipped) center += point;
            center /= clipped.Count;
            int centerIndex = helper.currentVertCount;
            helper.AddVert(center, color, Vector2.zero);
            foreach (var point in clipped) helper.AddVert(point, color, Vector2.zero);
            for (int i = 0; i < clipped.Count; i++)
                helper.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 1 + ((i + 1) % clipped.Count));
        }

        private static Vector2 Point(Rect rect, float x, float y) =>
            new(rect.xMin + rect.width * x, rect.yMin + rect.height * y);

        private static List<Vector2> ClipBelow(List<Vector2> source, float ceiling)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < source.Count; i++)
            {
                Vector2 current = source[i];
                Vector2 previous = source[(i + source.Count - 1) % source.Count];
                bool currentInside = current.y <= ceiling;
                bool previousInside = previous.y <= ceiling;
                if (currentInside != previousInside)
                {
                    float t = Mathf.InverseLerp(previous.y, current.y, ceiling);
                    result.Add(Vector2.Lerp(previous, current, t));
                }
                if (currentInside) result.Add(current);
            }
            return result;
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
