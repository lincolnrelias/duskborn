using System;

namespace InventorySystem.Core
{
    public sealed class InventorySlot
    {
        public int Index { get; }
        public IInventoryItem Item { get; private set; }
        public bool IsEmpty => Item == null;

        public InventorySlot(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Index = index;
        }

        public void SetItem(IInventoryItem item)
        {
            Item = item;
        }

        public void Clear()
        {
            Item = null;
        }
    }
}
