#if UNITY_EDITOR
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    [CustomEditor(typeof(ItemDefinition))]
    public sealed class ItemDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw all standard fields except EffectValue (we replace it with a slider).
            DrawPropertiesExcluding(serializedObject, "m_Script", "EffectValue");

            EditorGUILayout.Space(4);

            var typeProp  = serializedObject.FindProperty("EffectType");
            var modeProp  = serializedObject.FindProperty("EffectMode");
            var valueProp = serializedObject.FindProperty("EffectValue");

            var effectType = (ItemEffectType)typeProp.enumValueIndex;
            var effectMode = (BonusMode)modeProp.enumValueIndex;

            var cfg = SliderConfig(effectType, effectMode);

            // Clamp current value to range in case EffectType / EffectMode just changed.
            valueProp.floatValue = Mathf.Clamp(valueProp.floatValue, cfg.min, cfg.max);

            valueProp.floatValue = EditorGUILayout.Slider(
                new GUIContent("Effect Value", cfg.tooltip),
                valueProp.floatValue, cfg.min, cfg.max);

            EditorGUILayout.HelpBox(DescribeEffect(effectType, effectMode, valueProp.floatValue),
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        // ── Slider configuration per (EffectType, BonusMode) combination ─────

        private readonly struct Config
        {
            public readonly float  min;
            public readonly float  max;
            public readonly string tooltip;
            public Config(float min, float max, string tooltip)
            { this.min = min; this.max = max; this.tooltip = tooltip; }
        }

        private static Config SliderConfig(ItemEffectType type, BonusMode mode)
        {
            return (type, mode) switch
            {
                // HP ──────────────────────────────────────────────────────────
                (ItemEffectType.BonusHP, BonusMode.Additive)
                    => new Config(1f, 200f,
                        "Flat HP added after gear base (e.g. 20 = +20 HP)."),
                (ItemEffectType.BonusHP, BonusMode.Multiplicative)
                    => new Config(0.05f, 1f,
                        "Compound HP factor: HPBuffFactor *= (1+value). 0.2 = ×1.2."),

                // Damage ──────────────────────────────────────────────────────
                (ItemEffectType.BonusDamage, BonusMode.Additive)
                    => new Config(1f, 50f,
                        "Flat damage added after gear base."),
                (ItemEffectType.BonusDamage, BonusMode.Multiplicative)
                    => new Config(0.05f, 1f,
                        "Compound damage factor: DamageBuffFactor *= (1+value). 0.3 = ×1.3."),

                // MoveSpeed ───────────────────────────────────────────────────
                (ItemEffectType.BonusMoveSpeed, BonusMode.Additive)
                    => new Config(0.1f, 3f,
                        "Flat move-speed added after gear base."),
                (ItemEffectType.BonusMoveSpeed, BonusMode.Multiplicative)
                    => new Config(0.05f, 0.5f,
                        "Compound speed factor: MoveSpeedBuffFactor *= (1+value)."),

                // AttackSpeed ─────────────────────────────────────────────────
                (ItemEffectType.BonusAttackSpeed, BonusMode.Additive)
                    => new Config(0.05f, 1f,
                        "Flat attack-speed added after gear base."),
                (ItemEffectType.BonusAttackSpeed, BonusMode.Multiplicative)
                    => new Config(0.05f, 1f,
                        "Compound attack-speed factor: AttackSpeedBuffFactor *= (1+value)."),

                // CritChance ──────────────────────────────────────────────────
                (ItemEffectType.BonusCritChance, BonusMode.Additive)
                    => new Config(0.01f, 0.5f,
                        "Flat crit chance added after gear. 0.05 = +5%."),
                (ItemEffectType.BonusCritChance, BonusMode.Multiplicative)
                    => new Config(0.05f, 1f,
                        "Compound crit factor: CritChanceBuffFactor *= (1+value). 0.5 = ×1.5 crit."),

                // DamageReduction ─────────────────────────────────────────────
                (ItemEffectType.DamageReduction, BonusMode.Additive)
                    => new Config(0.01f, 0.8f,
                        "Flat reduction subtracted from incoming-damage fraction. 0.2 = −20%."),
                (ItemEffectType.DamageReduction, BonusMode.Multiplicative)
                    => new Config(0.01f, 0.8f,
                        "Compound reduction: IncomingDamageBuffFactor *= (1−value). 0.2 = take 20% less of remaining."),

                _ => new Config(0f, 1f, "Value"),
            };
        }

        // ── Human-readable effect description shown in the HelpBox ───────────

        private static string DescribeEffect(ItemEffectType type, BonusMode mode, float v)
        {
            return (type, mode) switch
            {
                (ItemEffectType.BonusHP, BonusMode.Additive)
                    => $"+{v:0} HP (flat, applied after gear base)",
                (ItemEffectType.BonusHP, BonusMode.Multiplicative)
                    => $"×{1f + v:0.00} HP — {v * 100f:0}% more HP on top of base+additive",

                (ItemEffectType.BonusDamage, BonusMode.Additive)
                    => $"+{v:0.#} flat damage (applied after gear base)",
                (ItemEffectType.BonusDamage, BonusMode.Multiplicative)
                    => $"×{1f + v:0.00} damage — {v * 100f:0}% more damage",

                (ItemEffectType.BonusMoveSpeed, BonusMode.Additive)
                    => $"+{v:0.##} flat move speed",
                (ItemEffectType.BonusMoveSpeed, BonusMode.Multiplicative)
                    => $"×{1f + v:0.00} move speed — {v * 100f:0}% more speed",

                (ItemEffectType.BonusAttackSpeed, BonusMode.Additive)
                    => $"+{v:0.##} flat attack speed",
                (ItemEffectType.BonusAttackSpeed, BonusMode.Multiplicative)
                    => $"×{1f + v:0.00} attack speed — {v * 100f:0}% faster attacks",

                (ItemEffectType.BonusCritChance, BonusMode.Additive)
                    => $"+{v * 100f:0.#}% crit chance (flat, stacks with gear crit)",
                (ItemEffectType.BonusCritChance, BonusMode.Multiplicative)
                    => $"×{1f + v:0.00} crit — scales total crit chance (base + gear + additive)",

                (ItemEffectType.DamageReduction, BonusMode.Additive)
                    => $"−{v * 100f:0.#}% incoming damage (flat, stacks additively with gear)",
                (ItemEffectType.DamageReduction, BonusMode.Multiplicative)
                    => $"Take {(1f - v) * 100f:0.#}% of remaining damage (compound reduction)",

                _ => $"Value: {v}",
            };
        }
    }
}
#endif
