using UnityEngine;

namespace Duskborn.UI
{
    /// <summary>
    /// Vector-drawn procedural reticle for Duskborn ranged combat.
    /// Features bold antialiased geometry, dynamic enemy-target coloring,
    /// dual tension charge gauges, recoil bloom, and distinct normal/critical hit feedback.
    /// Zero per-frame GC allocations.
    /// </summary>
    public static class RangedReticle
    {
        private static Texture2D s_LineTex;
        private static Texture2D s_DotTex;
        private static Texture2D s_GlowTex;

        // Neutral palette
        private static readonly Color NeutralIvory = new Color(0.97f, 0.96f, 0.93f, 0.98f);
        private static readonly Color NeutralAmber = new Color(1.0f, 0.74f, 0.22f, 1.0f);
        private static readonly Color NeutralGold = new Color(1.0f, 0.90f, 0.40f, 1.0f);
        private static readonly Color GaugeTrack = new Color(0.08f, 0.08f, 0.10f, 0.60f);

        // Hostile / Enemy acquired palette
        private static readonly Color EnemyRed = new Color(1.0f, 0.22f, 0.18f, 1.0f);
        private static readonly Color EnemyBright = new Color(1.0f, 0.42f, 0.28f, 1.0f);
        private static readonly Color EnemyGold = new Color(1.0f, 0.85f, 0.35f, 1.0f);

        // Hit feedback palette
        private static readonly Color CritRed = new Color(1.0f, 0.20f, 0.16f, 1.0f);
        private static readonly Color CritGold = new Color(1.0f, 0.88f, 0.35f, 1.0f);
        private static readonly Color HitWhite = new Color(1.0f, 0.96f, 0.88f, 1.0f);

