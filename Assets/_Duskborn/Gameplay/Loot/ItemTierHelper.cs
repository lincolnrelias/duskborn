using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    /// <summary>
    /// Central utility for the tier / rarity system (Common -> Uncommon -> Rare -> Epic -> Legendary).
    /// Centralizes the color palette, light intensities, and vertical beam dimensions (Diablo style).
    /// e nomenclatura localizada.
    /// </summary>
    public static class ItemTierHelper
    {
        // Vibrant colors tuned for Duskborn's low-poly / stylized aesthetic with strong visual contrast.
        public static readonly Color ColorCommon    = new Color(0.88f, 0.90f, 0.94f, 1f); // White / Soft Silver
        public static readonly Color ColorUncommon  = new Color(0.18f, 0.88f, 0.35f, 1f); // Vibrant Emerald Green
        public static readonly Color ColorRare      = new Color(0.22f, 0.58f, 1.00f, 1f); // Crystalline Sapphire Blue
        public static readonly Color ColorEpic      = new Color(0.72f, 0.28f, 1.00f, 1f); // Deep Arcane Purple
        public static readonly Color ColorLegendary = new Color(1.00f, 0.62f, 0.08f, 1f); // Solar orange / radiant gold.
        public static readonly Color ColorCursed    = new Color(0.95f, 0.22f, 0.22f, 1f); // Crimson Red

        public static Color GetColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common    => ColorCommon,
                ItemRarity.Uncommon  => ColorUncommon,
                ItemRarity.Rare      => ColorRare,
                ItemRarity.Epic      => ColorEpic,
                ItemRarity.Legendary => ColorLegendary,
                ItemRarity.Cursed    => ColorCursed,
                _                    => ColorCommon
            };
        }

        public static float GetLightIntensity(ItemRarity rarity)
        {
            // Soft light ("dim light") creates a subtle ground highlight without overpowering the environment.
            return rarity switch
            {
                ItemRarity.Common    => 0.20f,
                ItemRarity.Uncommon  => 0.28f,
                ItemRarity.Rare      => 0.38f,
                ItemRarity.Epic      => 0.50f,
                ItemRarity.Legendary => 0.65f,
                ItemRarity.Cursed    => 0.55f,
                _                    => 0.22f
            };
        }

        public static float GetLightRange(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common    => 1.1f,
                ItemRarity.Uncommon  => 1.4f,
                ItemRarity.Rare      => 1.7f,
                ItemRarity.Epic      => 2.1f,
                ItemRarity.Legendary => 2.5f,
                ItemRarity.Cursed    => 2.2f,
                _                    => 1.3f
            };
        }

        public static float GetBeamHeight(ItemRarity rarity)
        {
            // Vertical pillar height extending toward the sky (Diablo / ARPG skyward beams).
            return rarity switch
            {
                ItemRarity.Common    => 14.0f,
                ItemRarity.Uncommon  => 22.0f,
                ItemRarity.Rare      => 32.0f,
                ItemRarity.Epic      => 44.0f,
                ItemRarity.Legendary => 58.0f,
                ItemRarity.Cursed    => 46.0f,
                _                    => 15.0f
            };
        }

        public static float GetBeamWidth(ItemRarity rarity)
        {
            // Slender stylized width for a crisp, elegant skyward light pillar.
            return rarity switch
            {
                ItemRarity.Common    => 0.10f,
                ItemRarity.Uncommon  => 0.14f,
                ItemRarity.Rare      => 0.18f,
                ItemRarity.Epic      => 0.23f,
                ItemRarity.Legendary => 0.30f,
                ItemRarity.Cursed    => 0.25f,
                _                    => 0.12f
            };
        }

        public static float GetBeamAlpha(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common    => 0.35f,
                ItemRarity.Uncommon  => 0.50f,
                ItemRarity.Rare      => 0.65f,
                ItemRarity.Epic      => 0.80f,
                ItemRarity.Legendary => 0.95f,
                ItemRarity.Cursed    => 0.80f,
                _                    => 0.40f
            };
        }

        public static float GetHaloScale(ItemRarity rarity)
        {
            // Compact, subtle ground disc anchors the item's presence without flooding the ground.
            return rarity switch
            {
                ItemRarity.Common    => 0.28f,
                ItemRarity.Uncommon  => 0.36f,
                ItemRarity.Rare      => 0.46f,
                ItemRarity.Epic      => 0.58f,
                ItemRarity.Legendary => 0.72f,
                ItemRarity.Cursed    => 0.60f,
                _                    => 0.30f
            };
        }

        public static string GetLocalizedName(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common    => "Common",
                ItemRarity.Uncommon  => "Uncommon",
                ItemRarity.Rare      => "Rare",
                ItemRarity.Epic      => "Epic",
                ItemRarity.Legendary => "Legendary",
                ItemRarity.Cursed    => "Cursed",
                _                    => rarity.ToString()
            };
        }

        public static string GetColorHex(ItemRarity rarity)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(GetColor(rarity));
        }

        /// <summary>
        /// Determine whether the item should levitate / float in the air.
        /// Only Epic or higher rarity items (Epic, Legendary) remain suspended in the air.
        /// The others (Common, Uncommon, Rare) fall freely under gravity and physics as usual.
        /// </summary>
        public static bool ShouldFloatInAir(ItemRarity rarity)
        {
            return rarity >= ItemRarity.Epic;
        }

        /// <summary>
        /// Return hover height above the ground surface.
        /// </summary>
        public static float GetHoverHeight(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Epic      => 0.80f,
                ItemRarity.Legendary => 0.95f,
                ItemRarity.Cursed    => 0.85f,
                _                    => 0f
            };
        }
    }
}
