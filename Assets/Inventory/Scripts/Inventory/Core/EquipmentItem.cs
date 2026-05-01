namespace InventorySystem.Core
{
    public sealed class EquipmentItem : InventoryItemBase
    {
        public int PowerScore { get; }

        public override InventoryItemKind Kind => InventoryItemKind.Equipment;

        public EquipmentItem(string id, string displayName, string description, string iconId, int powerScore)
            : base(id, displayName, description, iconId)
        {
            PowerScore = powerScore;
        }
    }
}
