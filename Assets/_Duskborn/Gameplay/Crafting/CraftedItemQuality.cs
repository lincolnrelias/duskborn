namespace Duskborn.Gameplay.Crafting
{
    /// <summary>
    /// Nível de qualidade de um item fabricado.
    /// Modifica os atributos base e pode adicionar propriedades/quirks especiais.
    /// </summary>
    public enum CraftedItemQuality
    {
        /// <summary>Qualidade comum: atributos padrão 1.0x.</summary>
        Padrao = 0,

        /// <summary>Qualidade refinada: +15% aos atributos positivos.</summary>
        Refinado = 1,

        /// <summary>Obra-prima: +30% aos atributos positivos e acabamento perfeito.</summary>
        ObraPrima = 2,

        /// <summary>Instável / Amaldiçoado: +50% ao atributo primário, mas intensifica as penalidades.</summary>
        Instavel = 3
    }

    public static class CraftedQualityExtensions
    {
        public static string GetDisplayName(this CraftedItemQuality quality) => quality switch
        {
            CraftedItemQuality.Padrao    => "Padrão",
            CraftedItemQuality.Refinado  => "Refinado",
            CraftedItemQuality.ObraPrima => "Obra-Prima",
            CraftedItemQuality.Instavel  => "Instável",
            _                            => quality.ToString()
        };

        public static string GetColorHex(this CraftedItemQuality quality) => quality switch
        {
            CraftedItemQuality.Padrao    => "#94a3b8",
            CraftedItemQuality.Refinado  => "#38bdf8",
            CraftedItemQuality.ObraPrima => "#facc15",
            CraftedItemQuality.Instavel  => "#e879f9",
            _                            => "#ffffff"
        };
    }
}
