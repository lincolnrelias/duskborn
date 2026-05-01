using System;
using InventorySystem.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace InventorySystem.UI
{
    internal sealed class InventoryInteractionPanelController : IDisposable
    {
        private readonly InventoryService _service;
        private readonly Func<IInventoryItem> _createMaterial;
        private readonly Func<IInventoryItem> _createEquipment;
        private readonly Button _addMaterialButton;
        private readonly Button _addEquipmentButton;
        private readonly Button _removeLastButton;
        private readonly Button _fillRandomButton;
        private readonly Button _clearAllButton;

        public InventoryInteractionPanelController(
            InventoryService service,
            Button addMaterialButton,
            Button addEquipmentButton,
            Button removeLastButton,
            Button fillRandomButton,
            Button clearAllButton,
            Func<IInventoryItem> createMaterial,
            Func<IInventoryItem> createEquipment)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _createMaterial = createMaterial ?? throw new ArgumentNullException(nameof(createMaterial));
            _createEquipment = createEquipment ?? throw new ArgumentNullException(nameof(createEquipment));
            _addMaterialButton = addMaterialButton;
            _addEquipmentButton = addEquipmentButton;
            _removeLastButton = removeLastButton;
            _fillRandomButton = fillRandomButton;
            _clearAllButton = clearAllButton;

            HookButton(_addMaterialButton, AddMaterial);
            HookButton(_addEquipmentButton, AddEquipment);
            HookButton(_removeLastButton, RemoveLast);
            HookButton(_fillRandomButton, FillRandom);
            HookButton(_clearAllButton, ClearAll);
        }

        public void Dispose()
        {
            if (_addMaterialButton != null) _addMaterialButton.onClick.RemoveListener(AddMaterial);
            if (_addEquipmentButton != null) _addEquipmentButton.onClick.RemoveListener(AddEquipment);
            if (_removeLastButton != null) _removeLastButton.onClick.RemoveListener(RemoveLast);
            if (_fillRandomButton != null) _fillRandomButton.onClick.RemoveListener(FillRandom);
            if (_clearAllButton != null) _clearAllButton.onClick.RemoveListener(ClearAll);
        }

        private void AddMaterial()
        {
            _service.TryAddItem(_createMaterial(), out _);
        }

        private void AddEquipment()
        {
            _service.TryAddItem(_createEquipment(), out _);
        }

        private void RemoveLast()
        {
            for (var i = _service.Grid.SlotCount - 1; i >= 0; i--)
            {
                if (_service.GetItem(i) == null)
                {
                    continue;
                }

                _service.RemoveItem(i);
                return;
            }
        }

        private void FillRandom()
        {
            for (var i = 0; i < _service.Grid.SlotCount; i++)
            {
                if (_service.GetItem(i) != null)
                {
                    continue;
                }

                var item = UnityEngine.Random.value > 0.5f ? _createMaterial() : _createEquipment();
                _service.TryAddItem(item, out _);
            }
        }

        private void ClearAll()
        {
            for (var i = _service.Grid.SlotCount - 1; i >= 0; i--)
            {
                if (_service.GetItem(i) != null)
                {
                    _service.RemoveItem(i);
                }
            }
        }

        private static void HookButton(Button button, UnityAction handler)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(handler);
        }
    }
}
