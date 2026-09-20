namespace Duskborn.Gameplay.Crafting
{
    /// <summary>
    /// Tipos de estações de trabalho de fabricação no Duskborn.
    /// Cada estação possui sua própria especialidade e receitas dedicadas.
    /// </summary>
    public enum CraftingStationType
    {
        /// <summary>Bancada inicial: ferramentas básicas, armaduras de fibra, madeira e curtição.</summary>
        Bancada = 0,

        /// <summary>Forja de fundição: barras de ferro, armas pesadas, armaduras de placas e ligas.</summary>
        Forja = 1,

        /// <summary>Caldeirão alquímico: tônicos, elixires, bombas, óleos elementares e armadilhas.</summary>
        Caldeirao = 2,

        /// <summary>Mesa arcana: lapidação de cristais arcanos, jóias, cajados e relíquias do Espinheiro.</summary>
        MesaArcana = 3
    }
}
