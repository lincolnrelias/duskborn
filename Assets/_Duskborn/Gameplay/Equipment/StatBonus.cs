namespace Duskborn.Gameplay.Equipment
{
    public enum StatType
    {
        HP,
        Damage,
        MoveSpeed,
        AttackSpeed,
        CritChance,
        DamageReduction,
    }

    [System.Serializable]
    public struct StatBonus
    {
        public StatType Type;
        // Additive multiplier value — matches existing buff scale: 0.10 = +10%.
        // For DamageReduction: subtracted from IncomingDamageMultiplier (clamped at 0.1).
        public float Value;
    }
}
