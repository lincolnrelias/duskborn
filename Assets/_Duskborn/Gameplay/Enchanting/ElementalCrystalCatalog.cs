using UnityEngine;
using InventorySystem.Data;
using InventorySystem.Core;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.Enchanting
{
    public static class ElementalCrystalCatalog
    {
        public static string Element(RuneKind kind) => kind switch
        {
            RuneKind.Flame => "flame", RuneKind.Venom => "nature",
            RuneKind.Storm => "storm", RuneKind.Stone => "earth",
            RuneKind.Frost => "frost", RuneKind.Blood => "blood",
            RuneKind.Hex => "dark", RuneKind.Radiance => "light", _ => ""
        };
        public static string Id(RuneKind kind) => Element(kind).Length == 0 ? "" : "material_" + Element(kind) + "_crystal";
        public static int CraftingCost(int level) => level >= 1 && level <= 3 ? level * 3 : 0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterIcons()
        {
            foreach (var crystal in Resources.LoadAll<MaterialDefinition>("ElementalCrystals/Items"))
                if (crystal.Icon != null) ItemIconRegistry.Register(crystal.Id, crystal.Icon);
        }
    }
}
