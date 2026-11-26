using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LabSorting.Sorting;
using LabSorting.Player;
using PixelPlay.OffScreenIndicator;
using IAmJanitor.Rendering.Highlighting;
namespace LabSorting.UI
{
    [DefaultExecutionOrder(12000)]
    public sealed class MatchingRackGuide:MonoBehaviour
    {
        public SortingSession session;
        // Retained as the serialized canvas reference, never rendered as a caption.
        public Text label;
        public GuidedTutorial tutorial;
        public SortRack GuidedRack {get;private set;}
        public SortRack MarkerRack {get;private set;}
        public bool MarkerVisible=>marker&&marker.gameObject.activeSelf;
        public bool MarkerIsPin {get;private set;}
        public bool MarkerOccluded {get;private set;}
        public Rect MarkerScreenRect {get;private set;}
        public Vector3 MarkerWorldPoint {get;private set;}
        Image marker;RackLocationPin pin;Sprite arrow;
        RectTransform area;Camera uiCamera;CarryPresentation carry;
        readonly List<RectTransform> blockers=new List<RectTransform>();
        readonly Dictionary<SortRack,Bounds> rackGeometry=new Dictionary<SortRack,Bounds>();
        readonly Vector3[] corners=new Vector3[4];
        ItemDefinition selectedType;SortRack lastAim;int revision=-1,anchorIndex;
        float nextRefresh;Vector3 lastPosition,waypoint;bool routeValid;
        static readonly Color Mint=new Color(.35f,1,.72f);
        // Same mint as the pin, visible through walls, with a slow light breathe (~1.25 s, 25% dimmer at its low).
        static readonly HighlightStyle OutlineStyle=new HighlightStyle{color=Mint,widthPixels=3,pulse=true,pulseFrequency=.8f,pulseAmount=.25f,throughWalls=true};
        // The one rack the pin is guiding to also gets a rim, so its shape reads from behind walls.
        public SortRack OutlinedRack {get;private set;}
        ObjectHighlight outlined;

