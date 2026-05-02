namespace InventorySystem.Core
{
    public sealed class MaterialItem : InventoryItemBase, IStackable
    {
        public string Category  { get; }
        public int    StackSize { get; }

        public override InventoryItemKind Kind => InventoryItemKind.Material;

        public MaterialItem(string id, string displayName, string description, string iconId, string category, int stackSize = 1)
            : base(id, displayName, description, iconId)
        {
            Category  = category ?? string.Empty;
            StackSize = stackSize;
        }
    }
}
