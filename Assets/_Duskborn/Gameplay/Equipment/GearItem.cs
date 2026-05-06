using System.Collections.Generic;
using InventorySystem.Core;

namespace Duskborn.Gameplay.Equipment
{
    public sealed class GearItem : InventoryItemBase
    {
        public EquipmentSlot            Slot    { get; }
        public IReadOnlyList<StatBonus> Bonuses { get; }

        public override InventoryItemKind Kind => InventoryItemKind.Equipment;

        public GearItem(string id, string displayName, string description,
                        string iconId, EquipmentSlot slot, IReadOnlyList<StatBonus> bonuses)
            : base(id, displayName, description, iconId)
        {
            Slot    = slot;
            Bonuses = bonuses ?? System.Array.Empty<StatBonus>();
        }
    }
}
