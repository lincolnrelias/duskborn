using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Duskborn.UI
{
    /// <summary>
    /// Component allowing interface windows and panels (such as Inventory and Crafting) to move.
    /// Supports dragging by the header handle or panel frame, with optional screen clamping
    /// and raising to the top of the visual hierarchy on click / drag.
    /// </summary>
    public class DraggablePanel : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("The RectTransform of the panel that should actually move.")]
        [SerializeField] private RectTransform panel;

        [Tooltip("When true, prevent the window from leaving visible screen bounds.")]
        [SerializeField] private bool clampToScreen = true;

        [Tooltip("When true, bring the panel in front of other windows on click / drag.")]
        [SerializeField] private bool bringToFrontOnPointerDown = true;

        private Canvas _canvas;
        private Vector2 _startMouseLocal;
        private Vector2 _startPanelPos;
        private bool _isDragging;

        public RectTransform TargetPanel
        {
            get
            {
                if (panel == null)
                    panel = GetComponent<RectTransform>();
                return panel;
            }
            set => panel = value;
        }

        public bool ClampToScreen
        {
            get => clampToScreen;
            set => clampToScreen = value;
        }

        public bool BringToFrontOnPointerDown
        {
            get => bringToFrontOnPointerDown;
            set => bringToFrontOnPointerDown = value;
        }

        private void Awake()
        {
            EnsureCanvas();
            EnsureGraphicRaycaster();
        }

        public Canvas EnsureCanvas()
        {
            if (_canvas != null && _canvas.isActiveAndEnabled) return _canvas;

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null && TargetPanel != null)
                _canvas = TargetPanel.GetComponentInParent<Canvas>();
            if (_canvas == null)
                _canvas = FindFirstObjectByType<Canvas>();

            return _canvas;
        }

        private void EnsureGraphicRaycaster()
        {
            // Ensure the GameObject has an active graphic component to receive pointer events.
            var graphic = GetComponent<Graphic>();
            if (graphic == null)
            {
                var img = gameObject.AddComponent<Image>();
                img.color = Color.clear;
                img.raycastTarget = true;
            }
            else
            {
                graphic.raycastTarget = true;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (bringToFrontOnPointerDown && TargetPanel != null)
            {
                TargetPanel.SetAsLastSibling();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (TargetPanel == null) return;

            var canvas = EnsureCanvas();
            if (canvas == null) return;

            var parentRect = TargetPanel.parent as RectTransform;
            if (parentRect == null) return;

            if (bringToFrontOnPointerDown)
                TargetPanel.SetAsLastSibling();

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 mouseLocal))
            {
                _isDragging = true;
                _startMouseLocal = mouseLocal;
                _startPanelPos = TargetPanel.anchoredPosition;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (TargetPanel == null) return;

            var parentRect = TargetPanel.parent as RectTransform;
            if (parentRect == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 mouseLocal))
            {
                Vector2 delta = mouseLocal - _startMouseLocal;
                Vector2 targetPos = _startPanelPos + delta;

                if (clampToScreen)
                {
                    targetPos = ClampWithinParent(targetPos, TargetPanel, parentRect);
                }

                TargetPanel.anchoredPosition = targetPos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }

        public static Vector2 ClampWithinParent(Vector2 proposedAnchoredPos, RectTransform target, RectTransform parent)
        {
            if (target == null || parent == null) return proposedAnchoredPos;

            Rect parentRect = parent.rect;
            float targetW = target.rect.width;
            float targetH = target.rect.height;

            // Anchor center in parent local space.
            Vector2 anchorCenter = new Vector2(
                Mathf.Lerp(parentRect.xMin, parentRect.xMax, (target.anchorMin.x + target.anchorMax.x) * 0.5f),
                Mathf.Lerp(parentRect.yMin, parentRect.yMax, (target.anchorMin.y + target.anchorMax.y) * 0.5f)
            );

            Vector2 localPivotPos = anchorCenter + proposedAnchoredPos;

            float minX = localPivotPos.x - targetW * target.pivot.x;
            float maxX = localPivotPos.x + targetW * (1f - target.pivot.x);
            float minY = localPivotPos.y - targetH * target.pivot.y;
            float maxY = localPivotPos.y + targetH * (1f - target.pivot.y);

            // Keep the window onscreen if it fits.
            if (targetW <= parentRect.width)
            {
                if (minX < parentRect.xMin) localPivotPos.x += (parentRect.xMin - minX);
                if (maxX > parentRect.xMax) localPivotPos.x -= (maxX - parentRect.xMax);
            }

            if (targetH <= parentRect.height)
            {
                if (minY < parentRect.yMin) localPivotPos.y += (parentRect.yMin - minY);
                if (maxY > parentRect.yMax) localPivotPos.y -= (maxY - parentRect.yMax);
            }

            return localPivotPos - anchorCenter;
        }
    }
}
