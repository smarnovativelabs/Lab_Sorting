using UnityEngine;
using UnityEngine.EventSystems;

namespace LabSorting.Player
{
    public sealed class LabTouchArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        public LabPlayerInput input;
        public bool movement;
        public RectTransform knob;
        public LabSorting.Sorting.SortingSession session;
        Vector2 touchStart;
        float touchAt,travel;
        int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if(input.blocked || pointer!=int.MinValue) return;
            pointer=e.pointerId;
            touchStart=e.position;touchAt=Time.unscaledTime;travel=0;
            if(movement) OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if(e.pointerId!=pointer) return;
            if(input.blocked) { ResetGesture(); return; }
            if(!movement) { travel+=e.delta.magnitude;input.AddLook(e.delta*(720f/Mathf.Max(1,Screen.height))); return; }
            var rect=(RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var point);
            float radius=rect.rect.width*.35f;
            var v=Vector2.ClampMagnitude(point/radius,1);
            v=v.magnitude<.12f?Vector2.zero:v.normalized*Mathf.InverseLerp(.12f,1,v.magnitude);
            input.SetMove(v);
            if(knob) knob.anchoredPosition=v*radius;
        }
        public void OnPointerUp(PointerEventData e) { if(e.pointerId!=pointer)return;if(!movement&&session&&!input.blocked&&Time.unscaledTime-touchAt<.28f&&travel<Screen.height*.018f&&Vector2.Distance(touchStart,e.position)<Screen.height*.018f)session.TryTouchPick(e.position);ResetGesture(); }
        public void OnEndDrag(PointerEventData e) { OnPointerUp(e); }
        void ResetGesture() { pointer=int.MinValue; if(input) { if(movement) input.SetMove(Vector2.zero); } if(knob) knob.anchoredPosition=Vector2.zero; }
        void OnDisable() { ResetGesture(); }
        void LateUpdate() { if(input && input.blocked && pointer!=int.MinValue) ResetGesture(); }
        void OnApplicationFocus(bool focus) { if(!focus) ResetGesture(); }
        void OnApplicationPause(bool pause) { if(pause) ResetGesture(); }
    }
}
