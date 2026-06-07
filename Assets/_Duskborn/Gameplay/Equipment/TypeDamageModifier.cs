using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [System.Serializable]
    public struct TypeDamageModifier
    {
        public TargetType Type;

        [Range(0f, 10f)]
        [Tooltip("Extra damage dealt to matching target types, as a fraction of the base hit. " +
                 "0.5 = +50% damage (1.5x), 1 = +100% damage (2x), 10 = +1000% damage (11x). " +
                 "If a target matches more than one modifier, only the single highest Bonus applies — they do not stack.")]
        public float Bonus;

        // Human-readable line for item descriptions/tooltips, e.g. "+50% damage vs Beast".
        public string FormatLine()
        {
            string sign = Bonus >= 0 ? "+" : "";
            return $"{sign}{Bonus * 100:F0}% damage vs {Type}";
        }

        // Best-match wins: only the single highest matching bonus applies (no stacking).
        public static float GetBestMultiplier(IReadOnlyList<TypeDamageModifier> modifiers, TargetType targetTypes)
        {
            float best = 0f;
            if (modifiers != null)
                foreach (var m in modifiers)
                    if ((targetTypes & m.Type) != 0 && m.Bonus > best)
                        best = m.Bonus;
            return 1f + best;
        }
    }
}
