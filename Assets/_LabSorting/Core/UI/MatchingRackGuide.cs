using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class MatchingRackGuide:MonoBehaviour
    {
        public SortingSession session;
        public UnityEngine.UI.Text label;
        public GuidedTutorial tutorial;
        public SortRack GuidedRack {get;private set;}
        public SortRack MarkerRack {get;private set;}
        public bool MarkerVisible=>marker&&marker.gameObject.activeSelf;
        UnityEngine.UI.Image marker;
        float nextVisibilityCheck;
        bool markerOccluded;
        SortRack checkedRack;
        float nextRefresh;
        public static SortRack FindRack(SortingSession session,ItemDefinition type)
        {
            SortRack best=null;float score=float.PositiveInfinity;
            foreach(var rack in session.racks)
            {
                if(!rack||!rack.gameObject.activeInHierarchy||!rack.CanAccept(type))continue;
                float distance=(rack.transform.position-session.player.transform.position).sqrMagnitude;
                // Assigned racks win over empty racks, even on a different floor.
                if(!best||(rack.Type==type&&best.Type!=type)||(rack.Type==best.Type&&distance<score)){best=rack;score=distance;}
            }
            return best;
        }
        void Update()
        {
            if(!label||!session)return;
            if(session.player.blocked||!session.Active||(tutorial&&tutorial.IsRunning))
            {label.gameObject.SetActive(false);GuidedRack=null;nextRefresh=0;return;}
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.15f;
            var type=session.Active.definition;GuidedRack=FindRack(session,type);
            bool show=GuidedRack&&!(session.TargetRack&&session.TargetRack.CanAccept(type));
            label.gameObject.SetActive(show);if(!show)return;
            var delta=GuidedRack.transform.position-session.player.transform.position;
            label.text=(GuidedRack.Type==type?"MATCHING RACK":"EMPTY RACK")+"  |  "+SearchDirection.Bearing(session.player.view.transform.forward,delta)+"  |  "+delta.magnitude.ToString("0.0")+"m\n"+SearchDirection.Floor(GuidedRack.transform.position.y,session.player.transform.position.y);
            label.color=Mathf.Abs(delta.y)>1.8f?new Color(1,.87f,.4f):new Color(.45f,1,.7f);
        }
        void LateUpdate()
        {
            MarkerRack=null;
            if(session&&session.player&&!session.player.blocked&&session.Active)
            {
                var candidate=tutorial&&tutorial.IsRunning?tutorial.GuidedRack:GuidedRack;
                if(candidate&&candidate.gameObject.activeInHierarchy&&candidate.CanAccept(session.Active.definition))MarkerRack=candidate;
            }
            if(!MarkerRack){if(marker)marker.gameObject.SetActive(false);return;}
            if(!marker)
            {
                var go=new GameObject("Guided Rack Diamond",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Outline));
                go.transform.SetParent(label.transform.parent,false);marker=go.GetComponent<UnityEngine.UI.Image>();marker.raycastTarget=false;
                marker.rectTransform.sizeDelta=Vector2.one*18;marker.rectTransform.localRotation=Quaternion.Euler(0,0,45);
                var rim=go.GetComponent<UnityEngine.UI.Outline>();rim.effectColor=new Color(0,.15f,.12f,.9f);rim.effectDistance=new Vector2(2,2);
            }
            var camera=session.player.view;
            var point=(MarkerRack.label?MarkerRack.label.transform.position:MarkerRack.transform.position+Vector3.up*1.4f)+Vector3.up*.22f;
            if(checkedRack!=MarkerRack||Time.unscaledTime>=nextVisibilityCheck)
            {
                checkedRack=MarkerRack;nextVisibilityCheck=Time.unscaledTime+.15f;
                markerOccluded=Physics.Linecast(camera.transform.position,point,out var hit,~((1<<2)|LayerMask.GetMask("SortingItem")),QueryTriggerInteraction.Ignore)&&!hit.collider.transform.IsChildOf(MarkerRack.transform);
            }
            var screen=camera.WorldToScreenPoint(point);var area=(RectTransform)marker.transform.parent;
            var canvas=marker.GetComponentInParent<Canvas>();var uiCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area,screen,uiCamera,out var local);
            bool visible=!markerOccluded&&screen.z>0&&area.rect.Contains(local)&&Mathf.Abs(MarkerRack.transform.position.y-session.player.transform.position.y)<1.8f;
            marker.gameObject.SetActive(visible);if(!visible)return;
            marker.rectTransform.localPosition=local;marker.color=new Color(.4f,1,.75f,.65f+.15f*Mathf.Sin(Time.unscaledTime*3));
        }
        void OnDisable(){if(label)label.gameObject.SetActive(false);if(marker)marker.gameObject.SetActive(false);MarkerRack=null;GuidedRack=null;}
    }
}
