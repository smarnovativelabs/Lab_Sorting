using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace LabSorting.UI
{
    // Also attached to purchase buttons to forward drags that begin over a button.
    public sealed class StoreScrollGuard:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IInitializePotentialDragHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public StoreScrollGuard owner;
        public ScrollRect scroll;
        StoreScrollGuard Root=>owner?owner:this;
        bool dragging;float suppressUntil;int pointer=int.MinValue;Vector2 pressed;
        public bool Blocked=>Root.dragging||Time.unscaledTime<Root.suppressUntil||(Root.scroll&&Mathf.Abs(Root.scroll.velocity.x)>20);
        public void OnPointerDown(PointerEventData e)
        {
            var r=Root;
            if(r.pointer!=int.MinValue&&r.pointer!=e.pointerId){r.suppressUntil=Time.unscaledTime+.3f;return;}
            r.pointer=e.pointerId;r.pressed=e.position;
            if(r.scroll&&Mathf.Abs(r.scroll.velocity.x)>20){r.suppressUntil=Time.unscaledTime+.25f;r.scroll.StopMovement();}
        }
        public void OnPointerUp(PointerEventData e){var r=Root;if(r.pointer!=e.pointerId)return;float threshold=EventSystem.current?EventSystem.current.pixelDragThreshold:10;if((e.position-r.pressed).sqrMagnitude>threshold*threshold||r.dragging)r.suppressUntil=Time.unscaledTime+.25f;r.pointer=int.MinValue;}
        public void OnInitializePotentialDrag(PointerEventData e){if(owner&&Root.scroll)Root.scroll.OnInitializePotentialDrag(e);}
        public void OnBeginDrag(PointerEventData e){Root.dragging=true;if(owner&&Root.scroll)Root.scroll.OnBeginDrag(e);}
        public void OnDrag(PointerEventData e){Root.dragging=true;if(owner&&Root.scroll)Root.scroll.OnDrag(e);}
        public void OnEndDrag(PointerEventData e){Root.dragging=false;Root.suppressUntil=Time.unscaledTime+.25f;Root.pointer=int.MinValue;if(owner&&Root.scroll)Root.scroll.OnEndDrag(e);}
        void OnDisable(){if(owner)return;dragging=false;pointer=int.MinValue;suppressUntil=0;if(scroll)scroll.StopMovement();}
    }
}
