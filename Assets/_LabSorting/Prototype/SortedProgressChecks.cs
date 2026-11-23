#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;

namespace LabSorting.Prototype
{
    public sealed class SortedProgressChecks : MonoBehaviour
    {
        readonly StringBuilder report=new StringBuilder();
        public bool Finished {get;private set;}
        void Check(bool value,string message)
        {
            report.AppendLine((value?"PASS ":"FAIL ")+message);Directory.CreateDirectory("Logs/SortedProgress");
            File.WriteAllText("Logs/SortedProgress/report.txt",report.ToString());
            if(!value)throw new Exception("SORTED PROGRESS: "+message);
        }
        static IEnumerator Wait(float seconds)
        {float end=Time.time+seconds,limit=Time.realtimeSinceStartup+20;while(Time.time<end){if(Time.realtimeSinceStartup>limit)throw new Exception("Clock stopped");yield return null;}}
        public IEnumerator Run()
        {
            Application.runInBackground=true;
            var s=FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var hud=FindFirstObjectByType<SortingHUD>();while(!hud.GetComponent<SortedProgressFeedback>())yield return null;
            var counter=hud.GetComponent<SortedProgressFeedback>();
            s.persistence=false;s.player.SetBlocked(false);s.player.SetTutorialBlocked(false);s.player.SetNetworkBlocked(false);s.player.SendMessage("OnApplicationFocus",true);
            var snapshot=s.Capture();var rack=s.racks.First(r=>!r.ratsOnly&&r.SizeBased);
            var items=s.items.Where(i=>i.definition.name=="hand-sanitizer-bottles-1"&&i.location==ItemLocation.Loose).Take(10).ToArray();
            int baseline=s.SortedCount;
            Action reset=()=>{s.player.SetBlocked(false);s.Restore(snapshot);counter.Synchronize();};
            try
            {
                Check(counter.DisplayedCount==baseline,"Initial display matches real saved count");
                s.TryPick(items[0]);items[0].CompleteMotion();Check(s.TryPlace(rack),"Single placement accepted");
                Check(s.SortedCount==baseline+1&&counter.DisplayedCount==baseline&&counter.PendingCount==1,"Data commits immediately while display waits");
                yield return Wait(.4f);
                Check(counter.IsAnimating&&counter.ActiveAmount==1&&counter.DisplayedCount==baseline,"Arrival starts +1 without incrementing the number early");
                Check(counter.IncrementRect&&counter.IncrementRect.parent==hud.progress.transform.parent,"Increment uses progress HUD coordinate space");
                var start=counter.IncrementRect.position;
                yield return Wait(.16f);
                Check(counter.IncrementRect.position.y>start.y,"Increment floats upward toward the badge");
                ScreenCapture.CaptureScreenshot("Logs/SortedProgress/increment.png");
                yield return Wait(.45f);
                Check(counter.DisplayedCount==baseline+1&&counter.PendingCount==0&&!counter.IsAnimating,"Merge increments exactly once");
                Check(hud.progress.text.Contains("SORTED  "+UIFormat.Count(baseline+1)),"Sorted label displays the merged count");
                float expected=(float)(baseline+1)/s.items.Length;
                Check(Mathf.Abs(hud.progressFill.anchorMax.x-expected)<.00001f,"Progress fill catches up to merged count");
                Check(!FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Any(t=>t.name=="PlusOne"),"Old rack-side +1 is removed");

                reset();foreach(var item in items){Check(s.TryPick(item),"Collect batch item");item.CompleteMotion();}
                Check(s.PlaceMatching(rack),"Batch placement accepted");
                Check(counter.DisplayedCount==baseline&&counter.PendingCount==10,"Batch reserves ten increments without jumping display");
                yield return Wait(.7f);Check(counter.DisplayedCount<s.SortedCount,"Count remains behind data until increments merge");
                yield return Wait(2.4f);
                Check(counter.DisplayedCount==baseline+10&&counter.PendingCount==0&&!counter.IsAnimating,"Batch merges all ten with no loss or duplication");
                Check(counter.LargestIncrement>1,"Rapid arrivals coalesce into +N");
                Check(FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(t=>t.name=="SortedCountIncrement")==1,"Only one reusable floating increment exists");

                reset();s.TryPick(items[0]);items[0].CompleteMotion();s.TryPlace(rack);yield return Wait(.45f);
                s.player.SetBlocked(true);yield return null;yield return null;
                Check(counter.DisplayedCount==s.SortedCount&&counter.PendingCount==0&&!counter.IsAnimating&&!counter.IncrementRect.gameObject.activeSelf,"Pause/modal clears pending animation and synchronizes count");
                s.player.SetBlocked(false);yield return Wait(.6f);Check(counter.DisplayedCount==baseline+1,"Resume does not replay +1");

                reset();s.TryPick(items[0]);items[0].CompleteMotion();s.TryPlace(rack);yield return Wait(.45f);
                var midflight=s.Capture();File.WriteAllText("Logs/SortedProgress/save.json",JsonUtility.ToJson(midflight));
                Check(s.Restore(JsonUtility.FromJson<SortingSession.Snapshot>(File.ReadAllText("Logs/SortedProgress/save.json"))),"Restore saved placement during counter flight");
                yield return null;yield return null;
                Check(counter.DisplayedCount==s.SortedCount&&counter.PendingCount==0&&!counter.IsAnimating,"Load synchronizes to saved count without replay");
                yield return Wait(.6f);Check(counter.DisplayedCount==baseline+1,"Cancelled pre-load tween cannot increment later");

                reset();s.TryPick(items[0]);items[0].CompleteMotion();s.TryPlace(rack);yield return Wait(.45f);
                Check(s.TryPick(items[0]),"Retrieve placed item before +1 merges");
                yield return Wait(.6f);Check(counter.DisplayedCount==baseline&&counter.PendingCount==0&&!counter.IsAnimating,"Retrieval cancels stale increment without overshooting");

                reset();s.TryPick(items[0]);items[0].CompleteMotion();s.TryPlace(rack);yield return Wait(.45f);
                counter.enabled=false;Check(counter.DisplayedCount==s.SortedCount&&!counter.IsAnimating,"Disable cancels tween and synchronizes");
                counter.enabled=true;yield return Wait(.6f);Check(counter.DisplayedCount==baseline+1&&counter.PendingCount==0,"Re-enable does not duplicate increments");

                reset();s.TryPick(items[0]);items[0].CompleteMotion();s.TryPlace(rack);s.SendMessage("OnApplicationPause",true);yield return null;
                Check(counter.DisplayedCount==s.SortedCount&&counter.PendingCount==0&&!counter.IsAnimating,"Background during item flight synchronizes even without arrival event");
                Check(hud.progress.transform.localScale==Vector3.one,"Number pulse restores exact original scale");
                Finished=true;Check(true,"ALL_SORTED_PROGRESS_CHECKS_PASS");
            }
            finally{counter.enabled=true;reset();s.InitializeSaveCache();}
        }
    }
}
#endif
