using System;
using InventorySystem.Core;

namespace Duskborn.Gameplay.ActionBar
{
    public sealed class ActionBarService
    {
        public InventoryService Service { get; }
        public int SlotCount { get; }
        public int SelectedIndex { get; private set; }

        public event Action<int> SelectedSlotChanged;

        public ActionBarService(int slotCount = 8)
        {
            SlotCount = slotCount;
            Service   = new InventoryService(new InventoryGrid(slotCount, 1));
        }

        public IInventoryItem GetSelectedItem() => Service.GetItem(SelectedIndex);

        public void SelectSlot(int index)
        {
            index = ((index % SlotCount) + SlotCount) % SlotCount;
            if (index == SelectedIndex) return;
            SelectedIndex = index;
            SelectedSlotChanged?.Invoke(SelectedIndex);
        }

        public void SelectNext()     => SelectSlot(SelectedIndex + 1);
        public void SelectPrevious() => SelectSlot(SelectedIndex - 1);
    }
}