        private static void EnsureTextures()
        {
            if (s_LineTex != null && s_DotTex != null && s_GlowTex != null) return;

            // 1. Antialiased line texture with solid core (80% solid, 20% feather) for bold visibility
            s_LineTex = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            Color[] linePixels = new Color[16 * 16];
            for (int y = 0; y < 16; y++)
            {
                float dy = Mathf.Abs((y + 0.5f) / 16f - 0.5f) * 2f;
                float alpha = Mathf.Clamp01(Mathf.SmoothStep(1f, 0f, (dy - 0.78f) / 0.22f));
                for (int x = 0; x < 16; x++)
                {
                    linePixels[y * 16 + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            s_LineTex.SetPixels(linePixels);
            s_LineTex.Apply();

            // 2. Smooth circular dot texture with solid core
            s_DotTex = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            Color[] dotPixels = new Color[32 * 32];
            Vector2 center = new Vector2(16f, 16f);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / 15f;
                    float alpha = Mathf.Clamp01(Mathf.SmoothStep(1f, 0f, (dist - 0.80f) / 0.20f));
                    dotPixels[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            s_DotTex.SetPixels(dotPixels);
            s_DotTex.Apply();

            // 3. Radial glow texture
            s_GlowTex = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            Color[] glowPixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / 15f;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 2.0f);
                    glowPixels[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            s_GlowTex.SetPixels(glowPixels);
            s_GlowTex.Apply();
        }

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
            EnsureTextures();

            Matrix4x4 origMatrix = GUI.matrix;
            Color origColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * scale);

            // Dynamic color based on whether we are targeting a live hostile
            Color baseCol = isAimingAtEnemy ? EnemyRed : NeutralIvory;
            Color chargeCol = isAimingAtEnemy
                ? Color.Lerp(EnemyBright, EnemyGold, draw)
                : Color.Lerp(NeutralAmber, NeutralGold, draw);
            Color limbCol = isAimingAtEnemy
                ? Color.Lerp(EnemyRed, EnemyBright, draw * 0.75f)
                : Color.Lerp(NeutralIvory, NeutralAmber, draw * 0.75f);
            Color dotCol = isAimingAtEnemy
                ? (draw >= 0.98f ? EnemyGold : EnemyRed)
                : Color.Lerp(NeutralIvory, NeutralGold, draw);

            if (isAiming)
            {
                // Dynamic contraction with draw tension + recoil kick
                float baseRadius = Mathf.Lerp(26f, 17f, draw);
                float radius = baseRadius + shot * 11f;

                // 1. Center Precision Marks (Bold and sharp)
                Color crossCol = new Color(baseCol.r, baseCol.g, baseCol.b, 0.90f);
                const float crossInner = 5.5f;
                const float crossOuter = 11.0f;
                const float crossWidth = 2.6f;
                Line(new Vector2(0f, -crossOuter), new Vector2(0f, -crossInner), crossCol, crossWidth);
                Line(new Vector2(0f, crossInner), new Vector2(0f, crossOuter), crossCol, crossWidth);
                Line(new Vector2(-crossOuter, 0f), new Vector2(-crossInner, 0f), crossCol, crossWidth);
                Line(new Vector2(crossInner, 0f), new Vector2(crossOuter, 0f), crossCol, crossWidth);

                // Center Pinpoint Dot with halo at full draw or hostile lock
                float dotRadius = Mathf.Lerp(3.2f, 4.2f, draw);
                if (draw >= 0.98f || isAimingAtEnemy)
                {
                    Color haloCol = isAimingAtEnemy
                        ? new Color(EnemyRed.r, EnemyRed.g, EnemyRed.b, draw >= 0.98f ? 0.60f : 0.35f)
                        : new Color(NeutralGold.r, NeutralGold.g, NeutralGold.b, 0.50f);
                    Glow(Vector2.zero, 14f, haloCol);
                }
                Dot(Vector2.zero, dotRadius, dotCol);

                // 2. Bold Bow Limb Framing Brackets
                const float limbWidth = 3.6f;

                // Left Bracket (135° to 225°)
                Arc(radius, 135f, 225f, limbCol, limbWidth, 12);
                Vector2 leftTopTip = GetDir(135f) * radius;
                Vector2 leftBotTip = GetDir(225f) * radius;
                Line(leftTopTip, leftTopTip + new Vector2(-3.5f, -3.5f), limbCol, 2.8f);
                Line(leftBotTip, leftBotTip + new Vector2(-3.5f, 3.5f), limbCol, 2.8f);
                Line(new Vector2(-radius - 5.0f, 0f), new Vector2(-radius + 2.0f, 0f), limbCol, 2.8f);

                // Right Bracket (-45° to 45°)
                Arc(radius, -45f, 45f, limbCol, limbWidth, 12);
                Vector2 rightTopTip = GetDir(-45f) * radius;
                Vector2 rightBotTip = GetDir(45f) * radius;
                Line(rightTopTip, rightTopTip + new Vector2(3.5f, -3.5f), limbCol, 2.8f);
                Line(rightBotTip, rightBotTip + new Vector2(3.5f, 3.5f), limbCol, 2.8f);
                Line(new Vector2(radius - 2.0f, 0f), new Vector2(radius + 5.0f, 0f), limbCol, 2.8f);

                // 3. Symmetrical Dual Tension Gauges (Charge Meter)
                float gaugeRadius = radius + 8.5f;
                // High-contrast background track
                Arc(gaugeRadius, 145f, 215f, GaugeTrack, 3.6f, 10);
                Arc(gaugeRadius, -35f, 35f, GaugeTrack, 3.6f, 10);

                if (draw > 0.02f)
                {
                    float leftCurrent = Mathf.Lerp(215f, 145f, draw);
                    float rightCurrent = Mathf.Lerp(-35f, 35f, draw);
                    Arc(gaugeRadius, 215f, leftCurrent, chargeCol, 4.0f, 10);
                    Arc(gaugeRadius, -35f, rightCurrent, chargeCol, 4.0f, 10);

                    // Advancing charge pips
                    Dot(GetDir(leftCurrent) * gaugeRadius, 2.6f, chargeCol, false);
                    Dot(GetDir(rightCurrent) * gaugeRadius, 2.6f, chargeCol, false);
                }

                // 4. Full Draw Lock-In Indication
                if (draw >= 0.98f)
                {
                    Color lockCol = isAimingAtEnemy ? EnemyGold : NeutralGold;
                    Dot(GetDir(145f) * gaugeRadius, 3.2f, lockCol);
                    Dot(GetDir(215f) * gaugeRadius, 3.2f, lockCol);
                    Dot(GetDir(35f) * gaugeRadius, 3.2f, lockCol);
                    Dot(GetDir(-35f) * gaugeRadius, 3.2f, lockCol);

                    // Top alignment notch
                    float topY = -radius - 7.0f;
                    Line(new Vector2(0f, topY - 6.5f), new Vector2(0f, topY), lockCol, 3.2f);
                    Dot(new Vector2(0f, topY - 6.5f), 2.5f, lockCol);
                }
            }
            else
            {
                // Resting hip stance: bold center dot with enemy awareness
                float restingRadius = isAimingAtEnemy ? 3.8f : 3.2f;
                if (isAimingAtEnemy)
                {
                    Glow(Vector2.zero, 12f, new Color(EnemyRed.r, EnemyRed.g, EnemyRed.b, 0.45f));
                }
                Dot(Vector2.zero, restingRadius, baseCol);
            }

            // 5. Visceral Hit Feedback (Persistent and thick)
            if (hit > 0.001f)
            {
                if (critical)
                {
                    Color critCol = new Color(CritRed.r, CritRed.g, CritRed.b, hit);
                    Color goldCol = new Color(CritGold.r, CritGold.g, CritGold.b, hit);
                    float inside = 10f + (1f - hit) * 5f;
                    const float length = 12f;

                    Glow(Vector2.zero, 24f * hit, new Color(CritRed.r, CritRed.g, CritRed.b, hit * 0.55f));

                    for (int i = 0; i < 4; i++)
                    {
                        float angle = 45f + 90f * i;
                        Vector2 dir = GetDir(angle);
                        Vector2 tip = dir * (inside + length);

                        Line(dir * inside, tip, critCol, 4.2f);
                        Vector2 perp = new Vector2(-dir.y, dir.x);
                        Line(tip, tip - dir * 5.0f + perp * 5.0f, goldCol, 3.0f);
                        Line(tip, tip - dir * 5.0f - perp * 5.0f, goldCol, 3.0f);
                    }
                }
                else
                {
                    Color hitCol = new Color(HitWhite.r, HitWhite.g, HitWhite.b, hit);
                    float inside = 8f + (1f - hit) * 4f;
                    const float length = 8f;

                    for (int i = 0; i < 4; i++)
                    {
                        float angle = 45f + 90f * i;
                        Vector2 dir = GetDir(angle);
                        Line(dir * inside, dir * (inside + length), hitCol, 3.6f);
                    }
                }
            }

            GUI.matrix = origMatrix;
            GUI.color = origColor;
        }

        private static void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.001f) return;

            Matrix4x4 matrix = GUI.matrix;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, a);

