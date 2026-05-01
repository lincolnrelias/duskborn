using System;
using System.Collections.Generic;

namespace InventorySystem.Core
{
    /// <summary>
    /// Read-only inventory contract for host-game integration.
    /// </summary>
    public interface IInventoryReader
    {
        InventoryGrid Grid { get; }
        IInventoryItem GetItem(int index);
        IReadOnlyList<InventorySlot> GetSlots();
        bool CanPlaceAt(int index, IInventoryItem item);
    }

    /// <summary>
    /// Mutation contract for inventory operations.
    /// </summary>
    public interface IInventoryWriter
    {
        bool TryAddItem(IInventoryItem item, out int placedIndex);
        bool TryMoveItem(int fromIndex, int toIndex);
        bool TrySwapItems(int firstIndex, int secondIndex);
        bool RemoveItem(int index);
    }

    /// <summary>
    /// Full inventory contract with data access, commands, and events.
    /// </summary>
    public interface IInventory : IInventoryReader, IInventoryWriter
    {
        event Action<ItemAddedEvent> ItemAdded;
        event Action<ItemMovedEvent> ItemMoved;
        event Action<ItemsSwappedEvent> ItemsSwapped;
        event Action<ItemRemovedEvent> ItemRemoved;
        event Action InventoryChanged;
    }
}
