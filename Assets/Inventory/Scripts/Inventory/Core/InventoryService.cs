using System;
using System.Collections.Generic;

namespace InventorySystem.Core
{
    public sealed class InventoryService : IInventory
    {
        private readonly InventoryGrid _grid;
        private readonly InventorySlot[] _slots;
        private readonly IReadOnlyList<IItemValidationRule> _rules;

        public event Action<ItemAddedEvent> ItemAdded;
        public event Action<ItemMovedEvent> ItemMoved;
        public event Action<ItemsSwappedEvent> ItemsSwapped;
        public event Action<ItemRemovedEvent> ItemRemoved;
        public event Action InventoryChanged;

        public InventoryGrid Grid => _grid;

        public InventoryService(InventoryGrid grid, IReadOnlyList<IItemValidationRule> rules = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _rules = rules ?? Array.Empty<IItemValidationRule>();

            _slots = new InventorySlot[_grid.SlotCount];
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new InventorySlot(i);
            }
        }

        public bool TryAddItem(IInventoryItem item, out int placedIndex)
        {
            placedIndex = -1;
            if (item == null)
            {
                return false;
            }

            for (var y = 0; y < _grid.Rows; y++)
            {
                for (var x = 0; x < _grid.Columns; x++)
                {
                    var index = _grid.ToIndex(x, y);
                    if (!_slots[index].IsEmpty || !CanPlaceAt(index, item))
                    {
                        continue;
                    }

                    _slots[index].SetItem(item);
                    placedIndex = index;
                    ItemAdded?.Invoke(new ItemAddedEvent(index, item));
                    InventoryChanged?.Invoke();
                    return true;
                }
            }

            return false;
        }

        public bool TryPlaceItemAt(int index, IInventoryItem item)
        {
            if (!IsValidIndex(index) || item == null || !_slots[index].IsEmpty || !CanPlaceAt(index, item))
                return false;

            _slots[index].SetItem(item);
            ItemAdded?.Invoke(new ItemAddedEvent(index, item));
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryMoveItem(int fromIndex, int toIndex)
        {
            if (!IsValidOccupiedSource(fromIndex) || !IsValidIndex(toIndex) || fromIndex == toIndex)
            {
                return false;
            }

            var sourceItem = _slots[fromIndex].Item;
            if (!_slots[toIndex].IsEmpty || !CanPlaceAt(toIndex, sourceItem))
            {
                return false;
            }

            _slots[toIndex].SetItem(sourceItem);
            _slots[fromIndex].Clear();
            ItemMoved?.Invoke(new ItemMovedEvent(fromIndex, toIndex, sourceItem));
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TrySwapItems(int firstIndex, int secondIndex)
        {
            if (!IsValidOccupiedSource(firstIndex) || !IsValidOccupiedSource(secondIndex) || firstIndex == secondIndex)
            {
                return false;
            }

            var first = _slots[firstIndex].Item;
            var second = _slots[secondIndex].Item;

            if (!CanPlaceAt(firstIndex, second) || !CanPlaceAt(secondIndex, first))
            {
                return false;
            }

            _slots[firstIndex].SetItem(second);
            _slots[secondIndex].SetItem(first);
            ItemsSwapped?.Invoke(new ItemsSwappedEvent(firstIndex, secondIndex, first, second));
            InventoryChanged?.Invoke();
            return true;
        }

        public bool RemoveItem(int index)
        {
            if (!IsValidOccupiedSource(index))
            {
                return false;
            }

            var item = _slots[index].Item;
            _slots[index].Clear();
            ItemRemoved?.Invoke(new ItemRemovedEvent(index, item));
            InventoryChanged?.Invoke();
            return true;
        }

        public IInventoryItem GetItem(int index)
        {
            return IsValidIndex(index) ? _slots[index].Item : null;
        }

        public bool CanPlaceAt(int index, IInventoryItem item)
        {
            if (!IsValidIndex(index) || item == null)
            {
                return false;
            }

            for (var i = 0; i < _rules.Count; i++)
            {
                if (!_rules[i].CanPlace(index, item, this))
                {
                    return false;
                }
            }

            return true;
        }

        public IReadOnlyList<InventorySlot> GetSlots()
        {
            return _slots;
        }

        private bool IsValidIndex(int index)
        {
            return _grid.IsValidIndex(index);
        }

        private bool IsValidOccupiedSource(int index)
        {
            return IsValidIndex(index) && !_slots[index].IsEmpty;
        }
    }
}
