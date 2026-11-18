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
        [Header("Floating joystick (movement only)")]
        [Tooltip("Wider catch zone for the left thumb. A press there moves the joystick under the thumb; a press on the joystick itself works as a fixed stick.")]
        public RectTransform floatingZone;
        [Tooltip("Joystick opacity while nobody is touching it.")]
        [Range(0,1)] public float idleAlpha=.55f;
        Vector2 touchStart;
        float touchAt,travel;
        int pointer = int.MinValue;
        CanvasGroup group;
        Vector2 rest;
        bool floated;
        public bool Floated=>floated;
        void Awake(){group=GetComponent<CanvasGroup>();ShowIdle();}
        public void OnPointerDown(PointerEventData e)
        {
            if(input.blocked || pointer!=int.MinValue) return;
            pointer=e.pointerId;
            touchStart=e.position;touchAt=Time.unscaledTime;travel=0;
            if(movement&&group&&floatingZone)group.alpha=1;
            if(movement) OnDrag(e);
        }
        /// Press that landed in the floating zone instead of on the stick: bring the stick to the thumb first.
        public void OnZonePointerDown(PointerEventData e)
        {
            if(!movement||!floatingZone||!input||input.blocked||pointer!=int.MinValue)return;
            MoveUnder(e);
            OnPointerDown(e);
        }
        void MoveUnder(PointerEventData e)
        {
            var rect=(RectTransform)transform;var parent=rect.parent as RectTransform;
            if(!parent||!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out var point))return;
            // Keep the whole stick inside the zone so it never slides under the screen edge.
            var corners=new Vector3[4];floatingZone.GetWorldCorners(corners);
            Vector2 min=parent.InverseTransformPoint(corners[0]),max=parent.InverseTransformPoint(corners[2]);
            var half=Vector2.Scale(rect.rect.size,(Vector2)rect.localScale)*.5f;
            point.x=Mathf.Clamp(point.x,min.x+half.x,Mathf.Max(min.x+half.x,max.x-half.x));
            point.y=Mathf.Clamp(point.y,min.y+half.y,Mathf.Max(min.y+half.y,max.y-half.y));
            if(!floated){rest=rect.anchoredPosition;floated=true;}
            var pivotOffset=Vector2.Scale(new Vector2(.5f,.5f)-rect.pivot,Vector2.Scale(rect.rect.size,(Vector2)rect.localScale));
            rect.localPosition=new Vector3(point.x-pivotOffset.x,point.y-pivotOffset.y,rect.localPosition.z);
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
        void ResetGesture()
        {
            pointer=int.MinValue; if(input) { if(movement) input.SetMove(Vector2.zero); } if(knob) knob.anchoredPosition=Vector2.zero;
            if(floated){((RectTransform)transform).anchoredPosition=rest;floated=false;}
            ShowIdle();
        }
        void ShowIdle(){if(movement&&group&&floatingZone)group.alpha=idleAlpha;}
        void OnDisable() { ResetGesture(); }
        void LateUpdate() { if(input && input.blocked && pointer!=int.MinValue) ResetGesture(); }
        void OnApplicationFocus(bool focus) { if(!focus) ResetGesture(); }
        void OnApplicationPause(bool pause) { if(pause) ResetGesture(); }
    }
}
