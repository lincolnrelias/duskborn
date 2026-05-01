namespace InventorySystem.Core
{
    public sealed class MaterialItem : InventoryItemBase
    {
        public string Category { get; }

        public override InventoryItemKind Kind => InventoryItemKind.Material;

        public MaterialItem(string id, string displayName, string description, string iconId, string category)
            : base(id, displayName, description, iconId)
        {
            Category = category ?? string.Empty;
        }
    }
}
