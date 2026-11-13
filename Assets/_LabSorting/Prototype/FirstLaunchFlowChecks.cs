#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class FirstLaunchFlowChecks
    {
        const string Prefix="LabSorting.FlowTest.";
        static void Check(bool ok,string text){if(!ok)throw new Exception("FIRST FLOW: "+text);Debug.Log("FIRST FLOW PASS: "+text);}
        public static void Cleanup(){foreach(string key in new[]{"Tour","GuidedDone","GuidedStep","GuidedToolPage","RackTurnShown.v1"})PlayerPrefs.DeleteKey(Prefix+key);}
        public static void Configure()
        {
            Cleanup();var tour=UnityEngine.Object.FindFirstObjectByType<FirstLaunchTour>();tour.autoPlay=true;tour.completionKey=Prefix+"Tour";
            tour.settings.autoTutorial=false;tour.settings.preferencesPrefix=Prefix;
            tour.GetComponent<GuidedTutorial>().preferencesPrefix=Prefix;
            typeof(FirstLaunchTour).GetField("tutorialAfterTour",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(tour,true);
        }
        public static IEnumerator Run()
        {
            var tour=UnityEngine.Object.FindFirstObjectByType<FirstLaunchTour>();var s=tour.session;var g=tour.GetComponent<GuidedTutorial>();var sound=tour.GetComponent<LabSoundscape>();
            tour.SendMessage("OnApplicationFocus",true);sound.SendMessage("OnApplicationFocus",true);s.player.SendMessage("OnApplicationFocus",true);
            float deadline=Time.realtimeSinceStartup+8;while(!tour.IsRunning&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(tour.IsRunning&&!g.IsRunning&&s.player.blocked,"Loading hands control to tour before tutorial");
            Check(tour.gameplayUI.alpha==0,"Gameplay panels hidden throughout environment introduction");
            var services=tour.GetComponent<ServicePresentation>();var internet=LabSorting.Services.LabServiceRoot.Instance.internet;
            internet.enabled=false;services.automaticConnectivity=true;
            var status=typeof(LabSorting.Services.InternetConnectivity).GetMethod("SetStatus",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            status.Invoke(internet,new object[]{LabSorting.Services.ConnectivityStatus.Offline});yield return null;
            float frozen=tour.Elapsed;yield return new WaitForSecondsRealtime(.3f);
            Check(services.offlineRoot.activeSelf&&s.player.NetworkBlocked&&Mathf.Approximately(frozen,tour.Elapsed),"Disconnect freezes tour behind mandatory connection gate");
            status.Invoke(internet,new object[]{LabSorting.Services.ConnectivityStatus.Online});yield return null;
            Check(!services.offlineRoot.activeSelf&&tour.IsRunning&&s.player.blocked,"Reconnect preserves tour ownership of controls");
            tour.SendMessage("OnApplicationPause",true);sound.SendMessage("OnApplicationPause",true);s.SendMessage("OnApplicationPause",true);
            frozen=tour.Elapsed;yield return new WaitForSecondsRealtime(.25f);
            Check(Mathf.Approximately(frozen,tour.Elapsed)&&!sound.music.isPlaying,"Background freezes tour and pauses music");
            tour.SendMessage("OnApplicationPause",false);sound.SendMessage("OnApplicationPause",false);s.SendMessage("OnApplicationPause",false);
            int music=sound.music.GetInstanceID();float began=Time.realtimeSinceStartup;deadline=began+tour.duration+tour.handoffDuration+8;
            while(tour.IsRunning&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!tour.IsRunning&&g.IsRunning&&!s.player.blocked,"Tour naturally hands control to tutorial without an extra gate");
            Check(sound.music.GetInstanceID()==music&&sound.music.isPlaying&&sound.music.volume<=.1f,"Same quiet music continues through tour handoff");
            Check(!GameObject.Find("Loading Scene Fade")&&!tour.overlay.activeSelf,"No stale fade overlay covers tutorial");
            var item=s.items.Where(i=>i.location==ItemLocation.Loose).OrderBy(i=>(i.transform.position-s.player.transform.position).sqrMagnitude).First();
            Check(s.TryPick(item),"Actual item pickup begins rack lesson");DG.Tweening.DOTween.CompleteAll();yield return new WaitForSecondsRealtime(.3f);
            Check(g.Current==GuidedTutorial.Step.Place,"Tutorial responds to real pickup");
            var rack=MatchingRackGuide.FindRack(s,item.definition);Check(rack&&s.TryPlace(rack),"Actual rack placement completes core sorting lesson");DG.Tweening.DOTween.CompleteAll();yield return new WaitForSecondsRealtime(.2f);
            int cash=s.Cash,search=s.SearchCredits,magnet=s.MagnetCredits,capacity=s.capacity;
            for(int page=0;page<3;page++)
            {
                yield return new WaitForSecondsRealtime(.25f);
                Check(g.Current==GuidedTutorial.Step.Tools&&g.ToolPage==page,"Tool explanation sequence "+page);
                s.SendMessage("OnApplicationPause",true);s.SendMessage("OnApplicationPause",false);g.Begin(false);
                Check(g.Current==GuidedTutorial.Step.Tools&&g.ToolPage==page,"Resume restores the same tool explanation");
                Canvas.ForceUpdateCanvases();Check(g.ToolModalActive&&s.player.TutorialBlocked&&!g.hand.gameObject.activeSelf&&!g.skipButton.gameObject.activeSelf,"Explanation blocks gameplay, hides SKIP and hand"); var menusDuring=g.GetComponent<DesignerMenus>();menusDuring.OpenStore();Check(!menusDuring.IsOpen,"Store cannot interrupt tool explanation"); Check(g.group.transform.Find("ToolExplanationBlocker").GetComponent<UnityEngine.UI.Image>().raycastTarget,"Backdrop consumes other touches");
                var target=page==0?g.searchButton:page==1?g.magnetButton:g.capacityButton;var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,target.position)};UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.name=="ToolExplanationBlocker","Actual tool tap is intercepted by modal backdrop");pointer.position=RectTransformUtility.WorldToScreenPoint(null,g.gotItButton.transform.position);hits.Clear();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(g.gotItButton.transform),"NEXT remains touchable above the blocker"); Check(!g.GetComponent<TaskCardLayout>().card.gameObject.activeSelf,"Task card hidden during tutorial explanation");
                Check(s.Cash==cash&&s.SearchCredits==search&&s.MagnetCredits==magnet&&s.capacity==capacity,"Tool guidance never spends supplies");
                SortingSmokeChecks.Capture(s,"Logs/FirstFlow-Tool-"+page+".png");yield return new WaitForSecondsRealtime(.15f);g.GotIt();g.GotIt();g.gotItButton.onClick.Invoke();Check(page==2?g.Current==GuidedTutorial.Step.Done:g.ToolPage==page+1,"Rapid repeated NEXT advances only one explanation");
            }
            yield return new WaitForSecondsRealtime(5.2f);
            Check(!g.IsRunning&&g.Completed&&tour.Completed&&!s.player.blocked,"Tutorial ends with remembered completion and usable controls");
            Check(s.SortedCount==1&&s.items.Length==4032,"Full flow preserves item count and actual sorted progress");
            Check(!tour.BeginTour(),"Returning player does not repeat completed tour");
            var menus=g.GetComponent<DesignerMenus>();Check(!menus.pauseRoot.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text.ToUpperInvariant().Contains("MAIN MENU")),"Pause has no main-menu navigation");
            SortingSmokeChecks.Capture(s,"Logs/FirstFlow-Gameplay.png");
        }
    }
}
#endif
