#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using LabSorting.Sorting;
using LabSorting.Player;
using LabSorting.UI;
using PixelPlay.OffScreenIndicator;

namespace LabSorting.Prototype
{
    // Deliberately opt-in: attached only to Scenes/RackGuidance_Preview.unity.
    // This is a review proposal, not a change to the production guide.
    [DefaultExecutionOrder(12000)]
    public sealed class RackGuidancePreview : MonoBehaviour
    {
        public bool aimAtRack;
        SortingSession session;
        SortRack rack;
        CarryPresentation carry;
        ContextActionHUD actions;
        SortingHUD hud;
        MatchingRackGuide original;
        Image marker;
        Text actionCaption;
        Sprite arrow,box;
        bool ready,lastAim;
        public SortRack PreviewRack=>rack;
        public bool MarkerShown=>marker&&marker.enabled;
        public bool ActionCaptionShown=>actionCaption&&actionCaption.gameObject.activeInHierarchy;

        IEnumerator Start()
        {
            session=FindFirstObjectByType<SortingSession>();session.persistence=false;
            while(!session.Initialized)yield return null;
            foreach(var service in FindObjectsByType<ServicePresentation>(FindObjectsSortMode.None))service.enabled=false;
            foreach(var tutorial in FindObjectsByType<GuidedTutorial>(FindObjectsSortMode.None))tutorial.enabled=false;
            session.player.SetNetworkBlocked(false);session.player.SetTutorialBlocked(false);session.player.SetBlocked(false);session.player.SetPickTutorialLocked(false);
            original=FindFirstObjectByType<MatchingRackGuide>();original.enabled=false;original.label.gameObject.SetActive(false);
            actions=FindFirstObjectByType<ContextActionHUD>();hud=FindFirstObjectByType<SortingHUD>();carry=session.hand.GetComponent<CarryPresentation>();
            var item=session.items.First(i=>i.definition.displayName=="Microscope Big"&&i.location==ItemLocation.Loose);
            session.TryPick(item);item.CompleteMotion();
            rack=session.racks.Where(r=>r.CanAccept(item.definition)).OrderBy(r=>(r.transform.position-session.player.transform.position).sqrMagnitude).First();
            var position=rack.transform.position-rack.transform.forward*2.4f;position.y=.03f;
            session.player.character.Motor.SetPosition(position);
            arrow=Resources.Load<Sprite>("LabRackIndicators/WhiteArrow");box=Resources.Load<Sprite>("LabRackIndicators/WhiteBox");
            var go=new GameObject("Pixel Play preview marker",typeof(RectTransform),typeof(Image),typeof(UnityEngine.UI.Outline));go.transform.SetParent(original.label.transform.parent,false);
            marker=go.GetComponent<Image>();marker.raycastTarget=false;marker.color=new Color(.35f,1,.72f);marker.rectTransform.sizeDelta=Vector2.one*38;
            var border=go.GetComponent<UnityEngine.UI.Outline>();border.effectColor=new Color(.015f,.06f,.08f,1);border.effectDistance=Vector2.one*2;
            foreach(var text in actions.place.GetComponentsInChildren<Text>(true))text.enabled=false;
            actionCaption=Instantiate(original.label,actions.place.transform);actionCaption.name="Preview action caption";actionCaption.enabled=true;actionCaption.raycastTarget=false;
            actionCaption.fontSize=13;actionCaption.alignment=TextAnchor.MiddleCenter;actionCaption.color=Color.white;actionCaption.text="ASSIGN RACK";
            var rect=actionCaption.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-8);rect.sizeDelta=new Vector2(140,26);
            yield return new WaitForSecondsRealtime(.3f);
            ready=true;lastAim=aimAtRack;yield return SetView(aimAtRack);
        }
        public void ShowAimed(bool value){aimAtRack=value;lastAim=value;if(ready)StartCoroutine(SetView(value));}
        IEnumerator SetView(bool aimed)
        {
            for(int i=0;i<4;i++)
            {
                var direction=rack.transform.position+Vector3.up*1.05f-session.player.view.transform.position;
                session.player.look.SnapToDirection(direction);
                if(!aimed)session.player.look.SetYawPitch(new Vector2(session.player.look.Yaw+100,session.player.look.Pitch));
                yield return null;
            }
            session.RefreshTarget();
        }
        void LateUpdate()
        {
            if(!ready)return;
            if(aimAtRack!=lastAim){lastAim=aimAtRack;StartCoroutine(SetView(aimAtRack));}
            original.label.gameObject.SetActive(false);
            // Keep item hover identification; remove only the rack action sentence.
            hud.target.enabled=!session.TargetRack;if(session.TargetRack&&hud.targetBackdrop)hud.targetBackdrop.enabled=false;
            var active=session.Active;
            bool eligible=active&&!session.player.blocked&&rack.CanAccept(active.definition);
            bool aimed=eligible&&session.TargetRack&&session.TargetRack.CanAccept(active.definition);
            actionCaption.gameObject.SetActive(aimed);
            if(aimed)actionCaption.text=session.TargetRack.Type?"PLACE":"ASSIGN RACK";
            marker.enabled=eligible;if(!eligible)return;
            var point=rack.label?rack.label.transform.position-Vector3.up*.35f:rack.transform.position+Vector3.up*1.45f;
            var screen=OffScreenIndicatorCore.GetScreenPosition(session.player.view,point);
            var area=(RectTransform)marker.transform.parent;var canvas=marker.GetComponentInParent<Canvas>();
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area,screen,camera,out var local);
            var bounds=area.rect;var inset=Rect.MinMaxRect(bounds.xMin+70,bounds.yMin+200,bounds.xMax-360,bounds.yMax-250);
            bool offscreen=screen.z<0||!inset.Contains(local);float angle=0;
            if(offscreen)
            {
                var direction=local-inset.center;if(screen.z<0)direction=-direction;
                var projected=new Vector3(direction.x+inset.center.x,direction.y+inset.center.y,1);
                OffScreenIndicatorCore.GetArrowIndicatorPositionAndAngle(ref projected,ref angle,inset.center,inset.size*.5f);local=projected;
            }
            marker.sprite=offscreen?arrow:box;marker.rectTransform.localPosition=local;
            marker.rectTransform.localRotation=offscreen?Quaternion.Euler(0,0,angle*Mathf.Rad2Deg):Quaternion.identity;
            // Do not let even a small marker cover the model in this proposal.
            var corners=new Vector3[4];marker.rectTransform.GetWorldCorners(corners);
            var low=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var high=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            var occupied=carry.HeldScreenRect;occupied.xMin-=12;occupied.xMax+=12;occupied.yMin-=12;occupied.yMax+=12;
            if(occupied.Overlaps(Rect.MinMaxRect(low.x,low.y,high.x,high.y)))marker.enabled=false;
        }
    }
}
#endif
