using System;

namespace InventorySystem.Core
{
    public readonly struct ItemAddedEvent
    {
        public int SlotIndex { get; }
        public IInventoryItem Item { get; }

        public ItemAddedEvent(int slotIndex, IInventoryItem item)
        {
            SlotIndex = slotIndex;
            Item = item;
        }
    }

    public readonly struct ItemMovedEvent
    {
        public int FromIndex { get; }
        public int ToIndex { get; }
        public IInventoryItem Item { get; }

        public ItemMovedEvent(int fromIndex, int toIndex, IInventoryItem item)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            Item = item;
        }
    }

    public readonly struct ItemsSwappedEvent
    {
        public int FirstIndex { get; }
        public int SecondIndex { get; }
        public IInventoryItem FirstItem { get; }
        public IInventoryItem SecondItem { get; }

        public ItemsSwappedEvent(int firstIndex, int secondIndex, IInventoryItem firstItem, IInventoryItem secondItem)
        {
            FirstIndex = firstIndex;
            SecondIndex = secondIndex;
            FirstItem = firstItem;
            SecondItem = secondItem;
        }
    }

    public readonly struct ItemRemovedEvent
    {
        public int SlotIndex { get; }
        public IInventoryItem Item { get; }

        public ItemRemovedEvent(int slotIndex, IInventoryItem item)
        {
            SlotIndex = slotIndex;
            Item = item;
        }
    }
}
