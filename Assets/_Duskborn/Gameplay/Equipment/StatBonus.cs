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
        MiningResourceBonus,
        WoodcuttingResourceBonus,
        Lifesteal,
        ThornsDamage,
        GatheringSpeed,
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

        // Human-readable line for item descriptions/tooltips, e.g. "+15% Damage".
        public string FormatLine()
        {
            string sign = Value >= 0 ? "+" : "";
            return $"{sign}{Value * 100:F0}% {Label(Type)}";
        }

        private static string Label(StatType type) => type switch
        {
            StatType.HP                       => "Max HP",
            StatType.Damage                   => "Damage",
            StatType.MoveSpeed                => "Move Speed",
            StatType.AttackSpeed              => "Attack Speed",
            StatType.CritChance               => "Crit Chance",
            StatType.DamageReduction          => "Damage Reduction",
            StatType.MiningResourceBonus      => "Mining Resource Bonus",
            StatType.WoodcuttingResourceBonus => "Woodcutting Resource Bonus",
            StatType.Lifesteal                => "Roubo de Vida",
            StatType.ThornsDamage             => "Dano de Espinhos",
            StatType.GatheringSpeed           => "Velocidade de Coleta",
            _                                 => type.ToString()
        };
    }
}
