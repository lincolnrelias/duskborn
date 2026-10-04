using UnityEngine;
using UnityEngine.EventSystems;

namespace Duskborn.UI
{
    public sealed class WorldMapSurface : MonoBehaviour, IScrollHandler, IDragHandler, IBeginDragHandler, IPointerClickHandler
    {
        public WorldMapUI Owner;
        public bool World;
        public void OnScroll(PointerEventData data)
        {
            if (World) Owner.ZoomMap(data.scrollDelta.y > 0);
            else Owner.ZoomMinimap(data.scrollDelta.y > 0);
        }
        public void OnBeginDrag(PointerEventData data) { }
        public void OnDrag(PointerEventData data)
        {
            if (World && data.button == PointerEventData.InputButton.Left) Owner.Pan(data.delta);
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Right) Owner.ClearWaypoint();
            else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) Owner.Mark(data.position, World);
        }
    }
}
