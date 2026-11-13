using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace LabSorting.UI
{
    public sealed class InventoryAutoScroll:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IBeginDragHandler,IEndDragHandler,IScrollHandler
    {
        public ScrollRect scroll;
        public InventoryAutoScroll owner;
        bool held,moving;float manualUntil,elapsed,from,to;
        RectTransform pending;
        readonly Vector3[] corners=new Vector3[4];
        InventoryAutoScroll Root=>owner?owner:this;
        public bool ManualBusy=>held||Time.unscaledTime<manualUntil||(!moving&&scroll&&scroll.velocity.sqrMagnitude>100);
        public bool IsAnimating=>moving||pending;
        public void OnPointerDown(PointerEventData e){Root.held=true;Root.Cancel();}
        public void OnPointerUp(PointerEventData e){Root.held=false;Root.manualUntil=Time.unscaledTime+.8f;Root.Cancel();}
        public void OnBeginDrag(PointerEventData e){OnPointerDown(e);}
        public void OnEndDrag(PointerEventData e){OnPointerUp(e);}
        public void OnScroll(PointerEventData e){Root.manualUntil=Time.unscaledTime+.8f;Root.Cancel();}
        public void Cancel(){pending=null;moving=false;}
        public void Reveal(RectTransform row){if(!ManualBusy)pending=row;}
        void OnDisable(){held=false;Cancel();}
        void OnApplicationFocus(bool focused){if(!focused){held=false;manualUntil=Time.unscaledTime+.8f;Cancel();}}
        void OnApplicationPause(bool paused){if(paused)OnApplicationFocus(false);}
        void LateUpdate()
        {
            if(owner||!scroll)return;
            if(ManualBusy){Cancel();return;}
            if(pending)
            {
                Canvas.ForceUpdateCanvases();var viewport=scroll.viewport;pending.GetWorldCorners(corners);
                float bottom=viewport.InverseTransformPoint(corners[0]).y,top=viewport.InverseTransformPoint(corners[2]).y;
                float delta=bottom<viewport.rect.yMin+4?viewport.rect.yMin+4-bottom:top>viewport.rect.yMax-4?viewport.rect.yMax-4-top:0;
                pending=null;float overflow=scroll.content.rect.height-viewport.rect.height;
                if(overflow<=0||Mathf.Abs(delta)<.5f)return;
                scroll.StopMovement();from=scroll.verticalNormalizedPosition;to=Mathf.Clamp01(from-delta/overflow);elapsed=0;moving=true;
            }
            if(!moving)return;
            elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/.24f);
            scroll.verticalNormalizedPosition=Mathf.Lerp(from,to,t*t*(3-2*t));
            if(t>=1)moving=false;
        }
    }
}
