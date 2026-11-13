#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using LabSorting.UI;
using LabSorting.Sorting;
using LabSorting.Player;
namespace LabSorting.Prototype
{
    public static class MinimumTouchChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("MIN TOUCH: "+text);Debug.Log("MIN TOUCH PASS: "+text);}
        static PointerEventData Hit(RectTransform rect,int id)
        {
            Canvas.ForceUpdateCanvases();var c=rect.GetComponentInParent<Canvas>();var cam=c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera;
            var e=new PointerEventData(EventSystem.current){pointerId=id,button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(cam,rect.position)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&(hits[0].gameObject.transform==rect||hits[0].gameObject.transform.IsChildOf(rect)),"UI raycast reaches "+rect.name+" at minimum size");e.pointerPressRaycast=e.pointerCurrentRaycast=hits[0];return e;
        }
        static void Tap(RectTransform rect,int id){var e=Hit(rect,id);ExecuteEvents.ExecuteHierarchy(e.pointerCurrentRaycast.gameObject,e,ExecuteEvents.pointerDownHandler);ExecuteEvents.ExecuteHierarchy(e.pointerCurrentRaycast.gameObject,e,ExecuteEvents.pointerUpHandler);ExecuteEvents.ExecuteHierarchy(e.pointerCurrentRaycast.gameObject,e,ExecuteEvents.pointerClickHandler);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var h=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var settings=h.GetComponent<SettingsTutorialPanel>();var sizes=settings.controlSizes;var actions=h.GetComponent<ContextActionHUD>();var snapshot=s.Capture();var racks=s.racks;bool motor=s.player.character.Motor.enabled;float joy=sizes.JoystickScale,action=sizes.ActionScale;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.1f,1000);floor.transform.localScale=new Vector3(20,.2f,20);
            var fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.SetActive(false);
            try
            {
                sizes.Apply(.85f,.85f);s.player.SendMessage("OnApplicationFocus",true);
                yield return DesignerLayoutChecks.Run(s);
                foreach(var b in h.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {var labels=b.GetComponentsInChildren<UnityEngine.UI.Text>(true);if(labels.Length==1&&b.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length==1&&labels[0].transform.parent==b.transform)Check(labels[0].alignment==TextAnchor.MiddleCenter,"Text-only button is centered: "+b.name);}
                var rect=sizes.joystick;var area=rect.GetComponent<LabTouchArea>();var e=Hit(rect,71);area.OnPointerDown(e);
                var canvas=rect.GetComponentInParent<Canvas>();var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                e.position=RectTransformUtility.WorldToScreenPoint(cam,rect.TransformPoint(new Vector3(rect.rect.width*.35f,0,0)));area.OnDrag(e);
                Check(s.player.Move.x>.95f,"Minimum joystick still reaches full movement");area.OnPointerUp(new PointerEventData(EventSystem.current){pointerId=72});Check(s.player.Move.x>.95f,"Second finger cannot release joystick owner");area.OnPointerUp(e);Check(s.player.Move==Vector2.zero&&area.knob.anchoredPosition==Vector2.zero,"Joystick release returns to neutral");
                area.OnPointerDown(e);area.SendMessage("OnApplicationPause",true);Check(s.player.Move==Vector2.zero,"Background cancels joystick gesture");
                s.player.character.Motor.enabled=false;s.player.character.Motor.SetPosition(new Vector3(1000,.01f,1000));s.player.look.SetYawPitch(Vector2.zero);yield return new WaitForSecondsRealtime(.4f);
                var items=s.items.Where(i=>i.location==ItemLocation.Loose).Take(2).ToArray();items[0].Snap(new Vector3(1000,.4f,1002),Quaternion.identity);Physics.SyncTransforms();s.player.look.SnapToDirection(items[0].visual.bounds.center-s.player.view.transform.position);yield return new WaitForSecondsRealtime(.3f);s.RefreshTarget();actions.Refresh();
                Check(s.TargetItem==items[0],"Isolated item is targeted through actual world raycast");Tap((RectTransform)actions.pick.transform,81);yield return new WaitForSecondsRealtime(.4f);Check(s.carried.Count==1&&s.Active==items[0]&&!s.Holding,"Minimum pick tap collects exactly one and releases hold");
                fixture.transform.position=s.player.view.transform.position+s.player.view.transform.forward*2;fixture.transform.localScale=new Vector3(2,2,.15f);var rack=fixture.AddComponent<SortRack>();rack.id="minimum-touch-rack";rack.slots=new[]{fixture.transform,fixture.transform};rack.neutral=racks[0].neutral;rack.valid=racks[0].valid;rack.invalid=racks[0].invalid;rack.Initialize();s.racks=new[]{rack};fixture.SetActive(true);Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.25f);s.RefreshTarget();actions.Refresh();Check(s.TargetRack==rack,"Actual raycast targets place fixture");
                Tap((RectTransform)actions.place.transform,82);yield return new WaitForSecondsRealtime(.4f);Check(s.carried.Count==0&&rack.Count==1&&!s.Holding,"Minimum place tap places once");
                fixture.SetActive(false);items[1].Snap(new Vector3(1000,.4f,1001.5f),Quaternion.identity);Physics.SyncTransforms();s.player.look.SnapToDirection(items[1].visual.bounds.center-s.player.view.transform.position);yield return new WaitForSecondsRealtime(.3f);s.RefreshTarget();actions.Refresh();Tap((RectTransform)actions.pick.transform,83);yield return new WaitForSecondsRealtime(.4f);actions.Refresh();Check(s.Active==items[1],"Second item picked through minimum control");Tap((RectTransform)actions.drop.transform,84);yield return new WaitForSecondsRealtime(.7f);Check(s.carried.Count==0&&items[1].location==ItemLocation.Loose,"Minimum drop button releases one real item");
                SortingSmokeChecks.Capture(s,"Logs/MinimumTouchControls.png");
            }
            finally{s.CancelHold();s.racks=racks;DG.Tweening.DOTween.CompleteAll();s.Restore(snapshot);s.player.character.Motor.enabled=motor;sizes.Apply(joy,action);UnityEngine.Object.Destroy(fixture);UnityEngine.Object.Destroy(floor);}
        }
    }
}
#endif
