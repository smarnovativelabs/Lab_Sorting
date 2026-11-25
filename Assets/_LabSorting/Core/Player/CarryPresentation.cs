using UnityEngine;
using UnityEngine.Rendering.Universal;
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
        LabSorting.Sorting.SortingSession session;
        readonly Vector3[] uiCorners=new Vector3[4];
        public Rect HeldScreenRect {get;private set;}
        public Rect HeldSafeRect {get;private set;}
        Camera heldCamera;
        Light heldLight;
        LabSorting.Sorting.SortItem displayedItem;
        Transform[] heldParts;
        int[] originalLayers;
        int originalMask;
        int heldLayer=-1;
        public bool OverlayActive=>heldCamera&&heldCamera.enabled&&displayedItem;
        void EnsureOverlay()
        {
            if(heldCamera||!view)return;
            heldLayer=LayerMask.NameToLayer("HeldItem");if(heldLayer<0)return;
            var data=view.GetUniversalAdditionalCameraData();
            if(data.renderType!=CameraRenderType.Base||data.cameraStack==null)return;
            originalMask=view.cullingMask;view.cullingMask&=~(1<<heldLayer);
            var go=new GameObject("Held Item Camera");go.transform.SetParent(view.transform,false);
            heldCamera=go.AddComponent<Camera>();heldCamera.CopyFrom(view);
            heldCamera.cullingMask=1<<heldLayer;heldCamera.useOcclusionCulling=false;
            var overlay=heldCamera.GetUniversalAdditionalCameraData();overlay.renderType=CameraRenderType.Overlay;overlay.renderPostProcessing=false;overlay.renderShadows=false;
            data.cameraStack.Add(heldCamera);
            heldLight=go.AddComponent<Light>();heldLight.type=LightType.Point;heldLight.range=3;heldLight.intensity=.65f;heldLight.shadows=LightShadows.None;heldLight.cullingMask=1<<heldLayer;
        }
        void RestoreHeldVisual()
        {
            if(heldParts!=null)for(int i=0;i<heldParts.Length;i++)if(heldParts[i])heldParts[i].gameObject.layer=originalLayers[i];
            if(displayedItem)displayedItem.SetHeldPresentation(false);
            displayedItem=null;heldParts=null;originalLayers=null;
        }
        void UpdateHeldVisual()
        {
            EnsureOverlay();
            LabSorting.Sorting.SortItem active=null;
            foreach(Transform child in transform)
            {
                if(!child.gameObject.activeInHierarchy)continue;
                var item=child.GetComponent<LabSorting.Sorting.SortItem>();
                if(item&&item.location==LabSorting.Sorting.ItemLocation.Hand&&!item.MagnetFlying)active=item;
            }
            if(session&&session.Active&&session.Active.transform.parent==transform&&session.Active.gameObject.activeInHierarchy&&!session.Active.MagnetFlying)active=session.Active;
            if(active!=displayedItem)
            {
                RestoreHeldVisual();displayedItem=active;
                if(active)
                {
                    active.SetHeldPresentation(true);
                    if(heldCamera)
                    {
                        heldParts=active.GetComponentsInChildren<Transform>(true);originalLayers=new int[heldParts.Length];
                        for(int i=0;i<heldParts.Length;i++){originalLayers[i]=heldParts[i].gameObject.layer;heldParts[i].gameObject.layer=heldLayer;}
                    }
                }
            }
            // Save restores can reset item layers without changing the selected object.
            if(heldParts!=null)foreach(var part in heldParts)if(part)part.gameObject.layer=heldLayer;
            if(heldCamera)
            {
                heldCamera.enabled=active&&view.enabled;
                heldCamera.aspect=view.aspect;heldCamera.fieldOfView=view.fieldOfView;heldCamera.nearClipPlane=view.nearClipPlane;heldCamera.farClipPlane=view.farClipPlane;
                heldCamera.rect=view.rect;heldLight.enabled=heldCamera.enabled;
            }
        }
        void OnDisable()
        {
            RestoreHeldVisual();
            if(heldCamera)
            {
                if(view){view.GetUniversalAdditionalCameraData().cameraStack.Remove(heldCamera);view.cullingMask=originalMask;}
                Destroy(heldCamera.gameObject);heldCamera=null;
            }
        }
        void Start()
        {
            session=FindFirstObjectByType<LabSorting.Sorting.SortingSession>();
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
        Rect ProjectBounds(Renderer visual)
        {
            var bounds=visual.localBounds;
            Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
            for(int i=0;i<8;i++){var p=view.WorldToScreenPoint(visual.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        public struct HeldPose
        {
            public Vector3 position,scale;
            public Quaternion rotation;
            public void Apply(Transform item){item.localPosition=position;item.localRotation=rotation;item.localScale=scale;}
        }
        // Resolve against the actual model and current camera/UI, without leaving the
        // item at the trial pose. Flights and the settled display share this endpoint.
        public HeldPose ResolveHeldPose(LabSorting.Sorting.SortItem item)
        {
            var t=item.transform;
            var previous=new HeldPose{position=t.localPosition,rotation=t.localRotation,scale=t.localScale};
            try
            {
                t.localPosition=Vector3.zero;t.localRotation=Quaternion.Euler(-20,-20,-5);
                t.localScale=item.originalScale*item.CarryScale*ClearanceScale;
                if(view&&item.visual)FitHeldItem(item);
                return new HeldPose{position=t.localPosition,rotation=t.localRotation,scale=t.localScale};
            }
            finally{previous.Apply(t);}
        }
        void FitHeldItem(LabSorting.Sorting.SortItem item)
        {
            float padding=Screen.height*.018f;
            float left=Screen.width*.52f,right=Screen.width*.88f;
            if(selectedCard)left=Mathf.Max(left,ScreenRect(selectedCard).xMax+padding);
            if(actions)
            {
                // The item may slide under the edge of the action buttons, like a held object in front of the HUD.
                foreach(var button in new[]{actions.pick,actions.drop,actions.place})
                {
                    if(!button)continue;
                    var rect=ScreenRect(button.transform as RectTransform);
                    right=Mathf.Min(right,rect.xMin+rect.width*ButtonOverlap);
                }
            }
            // Use the open space above controls on unusually narrow layouts.
            float bottom=Screen.height*.11f,top=Screen.height*.52f;
            if(right-left<Screen.height*.12f){left=Screen.width*.52f;right=Screen.width*.80f;bottom=Screen.height*.48f;top=Screen.height*.72f;}
            var safe=Screen.safeArea;
            left=Mathf.Max(left,safe.xMin+padding);right=Mathf.Max(left+1,Mathf.Min(right,safe.xMax-padding));
            bottom=Mathf.Max(bottom,safe.yMin+padding);top=Mathf.Max(bottom+1,Mathf.Min(top,safe.yMax-padding));
            HeldSafeRect=Rect.MinMaxRect(left,bottom,right,top);
            var projected=ProjectBounds(item.visual);
            float multiplier=item.definition?Mathf.Max(1,item.definition.heldSizeMultiplier):1;
            float height=Screen.height*.33f*multiplier,fill=Mathf.Min(1f,.86f*multiplier);
            float size=HeldSafeRect.width*fill;
            float factor=Mathf.Min(size/Mathf.Max(1,projected.width),Mathf.Min(height,HeldSafeRect.height*fill)/Mathf.Max(1,projected.height));
            item.transform.localScale*=Mathf.Clamp(factor,.1f,4f);
            // Center visible geometry, not the imported model's arbitrary pivot.
            var center=view.WorldToScreenPoint(item.visual.bounds.center);
            var destination=new Vector3(HeldSafeRect.center.x,HeldSafeRect.center.y,center.z);
            item.transform.position+=view.ScreenToWorldPoint(destination)-view.ScreenToWorldPoint(center);
            // Scaling and moving an offset model changes its perspective bounds. Refit
            // the projected rectangle, so wide microscopes cannot spill into the HUD.
            for(int pass=0;pass<4;pass++)
            {
                var fitted=ProjectBounds(item.visual);
                float refit=Mathf.Min(size/Mathf.Max(1,fitted.width),Mathf.Min(height,HeldSafeRect.height*fill)/Mathf.Max(1,fitted.height));
                item.transform.localScale*=Mathf.Clamp(refit,.1f,4f);
                fitted=ProjectBounds(item.visual);
                float depth=view.WorldToScreenPoint(item.visual.bounds.center).z;
                item.transform.position+=view.ScreenToWorldPoint(new Vector3(HeldSafeRect.center.x,HeldSafeRect.center.y,depth))-view.ScreenToWorldPoint(new Vector3(fitted.center.x,fitted.center.y,depth));
            }
            // Keep the complete geometry inside the same clearance envelope as the
            // wall probe. Shrink about its visible center, not an arbitrary pivot.
            var bounds=item.visual.localBounds;var centerWorld=item.visual.transform.TransformPoint(bounds.center);
            float radius=0;
            for(int i=0;i<8;i++)radius=Mathf.Max(radius,Vector3.Distance(centerWorld,item.visual.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))));
            float centerDepth=view.WorldToScreenPoint(centerWorld).z;
            float limit=Mathf.Min(Envelope*ClearanceScale,Mathf.Max(.001f,centerDepth-view.nearClipPlane-.015f));
            if(radius>limit){item.transform.localScale*=limit/radius;item.transform.position+=centerWorld-item.visual.transform.TransformPoint(bounds.center);}
            HeldScreenRect=ProjectBounds(item.visual);
        }
        // Radius of the carried model, and how far (fraction of button width) it may slide under an action button.
        const float Envelope=.23f,ButtonOverlap=.35f;
        public float ClearanceScale {get;private set;}=1;
        void Awake(){rest=transform.localPosition;mask=~((1<<2)|LayerMask.GetMask("SortingItem","HeldItem"));}
        void LateUpdate()
        {
            if(!view)return;
            UpdateHeldVisual();
            Vector3 desired=transform.parent.TransformPoint(rest),origin=view.transform.position;
            var delta=desired-origin;float distance=delta.magnitude;
            bool blocked=Physics.SphereCast(origin,Envelope+.02f,delta.normalized,out var hit,distance,mask,QueryTriggerInteraction.Ignore);
            float restDepth=Vector3.Dot(delta,view.transform.forward);
            // Every carried model fits the .18m envelope. Keep its rear edge ahead of
            // the near plane rather than enforcing a fixed minimum distance from walls.
            float minimum=Mathf.Clamp01((view.nearClipPlane+.015f)/Mathf.Max(.001f,restDepth-Envelope));
            float target=blocked?Mathf.Clamp((hit.distance-.025f)/Mathf.Max(.001f,distance),minimum,1):1;
            // Smooth a camera-relative distance, never a world position left behind by a turn.
            // Size and camera depth change together, preserving the projected size near walls.
            ClearanceScale=target<ClearanceScale?target:Mathf.Lerp(ClearanceScale,target,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            transform.position=origin+delta*ClearanceScale;
            foreach(Transform child in transform)
            {
                if(!child.gameObject.activeSelf)continue;
                var item=child.GetComponent<LabSorting.Sorting.SortItem>();if(!item||item.location!=LabSorting.Sorting.ItemLocation.Hand)continue;
                item.RefreshHeldPresentation(this);
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
            if(Physics.SphereCast(transform.position,(Envelope+.01f)*ClearanceScale,delta.normalized,out var hit,distance,mask,QueryTriggerInteraction.Ignore))distance=Mathf.Max(0,hit.distance-.03f);
            return transform.position+delta.normalized*distance;
        }
    }
}