        static bool Eligible(SortingSession s,SortRack rack,ItemDefinition type,out float distance)
        {
            distance=float.PositiveInfinity;
            return rack&&rack.gameObject.activeInHierarchy&&rack.CanAccept(type)&&LabRackRoutes.For(s).Route(s.player.transform.position,rack,out distance,out _);
        }
        // Explicit aim wins. Otherwise prefer an assigned matching rack, then route distance.
        // The optional incumbent provides a distance margin so walking near a tie is stable.
        public static SortRack FindRack(SortingSession session,ItemDefinition type,SortRack incumbent=null)
        {
            if(!session||!type)return null;
            if(Eligible(session,session.TargetRack,type,out _))return session.TargetRack;
            SortRack best=null;float bestDistance=float.PositiveInfinity;bool bestMatching=false;
            foreach(var rack in session.racks)
            {
                if(!Eligible(session,rack,type,out float distance))continue;
                bool matching=rack.Type==type;
                if(!best||(matching&&!bestMatching)||(matching==bestMatching&&distance<bestDistance))
                {best=rack;bestMatching=matching;bestDistance=distance;}
            }
            if(best&&best!=incumbent&&Eligible(session,incumbent,type,out float currentDistance)&&
                (incumbent.Type==type)==bestMatching&&currentDistance-bestDistance<Mathf.Max(.6f,bestDistance*.12f))return incumbent;
            return best;
        }
        void Update()
        {
            if(label){label.text="";label.gameObject.SetActive(false);}
            if(!label||!session||!session.Initialized)return;
            if(session.player.blocked||!session.Active||(tutorial&&tutorial.IsRunning)){Hide();return;}
            var type=session.Active.definition;
            bool changed=type!=selectedType||revision!=session.Revision||lastAim!=session.TargetRack;
            bool moved=(session.player.transform.position-lastPosition).sqrMagnitude>=.0625f;
            if(!changed&&((GuidedRack&&!moved)||Time.unscaledTime<nextRefresh))return;
            nextRefresh=Time.unscaledTime+.4f;lastPosition=session.player.transform.position;
            var selected=FindRack(session,type,type==selectedType?GuidedRack:null);
            if(selected!=GuidedRack)anchorIndex=0;
            GuidedRack=selected;selectedType=type;revision=session.Revision;lastAim=session.TargetRack;
            routeValid=selected&&LabRackRoutes.For(session).Route(lastPosition,selected,out _,out waypoint);
        }
        void LateUpdate()
        {
            MarkerIsPin=false;MarkerOccluded=false;MarkerScreenRect=default;
            if(!session||!session.Initialized||session.player.blocked||!session.Active||(tutorial&&tutorial.IsRunning)||!GuidedRack||!routeValid)
            {if(marker)marker.gameObject.SetActive(false);MarkerRack=null;SetOutline(null);return;}
            if(!GuidedRack.gameObject.activeInHierarchy||!GuidedRack.CanAccept(session.Active.definition)){Hide();return;}
            EnsureMarker();MarkerRack=GuidedRack;
            bool otherFloor=Mathf.Abs(GuidedRack.transform.position.y-session.player.transform.position.y)>1.8f;
            // Another floor follows the stair waypoint instead; an aimed rack already shows its green/red feedback.
            SetOutline(otherFloor||GuidedRack==session.TargetRack?null:GuidedRack);
            bool onScreen=false;
            if(!otherFloor)
            {
                // Prefer the last safe rack-local anchor. A close view can expose a shelf
                // while the rack's nameplate is outside the view; never screen-clamp a pin.
                for(int pass=0;pass<6;pass++)
                {
                    int index=pass==0?anchorIndex:pass<=anchorIndex?pass-1:pass;
                    var world=Anchor(GuidedRack,index);var screen=OffScreenIndicatorCore.GetScreenPosition(session.player.view,world);
                    if(!InView(screen))continue;
                    onScreen=true;
                    if(Occluded(world)){MarkerOccluded=true;continue;}
                    PlaceMarker(screen,true,0);
                    if(!ClearOfUI(MarkerScreenRect))continue;
                    anchorIndex=index;MarkerWorldPoint=world;MarkerIsPin=true;MarkerOccluded=false;marker.gameObject.SetActive(true);return;
                }
                // Occluded or UI-covered visible racks don't get a misleading displaced pin.
                if(onScreen){marker.gameObject.SetActive(false);return;}
            }
            MarkerWorldPoint=otherFloor?waypoint+Vector3.up*.8f:Anchor(GuidedRack,0);
            var projected=OffScreenIndicatorCore.GetScreenPosition(session.player.view,MarkerWorldPoint);
            var safe=SafeRect();var direction=(Vector2)projected-safe.center;if(projected.z<0)direction=-direction;
            if(direction.sqrMagnitude<1)direction=Vector2.down;
            // Arrows may move inward along their direction to clear HUD controls. Pins never do.
            for(int step=0;step<9;step++)
            {
                float radius=1-step*.07f;float angle=0;
                var p=new Vector3(safe.center.x+direction.x,safe.center.y+direction.y,1);
                OffScreenIndicatorCore.GetArrowIndicatorPositionAndAngle(ref p,ref angle,safe.center,(safe.size*.5f-Vector2.one*30)*radius);
                PlaceMarker(p,false,angle*Mathf.Rad2Deg);
                if(!ClearOfUI(MarkerScreenRect))continue;
                marker.gameObject.SetActive(true);return;
            }
            marker.gameObject.SetActive(false);
        }
        Vector3 Anchor(SortRack rack,int index)
        {
            if(!rackGeometry.TryGetValue(rack,out var bounds))
            {
                bool found=false;bounds=new Bounds(rack.transform.position+Vector3.up*.8f,new Vector3(1,1.6f,.5f));
                foreach(var surface in rack.GetComponentsInChildren<Renderer>())
                {
                    if(!rack.IsFrameRenderer(surface))continue;
                    if(!found){bounds=surface.bounds;found=true;}else bounds.Encapsulate(surface.bounds);
                }
                rackGeometry[rack]=bounds;
            }
            var center=bounds.center;
            switch(index)
            {
                case 1:return center+Vector3.up*bounds.extents.y*.55f;
                case 2:return center;
                case 3:return center-Vector3.up*bounds.extents.y*.5f;
                case 4:return center+rack.transform.right*.35f;
                case 5:return center-rack.transform.right*.35f;
                default:return new Vector3(center.x,bounds.max.y+.08f,center.z);
            }
        }
        bool InView(Vector3 p)=>p.z>session.player.view.nearClipPlane&&session.player.view.pixelRect.Contains(p);
        bool Occluded(Vector3 point)
        {
            int mask=~((1<<2)|LayerMask.GetMask("SortingItem","HeldItem"));
            return Physics.Linecast(session.player.view.transform.position,point,out var hit,mask,QueryTriggerInteraction.Ignore)&&
                !hit.collider.transform.IsChildOf(GuidedRack.transform);
        }
        void PlaceMarker(Vector3 screen,bool isPin,float angle)
        {
            var rect=marker.rectTransform;
            rect.pivot=isPin?new Vector2(.5f,0):new Vector2(.5f,.5f);
            rect.sizeDelta=isPin?new Vector2(34,44):Vector2.one*34;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area,screen,uiCamera,out var local);
            rect.localPosition=local;rect.localRotation=Quaternion.Euler(0,0,isPin?0:angle);
            marker.enabled=!isPin;pin.enabled=isPin;MarkerScreenRect=ScreenRect(rect);
        }
        Rect ScreenRect(RectTransform rect)
        {
            rect.GetWorldCorners(corners);Vector2 min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue;
            foreach(var corner in corners){var p=RectTransformUtility.WorldToScreenPoint(uiCamera,corner);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        Rect SafeRect()
        {
            var a=ScreenRect(area);var b=Screen.safeArea;var c=session.player.view.pixelRect;
            return Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin,c.xMin)+6,Mathf.Max(a.yMin,b.yMin,c.yMin)+6,
                Mathf.Min(a.xMax,b.xMax,c.xMax)-6,Mathf.Min(a.yMax,b.yMax,c.yMax)-6);
        }
        static bool Visible(RectTransform rect)
        {
            if(!rect||!rect.gameObject.activeInHierarchy)return false;
            for(Transform t=rect;t;t=t.parent){var group=t.GetComponent<CanvasGroup>();if(group&&group.alpha<.05f)return false;}
            return true;
        }
        bool ClearOfUI(Rect rect)
        {
            var safe=SafeRect();if(!safe.Contains(rect.min)||!safe.Contains(rect.max))return false;
            float gap=8*Screen.height/720f;rect.xMin-=gap;rect.xMax+=gap;rect.yMin-=gap;rect.yMax+=gap;
            var center=session.player.view.pixelRect.center;
            if(rect.Overlaps(new Rect(center-Vector2.one*22,Vector2.one*44)))return false;
            if(carry&&carry.HeldScreenRect.width>0&&rect.Overlaps(carry.HeldScreenRect))return false;
            foreach(var blocker in blockers)if(Visible(blocker)&&rect.Overlaps(ScreenRect(blocker)))return false;
            return true;
        }
        void AddBlocker(Transform t){if(t is RectTransform rect&&!blockers.Contains(rect))blockers.Add(rect);}
        void EnsureMarker()
        {
            if(marker)return;
            area=(RectTransform)label.transform.parent;var canvas=label.GetComponentInParent<Canvas>();
            uiCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            carry=session.hand.GetComponent<CarryPresentation>();arrow=Resources.Load<Sprite>("LabRackIndicators/WhiteArrow");
            foreach(var button in canvas.GetComponentsInChildren<Button>(true))
            {AddBlocker(button.transform);foreach(var caption in button.GetComponentsInChildren<Text>(true))AddBlocker(caption.transform);}
            var task=canvas.GetComponent<TaskCardLayout>();if(task)AddBlocker(task.card);
            var hud=canvas.GetComponent<SortingHUD>();
            if(hud){if(hud.selectedCard)AddBlocker(hud.selectedCard.transform);if(hud.inventoryPanel)AddBlocker(hud.inventoryPanel.transform);if(hud.progress)AddBlocker(hud.progress.transform);if(hud.inventorySummary)AddBlocker(hud.inventorySummary.transform);}
            var controls=canvas.GetComponent<ControlSizes>();if(controls)AddBlocker(controls.joystick);
            var go=new GameObject("Pixel Play rack indicator",typeof(RectTransform),typeof(Image),typeof(UnityEngine.UI.Outline));go.transform.SetParent(area,false);
            marker=go.GetComponent<Image>();marker.sprite=arrow;marker.raycastTarget=false;marker.color=Mint;
            var outline=go.GetComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.015f,.06f,.08f,1);outline.effectDistance=Vector2.one*2;
            var pinObject=new GameObject("Rack location pin",typeof(RectTransform),typeof(RackLocationPin),typeof(UnityEngine.UI.Outline));pinObject.transform.SetParent(go.transform,false);
            pin=pinObject.GetComponent<RackLocationPin>();pin.color=Mint;pin.raycastTarget=false;
            var p=pin.rectTransform;p.anchorMin=Vector2.zero;p.anchorMax=Vector2.one;p.offsetMin=p.offsetMax=Vector2.zero;
            var rim=pinObject.GetComponent<UnityEngine.UI.Outline>();rim.effectColor=outline.effectColor;rim.effectDistance=Vector2.one*1.5f;
        }
        void SetOutline(SortRack rack)
        {
            if(rack==OutlinedRack)return;
            if(outlined)outlined.Release(this,HighlightChannel.Task);
            OutlinedRack=rack;outlined=rack?RackOutline(rack):null;
            if(outlined)outlined.SetRequest(this,HighlightChannel.Task,OutlineStyle);
        }
        // Created once per rack on first guidance; only the rack body is outlined, never its contents.
        static ObjectHighlight RackOutline(SortRack rack)
        {
            var highlight=rack.GetComponent<ObjectHighlight>();if(highlight)return highlight;
            var frame=new List<Renderer>();
            foreach(var r in rack.GetComponentsInChildren<MeshRenderer>(true))if(rack.IsFrameRenderer(r))frame.Add(r);
            if(frame.Count==0)return null;
            // A static-batched renderer only exposes the scene's combined mesh, so use the rack's baked body mesh.
            Mesh[] prepared=null;
            if(frame.Count==1&&rack.outlineMesh)prepared=new[]{OutlineMeshCache.Acquire(rack.outlineMesh)};
            else if(frame.Exists(r=>r.isPartOfStaticBatch))return null;
            highlight=rack.gameObject.AddComponent<ObjectHighlight>();highlight.Configure(frame.ToArray(),null,prepared);
            return highlight;
        }
        void Hide(){if(label){label.text="";label.gameObject.SetActive(false);}if(marker)marker.gameObject.SetActive(false);SetOutline(null);MarkerRack=null;GuidedRack=null;selectedType=null;routeValid=false;nextRefresh=0;}
        void OnDisable(){Hide();}
        void OnDestroy(){if(marker)Destroy(marker.gameObject);}
    }
}
