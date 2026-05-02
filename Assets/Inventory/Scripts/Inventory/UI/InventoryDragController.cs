using System;
using InventorySystem.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace InventorySystem.UI
{
    public sealed class InventoryDragController : IDisposable
    {
        private readonly InventoryService _service;
        private readonly InventoryPresenter _presenter;
        private readonly Canvas _canvas;
        private readonly Image _dragIcon;
        private readonly float _holdThreshold;

        private bool _isPressing;
        private bool _isDragging;
        private float _pressStartTime;
        private int _sourceSlotIndex = -1;
        private InventorySlotView _sourceSlot;

        public InventoryDragController(
            InventoryService service,
            InventoryPresenter presenter,
            Canvas canvas,
            Image dragIcon,
            float holdThreshold)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _dragIcon = dragIcon ?? throw new ArgumentNullException(nameof(dragIcon));
            _holdThreshold = Mathf.Max(0f, holdThreshold);
            _dragIcon.raycastTarget = false;
            _dragIcon.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            EndDrag();
        }

        public void Tick()
        {
            if (!TryReadPointer(out var pressedThisFrame, out var releasedThisFrame, out var pointerScreenPos))
            {
                return;
            }

            var pointerPanelPos = ToPanelPosition(pointerScreenPos);

            if (pressedThisFrame && !_isPressing)
            {
                BeginPress(pointerPanelPos);
            }

            if (_isPressing && !_isDragging && Time.unscaledTime - _pressStartTime >= _holdThreshold)
            {
                StartDrag(pointerPanelPos);
            }

            if (_isDragging)
            {
                MoveDragIcon(pointerPanelPos);
            }

            if (releasedThisFrame && _isPressing)
            {
                if (_isDragging)
                {
                    CompleteDrop(pointerPanelPos);
                }

                EndDrag();
            }
        }

        private void BeginPress(Vector2 pointerPanelPos)
        {
            if (!TryGetSlotAt(pointerPanelPos, out var slotIndex))
            {
                return;
            }

            if (_service.GetItem(slotIndex) == null)
            {
                return;
            }

            _isPressing = true;
            _pressStartTime = Time.unscaledTime;
            _sourceSlotIndex = slotIndex;
            _sourceSlot = _presenter.SlotElements[slotIndex];
        }

        private void StartDrag(Vector2 pointerPanelPos)
        {
            if (_isDragging || _sourceSlotIndex < 0)
            {
                return;
            }

            Sprite dragSprite = null;
            if (_sourceSlot != null && _sourceSlot.IconImage != null)
            {
                _sourceSlot.IconImage.color = new Color(1f, 1f, 1f, 0.35f);
                dragSprite = _sourceSlot.DragGhostSprite != null
                    ? _sourceSlot.DragGhostSprite
                    : _sourceSlot.IconImage.sprite;
            }

            _isDragging = true;
            if (dragSprite != null)
            {
                _dragIcon.sprite = dragSprite;
                _dragIcon.color = Color.white;
                _dragIcon.enabled = true;
                _dragIcon.gameObject.SetActive(true);
                _dragIcon.rectTransform.SetAsLastSibling();
            }
            else
            {
                _dragIcon.enabled = false;
                _dragIcon.sprite = null;
                _dragIcon.gameObject.SetActive(false);
            }

            MoveDragIcon(pointerPanelPos);
        }

        private void MoveDragIcon(Vector2 pointerPanelPos)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas.transform as RectTransform,
                    pointerPanelPos,
                    _canvas.worldCamera,
                    out var local))
            {
                return;
            }

            var dragRect = _dragIcon.rectTransform;
            dragRect.anchoredPosition = local;
        }

        private void CompleteDrop(Vector2 pointerPanelPos)
        {
            if (_sourceSlotIndex < 0)
            {
                return;
            }

            if (!TryGetSlotAt(pointerPanelPos, out var targetSlotIndex) || targetSlotIndex == _sourceSlotIndex)
            {
                return;
            }

            var targetItem = _service.GetItem(targetSlotIndex);
            if (targetItem == null)
            {
                _service.TryMoveItem(_sourceSlotIndex, targetSlotIndex);
            }
            else
            {
                _service.TrySwapItems(_sourceSlotIndex, targetSlotIndex);
            }
        }

        private void EndDrag()
        {
            if (_sourceSlot != null && _sourceSlot.IconImage != null)
            {
                _sourceSlot.IconImage.color = Color.white;
            }

            if (_dragIcon != null)
            {
                _dragIcon.enabled = false;
                _dragIcon.sprite = null;
                _dragIcon.gameObject.SetActive(false);
            }
            _sourceSlot = null;
            _sourceSlotIndex = -1;
            _isPressing = false;
            _isDragging = false;
        }

        private bool TryReadPointer(out bool pressedThisFrame, out bool releasedThisFrame, out Vector2 pointerScreenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                var left = Mouse.current.leftButton;
                pressedThisFrame = left.wasPressedThisFrame;
                releasedThisFrame = left.wasReleasedThisFrame;
                pointerScreenPosition = Mouse.current.position.ReadValue();
                return true;
            }
#endif
            pressedThisFrame = Input.GetMouseButtonDown(0);
            releasedThisFrame = Input.GetMouseButtonUp(0);
            pointerScreenPosition = Input.mousePosition;
            return true;
        }

        private Vector2 ToPanelPosition(Vector2 screenPos)
        {
            return screenPos;
        }

        private bool TryGetSlotAt(Vector2 pointerPanelPos, out int slotIndex)
        {
            slotIndex = -1;
            for (var i = 0; i < _presenter.SlotElements.Count; i++)
            {
                var slotRect = _presenter.SlotElements[i].RectTransform;
                if (slotRect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(slotRect, pointerPanelPos, _canvas.worldCamera))
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }
    }
}
