using LabSorting.Sorting;
using UnityEngine;
namespace LabSorting.UI
{
    public sealed partial class GuidedTutorial
    {
        public SortRack GuidedRack {get;private set;}
        public bool RackGuideVisible=>rackGuide&&rackGuide.gameObject.activeSelf;
        UnityEngine.UI.Text rackGuide;
        TutorialRackArrow rackArrow;
        readonly Vector3[] rackBubbleCorners=new Vector3[4];
        float nextGuideCheck,placedHintUntil;
        void HideRackGuide(){if(rackGuide)rackGuide.gameObject.SetActive(false);if(rackArrow)rackArrow.gameObject.SetActive(false);GuidedRack=null;nextGuideCheck=0;}
        void UpdateRackGuide(bool blocked)
        {
            if(blocked||!IsRunning||Current!=Step.Place||!session.Active){HideRackGuide();return;}
            var type=session.Active.definition;
            if(!GuidedRack||!GuidedRack.CanAccept(type)||Mathf.Abs(GuidedRack.transform.position.y-session.player.transform.position.y)>.8f)
            {
                GuidedRack=null;
                if(Time.unscaledTime>=nextGuideCheck)
                {
                    nextGuideCheck=Time.unscaledTime+.4f;float best=float.MaxValue;
                    foreach(var rack in session.racks)
                    {
                        if(!rack||!rack.gameObject.activeInHierarchy||!rack.CanAccept(type)||Mathf.Abs(rack.transform.position.y-session.player.transform.position.y)>.8f)continue;
                        var target=rack.label?rack.label.transform.position:rack.transform.position+Vector3.up*1.4f;
                        if(Physics.Linecast(session.player.view.transform.position,target,out var hit,~((1<<2)|LayerMask.GetMask("SortingItem")),QueryTriggerInteraction.Ignore)&&!hit.collider.transform.IsChildOf(rack.transform))continue;
                        var toRack=target-session.player.view.transform.position;
                        float distance=toRack.sqrMagnitude;
                        // Empty racks teach assignment; prefer a nearby rack already in front of the player.
                        if(rack.Type)distance*=2f;
                        float facing=Vector3.Dot(session.player.view.transform.forward,toRack.normalized);
                        distance*=Mathf.Lerp(1.8f,.75f,(facing+1)*.5f);
                        if(distance<best){best=distance;GuidedRack=rack;}
                    }
                }
            }
            if(!GuidedRack){if(rackGuide)rackGuide.gameObject.SetActive(false);if(rackArrow)rackArrow.gameObject.SetActive(false);return;}
            // Once the player aims at a compatible rack, the PLACE hand takes over.
            // Keep the navigation text from covering the rack's own label.
            if(session.TargetRack&&session.TargetRack.CanAccept(type)){if(rackGuide)rackGuide.gameObject.SetActive(false);if(rackArrow)rackArrow.gameObject.SetActive(false);return;}
            if(!rackGuide)
            {
                var go=new GameObject("Tutorial Rack Guide",typeof(RectTransform),typeof(UnityEngine.UI.Text),typeof(UnityEngine.UI.Outline));
                go.transform.SetParent(area,false);rackGuide=go.GetComponent<UnityEngine.UI.Text>();
                rackGuide.font=message.font;rackGuide.fontSize=24;rackGuide.alignment=TextAnchor.MiddleCenter;rackGuide.raycastTarget=false;
                var rt=rackGuide.rectTransform;rt.sizeDelta=new Vector2(190,68);rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);
                var arrowObject=new GameObject("Tutorial Rack Arrow",typeof(RectTransform),typeof(TutorialRackArrow),typeof(UnityEngine.UI.Outline));
                arrowObject.transform.SetParent(area,false);rackArrow=arrowObject.GetComponent<TutorialRackArrow>();rackArrow.raycastTarget=false;
                rackArrow.rectTransform.sizeDelta=new Vector2(32,40);rackArrow.color=new Color(.35f,1f,.75f);
                var border=arrowObject.GetComponent<UnityEngine.UI.Outline>();border.effectColor=new Color(0,.1f,.08f,.9f);border.effectDistance=Vector2.one*2;
                var outline=go.GetComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(0,0,0,.85f);outline.effectDistance=new Vector2(1,-1);
            }
            rackGuide.gameObject.SetActive(true);
            var point=GuidedRack.label?GuidedRack.label.transform.position:GuidedRack.transform.position+Vector3.up*1.4f;
            var screen=session.player.view.WorldToScreenPoint(point);var local=(Vector2)area.InverseTransformPoint(new Vector3(screen.x,screen.y,0));
            var rect=area.rect;var center=rect.center;
            if(screen.z<0)local=center-(local-center);
            var inset=Rect.MinMaxRect(rect.xMin+110,rect.yMin+145,rect.xMax-210,rect.yMax-125);
            bool edge=screen.z<0||!inset.Contains(local);
            Vector2 direction=local-center;
            if(direction.sqrMagnitude<1)direction=Vector2.down;
            if(edge)
            {
                float horizontal=(direction.x>0?inset.xMax-center.x:center.x-inset.xMin)/Mathf.Max(.001f,Mathf.Abs(direction.x));
                float vertical=(direction.y>0?inset.yMax-center.y:center.y-inset.yMin)/Mathf.Max(.001f,Mathf.Abs(direction.y));
                local=center+direction*Mathf.Min(horizontal,vertical);
            }
            // Keep both the arrow and its label clear of the tutorial callout.
            bubble.GetWorldCorners(rackBubbleCorners);
            Vector2 bubbleMin=area.InverseTransformPoint(rackBubbleCorners[0]),bubbleMax=area.InverseTransformPoint(rackBubbleCorners[2]);
            var bubbleBounds=Rect.MinMaxRect(bubbleMin.x,bubbleMin.y,bubbleMax.x,bubbleMax.y);
            var markerBounds=new Rect(local.x-100,local.y-82,200,150);
            if(markerBounds.Overlaps(bubbleBounds))
            {
                var destination=local;local.y=Mathf.Max(inset.yMin,bubbleBounds.yMin-70);
                direction=destination-local;edge=true;
                if(direction.sqrMagnitude<1)direction=Vector2.up;
            }
            rackArrow.gameObject.SetActive(true);
            rackArrow.rectTransform.localPosition=local+(!edge?Vector2.up*(35+5*Mathf.Sin(Time.unscaledTime*4)):Vector2.zero);
            rackArrow.rectTransform.localRotation=Quaternion.Euler(0,0,edge?Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg+90:0);
            rackGuide.rectTransform.localPosition=new Vector2(Mathf.Clamp(local.x,inset.xMin,inset.xMax),Mathf.Clamp(local.y-48,rect.yMin+70,rect.yMax-70));
            rackGuide.text=(GuidedRack.Type?"MATCHING RACK":"EMPTY RACK")+"\n"+Mathf.CeilToInt(Vector3.Distance(session.player.transform.position,point))+" m";
            rackGuide.color=new Color(.35f,1f,.75f,.86f+.14f*Mathf.Sin(Time.unscaledTime*3));
        }
    }
    // Filled arrow silhouette remains legible against both shelves and piles of items.
    public sealed class TutorialRackArrow:UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            Vector2[] points={new Vector2(-.22f,.5f),new Vector2(.22f,.5f),new Vector2(.22f,0),new Vector2(.5f,0),new Vector2(0,-.5f),new Vector2(-.5f,0),new Vector2(-.22f,0)};
            foreach(var p in points)mesh.AddVert(new Vector3(r.center.x+p.x*r.width,r.center.y+p.y*r.height,0),color,Vector2.zero);
            mesh.AddTriangle(0,1,2);mesh.AddTriangle(0,2,6);mesh.AddTriangle(5,3,4);
        }
    }
}
