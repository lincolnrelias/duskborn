using UnityEngine;

namespace Duskborn.Gameplay
{
    [System.Serializable]
    public class EntityStats
    {
        [Header("Base")]
        public float maxHP          = 100f;
        public float damage         = 10f;
        public float attackSpeed    = 1f;
        public float moveSpeed      = 5f;
        public float critChance     = 0f;
        public float critMultiplier = 1.5f;

        // Gear/weapon layer — reset to 1f each ApplyAll, accumulated by gear and weapon contributors.
        [HideInInspector] public float HPMultiplier             = 1f;
        [HideInInspector] public float DamageMultiplier         = 1f;
        [HideInInspector] public float AttackSpeedMultiplier    = 1f;
        [HideInInspector] public float MoveSpeedMultiplier      = 1f;
        [HideInInspector] public float IncomingDamageMultiplier = 1f;
        [HideInInspector] public float CritChanceBonus          = 0f;

        // Buff additive layer — flat additions after gear base (e.g. +20 HP literally).
        [HideInInspector] public float HPBuffAdditive          = 0f;
        [HideInInspector] public float DamageBuffAdditive      = 0f;
        [HideInInspector] public float MoveSpeedBuffAdditive   = 0f;
        [HideInInspector] public float AttackSpeedBuffAdditive = 0f;

        // Buff multiplicative layer — compound factor applied on top of (gear base + additive).
        // Starts at 1 (identity). Each multiplicative buff: Factor *= (1 + value).
        [HideInInspector] public float HPBuffFactor          = 1f;
        [HideInInspector] public float DamageBuffFactor      = 1f;
        [HideInInspector] public float MoveSpeedBuffFactor   = 1f;
        [HideInInspector] public float AttackSpeedBuffFactor = 1f;

        // CritChance buff layers — gear uses CritChanceBonus (flat).
        // Additive buffs add flat crit; multiplicative buffs scale the total.
        [HideInInspector] public float CritChanceBuffAdditive = 0f;
        [HideInInspector] public float CritChanceBuffFactor   = 1f;

        // DamageReduction buff layers — IncomingDamageMultiplier is the gear layer (1 = full damage).
        // Additive buffs subtract flat reduction; multiplicative buffs compound the remaining fraction.
        [HideInInspector] public float IncomingDamageBuffAdditive = 0f;
        [HideInInspector] public float IncomingDamageBuffFactor   = 1f;

        // Effective values: (rawStat × gearMultiplier + buffAdditive) × buffFactor
        public float MaxHP          => (maxHP       * HPMultiplier         + HPBuffAdditive)          * HPBuffFactor;
        public float Damage         => (damage      * DamageMultiplier      + DamageBuffAdditive)       * DamageBuffFactor;
        public float AttackSpeed    => (attackSpeed * AttackSpeedMultiplier + AttackSpeedBuffAdditive)  * AttackSpeedBuffFactor;
        public float MoveSpeed      => (moveSpeed   * MoveSpeedMultiplier   + MoveSpeedBuffAdditive)    * MoveSpeedBuffFactor;
        public float CritMultiplier => critMultiplier;

        // (critChance + gear flat + additive buff) × multiplicative factor, clamped to [0,1].
        public float CritChance => Mathf.Clamp01(
            (critChance + CritChanceBonus + CritChanceBuffAdditive) * CritChanceBuffFactor);

        // (gear fraction − additive buff reduction) × multiplicative factor, floored at 0.1 (10% min).
        public float EffectiveIncomingDamage => Mathf.Max(0.1f,
            (IncomingDamageMultiplier - IncomingDamageBuffAdditive) * IncomingDamageBuffFactor);

        public void ResetMultipliers()
        {
            HPMultiplier             = 1f;
            DamageMultiplier         = 1f;
            AttackSpeedMultiplier    = 1f;
            MoveSpeedMultiplier      = 1f;
            IncomingDamageMultiplier = 1f;
            CritChanceBonus          = 0f;

            HPBuffAdditive          = 0f;
            DamageBuffAdditive      = 0f;
            MoveSpeedBuffAdditive   = 0f;
            AttackSpeedBuffAdditive = 0f;

            HPBuffFactor          = 1f;
            DamageBuffFactor      = 1f;
            MoveSpeedBuffFactor   = 1f;
            AttackSpeedBuffFactor = 1f;

            CritChanceBuffAdditive    = 0f;
            CritChanceBuffFactor      = 1f;
            IncomingDamageBuffAdditive = 0f;
            IncomingDamageBuffFactor   = 1f;
        }
    }
}
