using System;
using InventorySystem.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace InventorySystem.UI
{
    public sealed class InventoryTooltipController : IDisposable
    {
        public enum TooltipBehaviorMode
        {
            HoverFollowPointer,
            StaticClickSelection
        }

        public enum TooltipAnchor
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private readonly InventoryService _service;
        private readonly InventoryPresenter _presenter;
        private readonly RectTransform _tooltipPanel;
        private readonly TextMeshProUGUI _titleLabel;
        private readonly TextMeshProUGUI _descriptionLabel;
        private readonly Func<IInventoryItem, string> _tooltipBuilder;
        private readonly Vector2 _tooltipOffset;
        private readonly Canvas _canvas;
        private readonly TooltipAnchor _tooltipAnchor;
        private readonly float _tooltipDisplayDelay;
        private readonly ITooltipBehavior _behavior;
        private bool _isTooltipVisible;

        public InventoryTooltipController(
            InventoryService service,
            InventoryPresenter presenter,
            Canvas canvas,
            RectTransform tooltipPanel,
            TextMeshProUGUI titleLabel,
            TextMeshProUGUI descriptionLabel,
            Func<IInventoryItem, string> tooltipBuilder,
            Vector2 tooltipOffset,
            TooltipAnchor tooltipAnchor,
            float tooltipDisplayDelay,
            TooltipBehaviorMode behaviorMode)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _tooltipPanel = tooltipPanel ?? throw new ArgumentNullException(nameof(tooltipPanel));
            _titleLabel = titleLabel ?? throw new ArgumentNullException(nameof(titleLabel));
            _descriptionLabel = descriptionLabel ?? throw new ArgumentNullException(nameof(descriptionLabel));
            _tooltipBuilder = tooltipBuilder ?? throw new ArgumentNullException(nameof(tooltipBuilder));
            _tooltipOffset = tooltipOffset;
            _tooltipAnchor = tooltipAnchor;
            _tooltipDisplayDelay = Mathf.Max(0f, tooltipDisplayDelay);
            if (behaviorMode == TooltipBehaviorMode.HoverFollowPointer)
            {
                ApplyAnchorToTooltipPanel();
            }

            _behavior = CreateBehavior(behaviorMode);

            DisableTooltipRaycastTargets();
            HideTooltip();

            foreach (var slot in _presenter.SlotElements)
            {
                slot.PointerEntered += OnPointerEnterSlot;
                slot.PointerExited += OnPointerLeaveSlot;
                slot.PointerClicked += OnPointerClickSlot;
            }
        }

        public void Tick()
        {
            _behavior.Tick();
        }

        public void Dispose()
        {
            foreach (var slot in _presenter.SlotElements)
            {
                slot.PointerEntered -= OnPointerEnterSlot;
                slot.PointerExited -= OnPointerLeaveSlot;
                slot.PointerClicked -= OnPointerClickSlot;
            }
        }

        private void OnPointerEnterSlot(int slotIndex, PointerEventData evt)
        {
            _behavior.OnPointerEnter(slotIndex, evt);
        }

        private void OnPointerLeaveSlot(int slotIndex, PointerEventData __)
        {
            _behavior.OnPointerExit(slotIndex, __);
        }

        private void OnPointerClickSlot(int slotIndex, PointerEventData __)
        {
            _behavior.OnPointerClick(slotIndex, __);
        }

        private void HideTooltip()
        {
            if (_tooltipPanel.gameObject.activeSelf)
            {
                _tooltipPanel.gameObject.SetActive(false);
            }

            _isTooltipVisible = false;
        }

        private void ShowTooltipForItem(IInventoryItem item)
        {
            _titleLabel.text = item.DisplayName;
            _descriptionLabel.text = _tooltipBuilder(item);
            _tooltipPanel.gameObject.SetActive(true);
            _isTooltipVisible = true;
        }

        private void MoveTooltipToPointer()
        {
            var screenPoint = GetPointerScreenPosition() + _tooltipOffset;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                screenPoint,
                _canvas.worldCamera,
                out var localPoint);
            _tooltipPanel.anchoredPosition = localPoint;
        }

        private IInventoryItem GetItemAtSlot(int slotIndex)
        {
            return _service.GetItem(slotIndex);
        }

        private void DisableTooltipRaycastTargets()
        {
            if (_tooltipPanel.TryGetComponent<Graphic>(out var panelGraphic))
            {
                panelGraphic.raycastTarget = false;
            }

            _titleLabel.raycastTarget = false;
            _descriptionLabel.raycastTarget = false;
        }

        private static Vector2 GetPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            return Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        private void ApplyAnchorToTooltipPanel()
        {
            var pivot = _tooltipAnchor switch
            {
                TooltipAnchor.TopLeft => new Vector2(0f, 1f),
                TooltipAnchor.TopRight => new Vector2(1f, 1f),
                TooltipAnchor.BottomLeft => new Vector2(0f, 0f),
                TooltipAnchor.BottomRight => new Vector2(1f, 0f),
                _ => new Vector2(0f, 1f)
            };

            _tooltipPanel.pivot = pivot;
            // Use canvas center as anchor reference because localPoint is returned in canvas-local space.
            // This makes the selected pivot land exactly on the mouse position.
            _tooltipPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _tooltipPanel.anchorMax = new Vector2(0.5f, 0.5f);
        }

        private ITooltipBehavior CreateBehavior(TooltipBehaviorMode behaviorMode)
        {
            return behaviorMode switch
            {
                TooltipBehaviorMode.HoverFollowPointer => new HoverFollowPointerTooltipBehavior(this),
                TooltipBehaviorMode.StaticClickSelection => new StaticClickSelectionTooltipBehavior(this),
                _ => throw new ArgumentOutOfRangeException(nameof(behaviorMode), behaviorMode, "Unsupported tooltip behavior mode.")
            };
        }

        private interface ITooltipBehavior
        {
            void Tick();
            void OnPointerEnter(int slotIndex, PointerEventData eventData);
            void OnPointerExit(int slotIndex, PointerEventData eventData);
            void OnPointerClick(int slotIndex, PointerEventData eventData);
        }

        private sealed class HoverFollowPointerTooltipBehavior : ITooltipBehavior
        {
            private readonly InventoryTooltipController _owner;
            private int _hoveredSlotIndex = -1;
            private float _hoverStartTime;

            public HoverFollowPointerTooltipBehavior(InventoryTooltipController owner)
            {
                _owner = owner;
            }

            public void Tick()
            {
                if (_hoveredSlotIndex < 0)
                {
                    return;
                }

                var item = _owner.GetItemAtSlot(_hoveredSlotIndex);
                if (item == null)
                {
                    _owner.HideTooltip();
                    return;
                }

                if (!_owner._isTooltipVisible && Time.unscaledTime - _hoverStartTime < _owner._tooltipDisplayDelay)
                {
                    return;
                }

                _owner.ShowTooltipForItem(item);
                _owner.MoveTooltipToPointer();
            }

            public void OnPointerEnter(int slotIndex, PointerEventData eventData)
            {
                if (_hoveredSlotIndex == slotIndex)
                {
                    return;
                }

                _hoveredSlotIndex = slotIndex;
                _hoverStartTime = Time.unscaledTime;
                _owner.HideTooltip();
            }

            public void OnPointerExit(int slotIndex, PointerEventData eventData)
            {
                if (slotIndex != _hoveredSlotIndex)
                {
                    return;
                }

                _hoveredSlotIndex = -1;
                _owner.HideTooltip();
            }

            public void OnPointerClick(int slotIndex, PointerEventData eventData)
            {
            }
        }

        private sealed class StaticClickSelectionTooltipBehavior : ITooltipBehavior
        {
            private readonly InventoryTooltipController _owner;

            public StaticClickSelectionTooltipBehavior(InventoryTooltipController owner)
            {
                _owner = owner;
            }

            public void Tick()
            {
            }

            public void OnPointerEnter(int slotIndex, PointerEventData eventData)
            {
            }

            public void OnPointerExit(int slotIndex, PointerEventData eventData)
            {
            }

            public void OnPointerClick(int slotIndex, PointerEventData eventData)
            {
                var item = _owner.GetItemAtSlot(slotIndex);
                if (item != null)
                {
                    _owner.ShowTooltipForItem(item);
                }
            }
        }
    }
}
