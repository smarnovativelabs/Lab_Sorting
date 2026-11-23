using LabSorting.Sorting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LabSorting.UI
{
    public sealed partial class GuidedTutorial
    {
        SortItem pickGuideTarget;
        Image pickGuideBlocker;
        Button pickGuideButton;
        bool pickGuideReady, pickGuideLocked;
        CursorLockMode pickGuideCursor;
        bool pickGuideCursorVisible;
        float pickGuideStarted, nextPickGuideAttempt;

        bool PickGuideCandidate(SortItem item)
        {
            if(!item||!item.gameObject.activeInHierarchy||item.location!=ItemLocation.Loose||item.Moving||item.PickLocked||!item.hitbox||!item.hitbox.enabled)return false;
            Vector3 delta=item.hitbox.bounds.center-session.player.view.transform.position;
            if(delta.magnitude>session.reach||delta.sqrMagnitude<.01f)return false;
            float pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
            if(Mathf.Abs(pitch)>75)return false;
            return Physics.Raycast(session.player.view.transform.position,delta.normalized,out var hit,delta.magnitude+.05f,~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<SortItem>()==item;
        }

        bool UpdatePickGuide()
        {
            if(!pickButton||!group||!hand||Time.unscaledTime<nextPickGuideAttempt)return false;
            if(pickGuideTarget&&!PickGuideCandidate(pickGuideTarget))HidePickGuide();
            if(!pickGuideTarget)
            {
                float best=float.MaxValue;
                foreach(var item in session.items)
                {
                    if(!PickGuideCandidate(item))continue;
                    float distance=(item.hitbox.bounds.center-session.player.view.transform.position).sqrMagnitude;
                    if(distance<best){best=distance;pickGuideTarget=item;}
                }
                // Keep walking available when there is nothing reachable; never lock an impossible pickup.
                if(!pickGuideTarget)return false;
                session.CancelHold();
                session.player.SetPickTutorialLocked(true);
                session.player.look.AcquireLookLock(this);
                pickGuideLocked=true;pickGuideStarted=Time.unscaledTime;
                pickGuideCursor=Cursor.lockState;pickGuideCursorVisible=Cursor.visible;
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
                CreatePickGuideUI();
                pickGuideBlocker.gameObject.SetActive(true);
                group.transform.SetAsLastSibling();pickGuideBlocker.transform.SetAsLastSibling();
                hand.SetAsLastSibling();
            }
            var look=session.player.look;
            Vector3 direction=pickGuideTarget.hitbox.bounds.center-session.player.view.transform.position;
            float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
            float speed=120*Mathf.Min(Time.unscaledDeltaTime,.05f);
            look.SetYawPitch(new Vector2(Mathf.MoveTowardsAngle(look.Yaw,yaw,speed),Mathf.MoveTowards(look.Pitch,pitch,speed)));
            session.RefreshTarget();
            pickGuideReady=Mathf.Abs(Mathf.DeltaAngle(look.Yaw,yaw))<1&&Mathf.Abs(look.Pitch-pitch)<1&&session.TargetItem==pickGuideTarget;
            // Camera rigs can lag behind the look target. Release rather than trap the player if aiming fails.
            if(!pickGuideReady&&Time.unscaledTime-pickGuideStarted>5){HidePickGuide();nextPickGuideAttempt=Time.unscaledTime+3;return false;}
            var rect=(RectTransform)pickGuideButton.transform;
            rect.position=pickButton.position;
            rect.sizeDelta=new Vector2(pickButton.rect.width*pickButton.lossyScale.x/rect.parent.lossyScale.x,pickButton.rect.height*pickButton.lossyScale.y/rect.parent.lossyScale.y);
            pickGuideButton.interactable=pickGuideReady;
            if(pickGuideReady)Tap(rect);else SetHand(HandMode.Hidden);
            return true;
        }

        void CreatePickGuideUI()
        {
            if(pickGuideBlocker)return;
            pickGuideBlocker=new GameObject("Pick Tutorial Blocker",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            pickGuideBlocker.transform.SetParent(group.transform,false);
            var rect=pickGuideBlocker.rectTransform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            pickGuideBlocker.color=new Color(0,0,0,.18f);pickGuideBlocker.raycastTarget=true;
            pickGuideButton=new GameObject("Tutorial PICK",typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<Button>();
            pickGuideButton.transform.SetParent(rect,false);
            var source=pickButton.GetComponent<Image>();var face=pickGuideButton.GetComponent<Image>();
            face.color=source?source.color:new Color(.1f,.65f,.35f);if(source){face.sprite=source.sprite;face.type=source.type;}
            pickGuideButton.targetGraphic=face;pickGuideButton.navigation=new Navigation{mode=Navigation.Mode.None};
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(pickGuideButton.transform,false);label.font=message.font;label.text="PICK";label.fontSize=28;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            pickGuideButton.onClick.AddListener(()=>
            {
                if(!IsRunning||Current!=Step.Pick||!pickGuideReady||session.player.blocked)return;
                session.RefreshTarget();
                if(session.TargetItem==pickGuideTarget)session.PickTarget();
            });
        }

        void HidePickGuide()
        {
            if(pickGuideBlocker)pickGuideBlocker.gameObject.SetActive(false);
            if(pickGuideLocked)
            {
                session.player.SetPickTutorialLocked(false);session.player.look.ReleaseLookLock(this);
                session.CancelHold();Cursor.lockState=pickGuideCursor;Cursor.visible=pickGuideCursorVisible;
            }
            pickGuideLocked=false;pickGuideReady=false;pickGuideTarget=null;
        }
    }
}
