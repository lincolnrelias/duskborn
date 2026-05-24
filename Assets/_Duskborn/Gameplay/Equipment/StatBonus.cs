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

    // Multiplicative = 0 so existing serialised StatBonus assets default to Multiplicative.
    public enum BonusMode { Multiplicative = 0, Additive = 1 }

    [System.Serializable]
    public struct StatBonus
    {
        public StatType  Type;
        // Multiplicative: adds to the gear multiplier (0.10 = +10% of raw base).
        // Additive: flat value added after gear base is established.
        // DamageReduction (gear): subtracts from IncomingDamageMultiplier (gear layer, clamped at 0.1).
        public float     Value;
        // Mode is only read by the buff system (PlayerBuffContainer). Gear and weapon contributors
        // always accumulate into the gear multiplier layer and ignore this field.
        public BonusMode Mode;
    }
}
