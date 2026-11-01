using UnityEngine;
using UnityEngine.EventSystems;

namespace XiuXian.RealmMap
{
    /// <summary>EventSystem pointer IDs support pinch with either Unity input module.</summary>
    public sealed class RealmMapGestureSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public RealmMapPanel owner;
        public void OnPointerDown(PointerEventData e) { if (owner != null) owner.PointerDown(e); }
        public void OnPointerUp(PointerEventData e) { if (owner != null) owner.PointerUp(e); }
        public void OnBeginDrag(PointerEventData e) { if (owner != null) owner.BeginDrag(e); }
        public void OnDrag(PointerEventData e) { if (owner != null) owner.Drag(e); }
        public void OnEndDrag(PointerEventData e) { if (owner != null) owner.PointerUp(e); }
        public void OnScroll(PointerEventData e) { if (owner != null) owner.Scroll(e); }
    }
}
