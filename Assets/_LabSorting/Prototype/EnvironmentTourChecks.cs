#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.UI;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class EnvironmentTourChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("TOUR: "+text);Debug.Log("TOUR PASS: "+text);}
        public static IEnumerator Run()
        {
            var tour=UnityEngine.Object.FindFirstObjectByType<FirstLaunchTour>();var s=tour.session;var guided=tour.GetComponent<GuidedTutorial>();int cash=s.Cash;var playerPosition=s.player.transform.position;int count=s.items.Length;
            try
            {
                yield return null;
                Check(tour.IsRunning&&s.player.blocked&&!guided.IsRunning,"First launch automatically starts before tutorial and blocks input");
                Check(tour.brain.enabled&&tour.tourCamera.gameObject.activeSelf&&tour.gameplayUI.alpha==0,"Cinemachine owns camera and gameplay HUD hidden");
                var initial=s.player.view.transform.position;yield return new WaitForSecondsRealtime(.7f);Check(Vector3.Distance(initial,s.player.view.transform.position)>.01f,"Live camera advances through the tour");
                // The character's first physics tick resolves its existing spawn overlap with the rack.
                // Measure tour drift after that normal startup correction, not before any physics has run.
                playerPosition=s.player.transform.position;
                tour.SendMessage("OnApplicationPause",true);float elapsed=tour.Elapsed;yield return new WaitForSecondsRealtime(.25f);Check(Mathf.Approximately(elapsed,tour.Elapsed),"Background freezes tour time");tour.SendMessage("OnApplicationPause",false);
                int itemLayer=LayerMask.NameToLayer("SortingItem");int mask=~(1<<itemLayer);Physics.SyncTransforms();
                for(float time=0;time<tour.duration+tour.handoffDuration;time+=.05f)
                {
                    tour.SamplePose(time,out var p,out var q);
                    var hit=Physics.OverlapSphere(p,.14f,mask,QueryTriggerInteraction.Ignore).FirstOrDefault(c=>!c.transform.IsChildOf(s.player.transform));
                    Check(!hit,"Camera clearance at "+time.ToString("F2")+(hit?" hit "+hit.name:""));
                }
                for(int i=0;i<4;i++)
                {
                    float target=i*tour.duration/3+.6f;
                    while(tour.IsRunning&&tour.Elapsed<target)yield return null;
                    if(tour.IsRunning)Capture(tour,"Logs/EnvironmentTour-"+i+".png");
                }
                float deadline=Time.realtimeSinceStartup+20;while(tour.IsRunning&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(!tour.IsRunning&&tour.Completed&&!s.player.blocked,"Natural completion persists once and releases control");
                Check(!tour.brain.enabled&&!tour.tourCamera.gameObject.activeSelf&&s.player.look.enabled,"Cinemachine releases camera to original look rig");
                Check(guided.IsRunning,"Gameplay tutorial begins after tour");guided.Skip();
                Check(!tour.BeginTour(),"Completed tour does not restart");
                Check(Vector3.Distance(s.player.transform.position,playerPosition)<.1f&&s.Cash==cash&&s.items.Length==count,"Tour leaves player position, wallet and 4032 items intact: position "+playerPosition+" -> "+s.player.transform.position+", cash "+cash+" -> "+s.Cash+", items "+count+" -> "+s.items.Length);
                PlayerPrefs.DeleteKey(tour.completionKey);Check(tour.BeginTour(),"Fresh preference allows test skip");yield return new WaitForSecondsRealtime(.3f);
                tour.overlay.transform.Find("SafeArea/Skip").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();tour.Skip();
                while(tour.brain.enabled)yield return null;
                Check(tour.IsRunning&&s.player.blocked&&tour.curtain.alpha>.9f,"Camera switches behind black curtain while input remains blocked");
                yield return new WaitForSecondsRealtime(tour.handoffDuration);
                Check(tour.Completed&&!tour.IsRunning&&!s.player.blocked&&!tour.overlay.activeSelf,"Repeated skip is safe and persists completion");
                PlayerPrefs.DeleteKey(tour.completionKey);Check(tour.BeginTour(),"Interruption fixture starts");tour.enabled=false;
                Check(!tour.Completed&&!tour.IsRunning&&!s.player.blocked&&s.player.look.enabled,"Interrupted component restores control without marking completion");tour.enabled=true;
                s.player.SetBlocked(true);Check(!tour.BeginTour()&&s.player.blocked,"Existing modal input block is preserved");s.player.SetBlocked(false);
            }
            finally{tour.enabled=false;PlayerPrefs.DeleteKey("LabSorting.TestTour.Done");PlayerPrefs.DeleteKey("LabSorting.TestTour.GuidedDone");PlayerPrefs.DeleteKey("LabSorting.TestTour.GuidedStep");PlayerPrefs.Save();}
        }
        static void Capture(FirstLaunchTour tour,string path)
        {
            var canvas=tour.overlay.GetComponent<Canvas>();var mode=canvas.renderMode;var camera=canvas.worldCamera;float plane=canvas.planeDistance;
            try{canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=tour.session.player.view;canvas.planeDistance=tour.session.player.view.nearClipPlane+.02f;SortingSmokeChecks.Capture(tour.session,path);}
            finally{canvas.renderMode=mode;canvas.worldCamera=camera;canvas.planeDistance=plane;}
        }
    }
}
#endif
