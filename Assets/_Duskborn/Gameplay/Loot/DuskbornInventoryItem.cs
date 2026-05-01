using InventorySystem.Core;

namespace Duskborn.Gameplay.Loot
{
    public sealed class DuskbornInventoryItem : InventoryItemBase
    {
        public override InventoryItemKind Kind => InventoryItemKind.Equipment;
        public ItemDefinition Source { get; }

        public DuskbornInventoryItem(ItemDefinition def)
            : base(
                id: def.name,
                displayName: def.ItemName,
                description: BuildDescription(def),
                iconId: string.Empty)
        {
            Source = def;
        }

        private static string BuildDescription(ItemDefinition def)
        {
            var sign = def.EffectValue >= 0 ? "+" : "";
            return $"[{def.Rarity}] {def.EffectType}: {sign}{def.EffectValue * 100:F0}%";
        }
    }
}
