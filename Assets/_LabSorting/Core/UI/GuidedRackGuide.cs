using LabSorting.Sorting;
using UnityEngine;
namespace LabSorting.UI
{
    public sealed partial class GuidedTutorial
    {
        public SortRack GuidedRack {get;private set;}
        public bool RackGuideVisible=>rackGuide&&rackGuide.gameObject.activeSelf;
        UnityEngine.UI.Text rackGuide;
        float nextGuideCheck,placedHintUntil;
        void HideRackGuide(){if(rackGuide)rackGuide.gameObject.SetActive(false);GuidedRack=null;nextGuideCheck=0;}
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
                        float distance=(target-session.player.view.transform.position).sqrMagnitude;
                        if(distance<best){best=distance;GuidedRack=rack;}
                    }
                }
            }
            if(!GuidedRack){if(rackGuide)rackGuide.gameObject.SetActive(false);return;}
            // Once the player aims at a compatible rack, the PLACE hand takes over.
            // Keep the navigation text from covering the rack's own label.
            if(session.TargetRack&&session.TargetRack.CanAccept(type)){if(rackGuide)rackGuide.gameObject.SetActive(false);return;}
            if(!rackGuide)
            {
                var go=new GameObject("Tutorial Rack Guide",typeof(RectTransform),typeof(UnityEngine.UI.Text),typeof(UnityEngine.UI.Outline));
                go.transform.SetParent(area,false);rackGuide=go.GetComponent<UnityEngine.UI.Text>();
                rackGuide.font=message.font;rackGuide.fontSize=24;rackGuide.alignment=TextAnchor.MiddleCenter;rackGuide.raycastTarget=false;
                var rt=rackGuide.rectTransform;rt.sizeDelta=new Vector2(190,68);rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);
                var outline=go.GetComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(0,0,0,.85f);outline.effectDistance=new Vector2(1,-1);
            }
            rackGuide.gameObject.SetActive(true);
            var point=GuidedRack.label?GuidedRack.label.transform.position:GuidedRack.transform.position+Vector3.up*1.4f;
            var screen=session.player.view.WorldToScreenPoint(point);var local=(Vector2)area.InverseTransformPoint(new Vector3(screen.x,screen.y,0));
            if(screen.z<0)local=area.rect.center-(local-area.rect.center);
            var rect=area.rect;bool edge=screen.z<0||!rect.Contains(local);
            local.x=Mathf.Clamp(local.x,rect.xMin+100,rect.xMax-100);local.y=Mathf.Clamp(local.y,rect.yMin+80,rect.yMax-100);
            rackGuide.rectTransform.localPosition=local;
            rackGuide.text=(edge?(local.x<rect.center.x?"< RACK":"RACK >"):"v RACK")+"\n"+Mathf.CeilToInt(Vector3.Distance(session.player.transform.position,point))+" m";
            rackGuide.color=new Color(.35f,1f,.75f,.86f+.14f*Mathf.Sin(Time.unscaledTime*3));
        }
    }
}
