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

        // Runtime multipliers — modified by buffs, gear, class bonuses.
        [HideInInspector] public float HPMultiplier             = 1f;
        [HideInInspector] public float DamageMultiplier         = 1f;
        [HideInInspector] public float AttackSpeedMultiplier    = 1f;
        [HideInInspector] public float MoveSpeedMultiplier      = 1f;
        [HideInInspector] public float IncomingDamageMultiplier = 1f;
        [HideInInspector] public float CritChanceBonus          = 0f;

        // Effective values.
        public float MaxHP          => maxHP * HPMultiplier;
        public float Damage         => damage * DamageMultiplier;
        public float AttackSpeed    => attackSpeed * AttackSpeedMultiplier;
        public float MoveSpeed      => moveSpeed * MoveSpeedMultiplier;
        public float CritMultiplier => critMultiplier;
        public float CritChance     => Mathf.Clamp01(critChance + CritChanceBonus);

        public void ResetMultipliers()
        {
            HPMultiplier             = 1f;
            DamageMultiplier         = 1f;
            AttackSpeedMultiplier    = 1f;
            MoveSpeedMultiplier      = 1f;
            IncomingDamageMultiplier = 1f;
            CritChanceBonus          = 0f;
        }
    }
}
