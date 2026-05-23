using UnityEngine;

namespace Duskborn.Effects
{
    [CreateAssetMenu(fileName = "HealthBarConfig", menuName = "Duskborn/Effects/Health Bar Config")]
    public class HealthBarConfig : ScriptableObject
    {
        [Header("Colors")]
        public Color fullColor  = new Color(0.12f, 0.78f, 0.12f);   // WoW green
        public Color midColor   = new Color(1f,    0.82f, 0f);       // WoW yellow
        public Color lowColor   = new Color(0.82f, 0.10f, 0.10f);   // WoW red
        public Color bgColor    = new Color(0f,    0f,    0f,  0.75f);
        public Color ghostColor = new Color(1f,    1f,    1f,  0.45f);

        [Header("Thresholds")]
        public float midThreshold = 0.5f;
        public float lowThreshold = 0.25f;

        [Header("Timing")]
        public float drainSpeed      = 10f;   // main bar lerp speed
        public float ghostDrainSpeed = 1.5f;  // ghost bar — slower so the gap is visible
        public float fadeDelay       = 3.5f;  // seconds after last damage before fading
        public float fadeDuration    = 0.6f;

        [Header("Layout")]
        public float yOffset = 2.2f;  // world units above entity pivot
    }
}
