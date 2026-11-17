using UnityEngine;
namespace LabSorting.Player
{
    // Presentation only: inventory, item colliders and saved world poses remain unchanged.
    [DefaultExecutionOrder(200)]
    public sealed class CarryPresentation : MonoBehaviour
    {
        public Camera view;
        Vector3 rest;
        int mask;
        RectTransform selectedCard;
        LabSorting.UI.ContextActionHUD actions;
        readonly Vector3[] uiCorners=new Vector3[4];
        public Rect HeldScreenRect {get;private set;}
        public Rect HeldSafeRect {get;private set;}
        void Start()
        {
            actions=FindFirstObjectByType<LabSorting.UI.ContextActionHUD>();
            foreach(var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(rect.name=="SelectedItemCard"){selectedCard=rect;break;}
        }
        Rect ScreenRect(RectTransform rect)
        {
            rect.GetWorldCorners(uiCorners);var canvas=rect.GetComponentInParent<Canvas>();
            var camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
            foreach(var corner in uiCorners){var p=RectTransformUtility.WorldToScreenPoint(camera,corner);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        Rect ProjectBounds(Bounds bounds)
        {
            Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
            for(int i=0;i<8;i++){var p=view.WorldToScreenPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        void FitHeldItem(LabSorting.Sorting.SortItem item)
        {
            if(!item.visual||item.Moving)return;
            float padding=Screen.height*.018f;
            float left=Screen.width*.52f,right=Screen.width*.82f;
            if(selectedCard)left=Mathf.Max(left,ScreenRect(selectedCard).xMax+padding);
            if(actions)
            {
                if(actions.pick)right=Mathf.Min(right,ScreenRect(actions.pick.transform as RectTransform).xMin-padding);
                if(actions.drop)right=Mathf.Min(right,ScreenRect(actions.drop.transform as RectTransform).xMin-padding);
                if(actions.place)right=Mathf.Min(right,ScreenRect(actions.place.transform as RectTransform).xMin-padding);
            }
            // Use the open space above controls on unusually narrow layouts.
            float bottom=Screen.height*.13f,top=Screen.height*.46f;
            if(right-left<Screen.height*.12f){left=Screen.width*.52f;right=Screen.width*.80f;bottom=Screen.height*.48f;top=Screen.height*.72f;}
            HeldSafeRect=Rect.MinMaxRect(left,bottom,right,top);
            var projected=ProjectBounds(item.visual.bounds);
            float multiplier=item.definition?Mathf.Max(1,item.definition.heldSizeMultiplier):1;
            float height=Screen.height*.23f*multiplier,fill=Mathf.Min(.95f,.86f*multiplier);
            float size=Mathf.Min(HeldSafeRect.width*fill,height);
            float factor=Mathf.Min(size/Mathf.Max(1,projected.width),Mathf.Min(height,HeldSafeRect.height*fill)/Mathf.Max(1,projected.height));
            item.transform.localScale*=Mathf.Clamp(factor,.1f,3f);
            // Center visible geometry, not the imported model's arbitrary pivot.
            var center=view.WorldToScreenPoint(item.visual.bounds.center);
            var destination=new Vector3(HeldSafeRect.center.x,HeldSafeRect.center.y,center.z);
            item.transform.position+=view.ScreenToWorldPoint(destination)-view.ScreenToWorldPoint(center);
            // Scaling and moving an offset model changes its perspective bounds. Refit
            // the projected rectangle, so wide microscopes cannot spill into the HUD.
            for(int pass=0;multiplier>1&&pass<4;pass++)
            {
                var fitted=ProjectBounds(item.visual.bounds);
                float refit=Mathf.Min(size/Mathf.Max(1,fitted.width),Mathf.Min(height,HeldSafeRect.height*fill)/Mathf.Max(1,fitted.height));
                item.transform.localScale*=Mathf.Clamp(refit,.1f,3f);
                fitted=ProjectBounds(item.visual.bounds);
                float depth=view.WorldToScreenPoint(item.visual.bounds.center).z;
                item.transform.position+=view.ScreenToWorldPoint(new Vector3(HeldSafeRect.center.x,HeldSafeRect.center.y,depth))-view.ScreenToWorldPoint(new Vector3(fitted.center.x,fitted.center.y,depth));
            }
            HeldScreenRect=ProjectBounds(item.visual.bounds);
        }
        public float ClearanceScale {get;private set;}=1;
        void Awake(){rest=transform.localPosition;mask=~((1<<2)|(1<<LayerMask.NameToLayer("SortingItem")));}
        void LateUpdate()
        {
            if(!view)return;
            Vector3 desired=transform.parent.TransformPoint(rest),origin=view.transform.position;
            var delta=desired-origin;float distance=delta.magnitude;
            bool blocked=Physics.SphereCast(origin,.20f,delta.normalized,out var hit,distance,mask,QueryTriggerInteraction.Ignore);
            float restDepth=Vector3.Dot(delta,view.transform.forward);
            // Every carried model fits the .18m envelope. Keep its rear edge ahead of
            // the near plane rather than enforcing a fixed minimum distance from walls.
            float minimum=Mathf.Clamp01((view.nearClipPlane+.015f)/Mathf.Max(.001f,restDepth-.18f));
            float target=blocked?Mathf.Clamp((hit.distance-.025f)/Mathf.Max(.001f,distance),minimum,1):1;
            // Smooth a camera-relative distance, never a world position left behind by a turn.
            // Size and camera depth change together, preserving the projected size near walls.
            ClearanceScale=target<ClearanceScale?target:Mathf.Lerp(ClearanceScale,target,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            transform.position=origin+delta*ClearanceScale;
            foreach(Transform child in transform)
            {
                if(!child.gameObject.activeSelf)continue;
                var item=child.GetComponent<LabSorting.Sorting.SortItem>();if(!item||item.location!=LabSorting.Sorting.ItemLocation.Hand)continue;
                if((!item.Moving&&!item.Presenting)||ClearanceScale<1)child.localScale=item.originalScale*item.CarryScale*ClearanceScale;
                if(!item.Moving&&!item.Presenting){child.localPosition=Vector3.zero;FitHeldItem(item);}
            }
        }
        public Vector3 MagnetEntry(float delay)
        {
            var delta=transform.TransformDirection(new Vector3((delay*17%1-.5f)*.65f,.2f,.7f));
            return ConstrainFlight(transform.position+delta);
        }
        public Vector3 ConstrainFlight(Vector3 position)
        {
            var delta=position-transform.position;
            float distance=delta.magnitude;
            if(distance<.001f)return transform.position;
            if(Physics.SphereCast(transform.position,.19f*ClearanceScale,delta.normalized,out var hit,distance,mask,QueryTriggerInteraction.Ignore))distance=Mathf.Max(0,hit.distance-.03f);
            return transform.position+delta.normalized*distance;
        }
    }
}
