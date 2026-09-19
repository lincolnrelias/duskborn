using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    /// <summary>
    /// Utilitário central para o sistema de tiers/raridades (Comum -> Incomum -> Raro -> Épico -> Lendário).
    /// Centraliza paleta de cores, intensidades de luz, dimensões de feixes de luz verticais (estilo Diablo)
    /// e nomenclatura localizada.
    /// </summary>
    public static class ItemTierHelper
    {
        // Cores vibrantes ajustadas para o estilo low-poly/stylized do Duskborn com alto contraste visual
        public static readonly Color ColorCommon    = new Color(0.88f, 0.90f, 0.94f, 1f); // Branco / Prata suave
        public static readonly Color ColorUncommon  = new Color(0.18f, 0.88f, 0.35f, 1f); // Verde Esmeralda vibrante
        public static readonly Color ColorRare      = new Color(0.22f, 0.58f, 1.00f, 1f); // Azul Safira cristalino
        public static readonly Color ColorEpic      = new Color(0.72f, 0.28f, 1.00f, 1f); // Roxo Arcano profundo
        public static readonly Color ColorLegendary = new Color(1.00f, 0.62f, 0.08f, 1f); // Laranja Solar / Ouro radiante
        public static readonly Color ColorCursed    = new Color(0.95f, 0.22f, 0.22f, 1f); // Vermelho Carmesim

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
            // Luz suave ("dim light") para criar destaque sutil no chão sem ofuscar o ambiente
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
            // Altura do pilar vertical projetando-se aos céus (estilo Diablo / ARPG skyward beams)
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
            // Largura esguia e estilizada para formar um pilar de luz nítido e elegante em direção ao céu
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
            // Disco no solo compacto e discreto para ancorar a presença do item sem inundar o chão
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
                ItemRarity.Common    => "Comum",
                ItemRarity.Uncommon  => "Incomum",
                ItemRarity.Rare      => "Raro",
                ItemRarity.Epic      => "Épico",
                ItemRarity.Legendary => "Lendário",
                ItemRarity.Cursed    => "Amaldiçoado",
                _                    => rarity.ToString()
            };
        }

        public static string GetColorHex(ItemRarity rarity)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(GetColor(rarity));
        }

        /// <summary>
        /// Determina se o item deve levitar/flutuar no ar.
        /// Apenas itens de raridade Épica ou superior (Épico, Lendário) permanecem suspensos no ar.
        /// Os demais (Comum, Incomum e Raro) caem livremente no chão sob gravidade e física como de costume.
        /// </summary>
        public static bool ShouldFloatInAir(ItemRarity rarity)
        {
            return rarity >= ItemRarity.Epic;
        }

        /// <summary>
        /// Retorna a altura de flutuação no ar acima da superfície do solo.
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
