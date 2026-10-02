namespace Duskborn.Gameplay.Crafting
{
    /// <summary>
    /// Crafting workstation types in Duskborn.
    /// Each station has its own specialty and dedicated recipes.
    /// </summary>
    public enum CraftingStationType
    {
        /// <summary>Starting workbench: basic tools, fiber armor, wood, and tanning.</summary>
        Workbench = 0,

        /// <summary>Smelting forge: iron bars, heavy weapons, plate armor, and alloys.</summary>
        Forge = 1,

        /// <summary>Alchemical cauldron: tonics, elixirs, bombs, elemental oils, and traps.</summary>
        Cauldron = 2,

        /// <summary>Arcane table: cutting arcane crystals, jewelry, staves, and Thornheart relics.</summary>
        ArcaneTable = 3
    }
}
