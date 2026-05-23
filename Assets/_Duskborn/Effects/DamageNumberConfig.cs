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
        public Color critColor      = new Color(1f, 0.6f, 0f);
        public float critFontSize   = 6f;

        [Header("Animation")]
        public float floatHeight      = 2f;
        public float duration         = 1.2f;
        public float horizontalSpread = 0.4f;

        [Header("Crit Punch")]
        public float critPunchScale = 1.4f;
        public float critPunchTime  = 0.08f;
    }
}