            // Rich opaque drop-shadow for guaranteed contrast
            Color shadowCol = new Color(0.02f, 0.02f, 0.03f, color.a * 0.95f);
            float shadowW = width + 3.2f;
            GUI.color = shadowCol;
            GUI.DrawTexture(new Rect(a.x - 0.5f, a.y - shadowW * 0.5f, len + 1f, shadowW), s_LineTex);

            // Bold foreground line
            GUI.color = color;
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), s_LineTex);

            GUI.matrix = matrix;
        }

        private static void Dot(Vector2 pos, float radius, Color color, bool shadow = true)
        {
            float d = radius * 2f;
            if (shadow)
            {
                Color shadowCol = new Color(0.02f, 0.02f, 0.03f, color.a * 0.95f);
                float sd = d + 3.2f;
                GUI.color = shadowCol;
                GUI.DrawTexture(new Rect(pos.x - radius - 1.6f, pos.y - radius - 1.6f, sd, sd), s_DotTex);
            }
            GUI.color = color;
            GUI.DrawTexture(new Rect(pos.x - radius, pos.y - radius, d, d), s_DotTex);
        }

        private static void Glow(Vector2 pos, float radius, Color color)
        {
            float d = radius * 2f;
            GUI.color = color;
            GUI.DrawTexture(new Rect(pos.x - radius, pos.y - radius, d, d), s_GlowTex);
        }

        private static void Arc(float radius, float startDeg, float endDeg, Color color, float width, int segments)
        {
            float step = (endDeg - startDeg) / segments;
            float rad0 = startDeg * Mathf.Deg2Rad;
            Vector2 last = new Vector2(Mathf.Cos(rad0), Mathf.Sin(rad0)) * radius;

            for (int i = 1; i <= segments; i++)
            {
                float currentDeg = startDeg + step * i;
                float rad = currentDeg * Mathf.Deg2Rad;
                Vector2 next = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                Line(last, next, color, width);
                last = next;
            }
        }

        private static Vector2 GetDir(float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
