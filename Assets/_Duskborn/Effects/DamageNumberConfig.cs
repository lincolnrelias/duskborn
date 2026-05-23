using UnityEngine;

namespace Duskborn.Effects
{
    [CreateAssetMenu(fileName = "DamageNumberConfig", menuName = "Duskborn/Effects/Damage Number Config")]
    public class DamageNumberConfig : ScriptableObject
    {
        [Header("Normal Hit")]
        public Color normalColor    = Color.white;
        public float normalFontSize = 4f;

        [Header("Critical Hit")]
        public Color critColor      = new Color(1f, 0.85f, 0f);  // bright gold
        public float critFontSize   = 6.5f;

        [Header("Outline")]
        public Color outlineColor = Color.black;
        public float outlineWidth = 0.25f;

        [Header("Animation")]
        public float floatHeight        = 2.5f;
        public float duration           = 1.1f;
        public float horizontalSpread   = 0.5f;  // spawn position jitter (applied by pool)
        public float driftAmount        = 0.2f;  // additional horizontal drift during flight
        public float spawnHeightOffset  = 1.5f;  // world units above entity origin to spawn
        public float fadeStartFraction  = 0.55f; // fraction of duration before fade begins

        [Header("Crit Pop")]
        // Crits spawn oversized and snap down to 1 — the "WHAM" feel.
        public float critPopStartScale = 1.5f;
        public float critPopTime       = 0.1f;
    }
}
