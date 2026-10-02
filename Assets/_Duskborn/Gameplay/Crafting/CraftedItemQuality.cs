namespace Duskborn.Gameplay.Crafting
{
    /// <summary>
    /// Quality level of a crafted item.
    /// Modifies base attributes and can add special properties / quirks.
    /// </summary>
    public enum CraftedItemQuality
    {
        /// <summary>Standard quality: default attributes at 1.0x.</summary>
        Standard = 0,

        /// <summary>Refined quality: +15% to positive attributes.</summary>
        Refined = 1,

        /// <summary>Masterwork: +30% to positive attributes and a perfect finish.</summary>
        Masterwork = 2,

        /// <summary>Unstable / Cursed: +50% to the primary attribute, but stronger penalties.</summary>
        Unstable = 3
    }

    public static class CraftedQualityExtensions
    {
        public static string GetDisplayName(this CraftedItemQuality quality) => quality switch
        {
            CraftedItemQuality.Standard    => "Standard",
            CraftedItemQuality.Refined  => "Refined",
            CraftedItemQuality.Masterwork => "Masterwork",
            CraftedItemQuality.Unstable  => "Unstable",
            _                            => quality.ToString()
        };

        public static string GetColorHex(this CraftedItemQuality quality) => quality switch
        {
            CraftedItemQuality.Standard    => "#94a3b8",
            CraftedItemQuality.Refined  => "#38bdf8",
            CraftedItemQuality.Masterwork => "#facc15",
            CraftedItemQuality.Unstable  => "#e879f9",
            _                            => "#ffffff"
        };
    }
}
