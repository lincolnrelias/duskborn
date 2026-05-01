namespace InventorySystem.Core
{
    public interface IItemValidationRule
    {
        bool CanPlace(int slotIndex, IInventoryItem item, InventoryService inventoryService);
    }

    public interface IItemTooltipFormatter
    {
        string BuildTooltip(IInventoryItem item);
    }

    public interface IItemIconResolver
    {
        string ResolveIconPath(IInventoryItem item);
    }
}
