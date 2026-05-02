using UnityEngine;
using UnityEngine.EventSystems;

namespace Duskborn.UI
{
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public class DraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [Tooltip("The RectTransform of the panel that should move (the inventory frame root).")]
        [SerializeField] private RectTransform panel;

        private Canvas  _canvas;
        private Vector2 _dragOffset;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_canvas == null || panel == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 mouseLocal);

            _dragOffset = panel.anchoredPosition - mouseLocal;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_canvas == null || panel == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 mouseLocal);

            panel.anchoredPosition = mouseLocal + _dragOffset;
        }
    }
}
