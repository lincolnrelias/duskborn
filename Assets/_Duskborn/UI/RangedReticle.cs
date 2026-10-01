using UnityEngine;

namespace Duskborn.UI
{
    /// <summary>
    /// Compact, outlined crosshair with chunky bars and a single bow-charge meter.
    /// Preserves target coloring, recoil bloom, and normal/critical hit feedback.
    /// Uses Unity's shared white texture with no per-frame allocations.
    /// </summary>
    public static class RangedReticle
    {
        private static readonly Color Ivory = new Color(0.97f, 0.96f, 0.93f, 1f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.30f, 1f);
        private static readonly Color EnemyRed = new Color(1f, 0.28f, 0.22f, 1f);
        private static readonly Color Outline = new Color(0.025f, 0.025f, 0.035f, 0.95f);
        private static readonly Color Track = new Color(0.18f, 0.18f, 0.20f, 1f);

        public static void Draw(
            Vector2 center,
            float scale,
            float draw,
            float shot,
            float hit,
            bool critical,
            bool isAiming = true,
            bool isAimingAtEnemy = false)
        {
            Matrix4x4 originalMatrix = GUI.matrix;
            Color originalColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * scale);

            draw = Mathf.Clamp01(draw);
            shot = Mathf.Clamp01(shot);
            hit = Mathf.Clamp01(hit);
            Color targetColor = isAimingAtEnemy ? EnemyRed : Ivory;
            Color readyColor = draw >= 0.98f ? Gold : targetColor;

            // The center stays fixed while the four bars close in as the bow draws.
            Block(new Rect(-2f, -2f, 4f, 4f), readyColor);
            if (isAiming)
            {
                float gap = Mathf.Lerp(12f, 7f, draw) + shot * 6f;
                const float length = 8f;
                const float width = 4f;
                Block(new Rect(-width * 0.5f, -gap - length, width, length), readyColor);
                Block(new Rect(-width * 0.5f, gap, width, length), readyColor);
                Block(new Rect(-gap - length, -width * 0.5f, length, width), readyColor);
                Block(new Rect(gap, -width * 0.5f, length, width), readyColor);

                // One stable meter below the crosshair gives an explicit charge readout.
                Rect meter = new Rect(-12f, 25f, 24f, 4f);
                Block(meter, Track);
                if (draw > 0f)
                    Fill(new Rect(meter.x, meter.y, meter.width * draw, meter.height), Gold);
            }

            // A short diagonal X confirms hits; gold and heavier strokes mark criticals.
            if (hit > 0.001f)
            {
                Color hitColor = critical ? Gold : Ivory;
                hitColor.a = hit;
                float inside = 8f + (1f - hit) * 3f;
                float length = critical ? 9f : 6f;
                float width = critical ? 4f : 3f;
                for (int i = 0; i < 4; i++)
                {
                    float angle = (45f + 90f * i) * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Line(direction * inside, direction * (inside + length), hitColor, width);
                }
            }

            GUI.matrix = originalMatrix;
            GUI.color = originalColor;
        }

        private static void Block(Rect rect, Color color)
        {
            const float border = 1.5f;
            Color outline = Outline;
            outline.a *= color.a;
            Fill(new Rect(rect.x - border, rect.y - border,
                rect.width + border * 2f, rect.height + border * 2f), outline);
            Fill(rect, color);
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private static void Line(Vector2 start, Vector2 end, Color color, float width)
        {
            Vector2 delta = end - start;
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            Block(new Rect(start.x, start.y - width * 0.5f, delta.magnitude, width), color);
            GUI.matrix = matrix;
        }
    }
}
