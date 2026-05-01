using System;
using System.Collections.Generic;
using InventorySystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem.UI
{
    public sealed class InventoryPresenter : IDisposable
    {
        private readonly InventoryService _service;
        private readonly RectTransform _gridRoot;
        private readonly Func<IInventoryItem, ItemViewModel> _viewModelBuilder;
        private readonly Func<IInventoryItem, Texture2D> _iconResolver;
        private readonly List<InventorySlotView> _slotElements = new();

        public IReadOnlyList<InventorySlotView> SlotElements => _slotElements;

        public InventoryPresenter(
            InventoryService service,
            RectTransform gridRoot,
            Func<IInventoryItem, ItemViewModel> viewModelBuilder,
            Func<IInventoryItem, Texture2D> iconResolver = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _gridRoot = gridRoot ?? throw new ArgumentNullException(nameof(gridRoot));
            _viewModelBuilder = viewModelBuilder ?? throw new ArgumentNullException(nameof(viewModelBuilder));
            _iconResolver = iconResolver;
            BuildSlots();
            Subscribe();
            RefreshAll();
        }

        public void Dispose()
        {
            _service.InventoryChanged -= HandleInventoryChanged;
        }

        public void RefreshAll()
        {
            for (var i = 0; i < _slotElements.Count; i++)
            {
                RefreshSlot(i);
            }
        }

        private void BuildSlots()
        {
            _slotElements.Clear();
            for (var i = 0; i < _gridRoot.childCount; i++)
            {
                var child = _gridRoot.GetChild(i);
                if (!child.TryGetComponent<InventorySlotView>(out var slot))
                {
                    continue;
                }

                _slotElements.Add(slot);
            }

            if (_slotElements.Count != _service.Grid.SlotCount)
            {
                throw new InvalidOperationException(
                    $"InventoryPresenter: expected {_service.Grid.SlotCount} slots in grid but found {_slotElements.Count}. " +
                    "Populate slots in the grid before entering play mode.");
            }

            for (var i = 0; i < _slotElements.Count; i++)
            {
                var slot = _slotElements[i];
                slot.Initialize(i);
            }
        }

        private void RefreshSlot(int slotIndex)
        {
            var slot = _slotElements[slotIndex];
            slot.ClearSlotVisual();

            var item = _service.GetItem(slotIndex);
            if (item == null)
            {
                return;
            }

            var vm = _viewModelBuilder(item);
            var hasIconTexture = false;
            var resolvedTexture = _iconResolver?.Invoke(item);
            if (resolvedTexture != null)
            {
                hasIconTexture = true;
            }
            else if (!string.IsNullOrWhiteSpace(vm.IconPath))
            {
                var texture = Resources.Load<Texture2D>(vm.IconPath);
                if (texture == null)
                {
                    texture = Resources.Load<Texture2D>(item.IconId);
                }

                if (texture != null)
                {
                    resolvedTexture = texture;
                    hasIconTexture = true;
                }
            }

            slot.SetIcon(hasIconTexture ? resolvedTexture : null, GetFallbackToken(item));
        }

        private static string GetFallbackToken(IInventoryItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.DisplayName))
            {
                return item.DisplayName.Substring(0, 1).ToUpperInvariant();
            }

            return item.Kind == InventoryItemKind.Equipment ? "E" : "M";
        }

        private void Subscribe()
        {
            _service.InventoryChanged += HandleInventoryChanged;
        }

        private void HandleInventoryChanged()
        {
            RefreshAll();
        }
    }
}
