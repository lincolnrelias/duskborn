using System;
using System.Collections.Generic;
using InventorySystem.Core;
using InventorySystem.Data;
using InventorySystem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.Gameplay.ActionBar
{
    public sealed class ActionBarInstaller : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Canvas                        canvas;
        [SerializeField] private RectTransform                 actionBarRoot;
        [SerializeField] private InventoryGridLayoutController gridController;
        [SerializeField] private Image                         dragIcon;

        [Header("Highlight")]
        [SerializeField] private Color selectedSlotColor = Color.white;
        [SerializeField] private Color normalSlotColor   = new(0.4f, 0.4f, 0.4f, 1f);

        [Header("Starting Items")]
        [SerializeField] private ItemDefinitionBase[] startingItems;

        private ActionBarService         _service;
        private InventoryPresenter       _presenter;
        private InventoryDragController  _dragController;
        private readonly Dictionary<string, Texture2D> _iconByItemId = new();

        public ActionBarService                   Service    => _service;
        public IReadOnlyList<InventorySlotView>   SlotViews  => _presenter?.SlotElements;

        public event Action<IInventoryItem, int> OnItemDroppedOutside;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            gridController.ApplyLayout();

            _service = new ActionBarService(gridController.SlotCount);

            CacheStartupItemIcons();

            _presenter = new InventoryPresenter(
                _service.Service,
                actionBarRoot,
                BuildViewModel,
                ResolveIconTexture);

            _dragController = new InventoryDragController(
                _service.Service,
                _presenter,
                canvas,
                dragIcon,
                gridController.HoldThresholdSeconds,
                actionBarRoot,
                (item, srcSlot) => OnItemDroppedOutside?.Invoke(item, srcSlot));

            _service.SelectedSlotChanged += RefreshSelectionHighlight;
            RefreshSelectionHighlight(_service.SelectedIndex);

            AddStartingItems();
        }

        private void Update()
        {
            _dragController?.Tick();
        }

        private void OnDestroy()
        {
            if (_service != null)
                _service.SelectedSlotChanged -= RefreshSelectionHighlight;
            _dragController?.Dispose();
            _presenter?.Dispose();
        }

        public void RegisterIcon(string id, Texture2D icon)
        {
            if (!string.IsNullOrWhiteSpace(id) && icon != null)
                _iconByItemId[id] = icon;
        }

        private void RefreshSelectionHighlight(int selectedIndex)
        {
            if (_presenter == null) return;
            var slots = _presenter.SlotElements;
            for (int i = 0; i < slots.Count; i++)
                slots[i].SetBorderColor(i == selectedIndex ? selectedSlotColor : normalSlotColor);
        }

        private void AddStartingItems()
        {
            if (startingItems == null) return;
            foreach (var def in startingItems)
            {
                if (def == null) continue;
                var item = def.CreateRuntimeItem();
                if (!_service.Service.TryAddItem(item, out _))
                    DuskLog.Warn(LogChannel.ActionBar, $"Could not add starting item '{def.name}' to action bar (no free slot).");
            }
        }

        private void CacheStartupItemIcons()
        {
            if (startingItems == null) return;
            foreach (var def in startingItems)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.Id)) continue;
                if (def.Icon != null)
                    _iconByItemId[def.Id] = def.Icon;
            }
        }

        private Texture2D ResolveIconTexture(IInventoryItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return null;
            return _iconByItemId.TryGetValue(item.Id, out var tex) ? tex : null;
        }

        private static ItemViewModel BuildViewModel(IInventoryItem item)
        {
            int stack = item is IStackable s ? s.StackSize : 0;
            return new ItemViewModel(item.DisplayName, item.Description, item.IconId, stack);
        }

        private bool ValidateReferences()
        {
            if (canvas == null || actionBarRoot == null || dragIcon == null || gridController == null)
            {
                Debug.LogError("ActionBarInstaller: assign all required UI references in the Inspector.");
                return false;
            }
            return true;
        }
    }
}
