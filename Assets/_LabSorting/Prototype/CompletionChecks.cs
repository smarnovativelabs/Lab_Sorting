using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class CompletionChecks
    {
        static IEnumerator Wait(float seconds){float end=Time.unscaledTime+seconds,limit=Time.realtimeSinceStartup+120;while(Time.unscaledTime<end){if(Time.realtimeSinceStartup>limit)throw new Exception("Completion test frame clock stopped");yield return null;}}
        static IEnumerator Until(Func<bool> done){float limit=Time.realtimeSinceStartup+120;while(!done()&&Time.realtimeSinceStartup<limit)yield return null;}
        public static IEnumerator Run(SortingSession s,Action<bool,string> check,Action<SortingSession,string> capture)
        {
            var p=UnityEngine.Object.FindFirstObjectByType<LabCompletionPanel>();var progress=UnityEngine.Object.FindFirstObjectByType<ItemsProgressPanel>();var settings=UnityEngine.Object.FindFirstObjectByType<SettingsTutorialPanel>();var initial=s.Capture();bool persistence=s.persistence,auto=p.autoShow;
            try
            {
                s.persistence=false;p.autoShow=true;check(p.root&&p.card&&p.continueButton&&p.confetti.Length>0,"Lab completion summary is wired");yield return Wait(.4f);check(!p.IsOpen&&!s.CompletionAcknowledged,"Incomplete lab never opens completion summary");int cash=s.Cash;
                for(int i=0;i<s.items.Length;i++){s.TryPick(s.items[i]);DOTween.CompleteAll();s.TryPlace(s.racks[i%5]);if(i<s.items.Length-1)DOTween.CompleteAll();}
                check(!p.IsOpen&&s.items[s.items.Length-1].Moving,"Summary waits for final placement instead of interrupting its animation");yield return Until(()=>p.IsOpen);check(p.IsOpen&&s.player.blocked&&!s.items.Any(i=>i.Moving),"Full lab opens summary after placement finishes and blocks gameplay");
                int expected=cash+s.items.Length*s.placementReward+s.firstTenBonus+s.categoryBonus+s.halfBonus+s.completeBonus+Mathf.CeilToInt(s.items.Length/(float)SortingSession.ShortGoalSize)*SortingSession.ShortGoalBonus+s.CompletedRackCount*SortingSession.RackBonus;
                check(s.Cash==expected,"Completion UI adds no second cash reward");check(p.summary.text.Contains("50 / 50")&&p.summary.text.Contains("5 categories")&&p.summary.text.Contains("4 milestones"),"Summary reflects current lab totals and milestones");check(p.summary.preferredHeight<=p.summary.rectTransform.rect.height,"Completion summary fits its card");capture(s,"Logs/LabCompletion.png");
                check(!s.TryPick(s.items[0])&&!s.TrySearch()&&!s.TryUpgradeCapacity()&&s.Cash==expected,"Completion modal blocks sorting and paid actions");settings.OpenSettings();progress.Open();check(!settings.IsOpen&&!progress.IsOpen&&p.IsOpen,"Other panels cannot nest over completion");
                var complete=s.Capture();check(!complete.completionAcknowledged,"Showing summary does not acknowledge it prematurely");s.persistence=true;s.Save();s.Restore(initial);check(s.Load(),"Completed unacknowledged lab loads from disk");yield return Until(()=>p.IsOpen);check(p.IsOpen&&!s.CompletionAcknowledged&&s.Cash==expected,"Interrupted completion summary returns without paying again");
                p.continueButton.onClick.Invoke();check(!p.IsOpen&&!s.player.blocked&&s.CompletionAcknowledged&&s.SortedCount==s.items.Length,"Continue Exploring keeps same sorted lab and restores controls");s.Restore(initial);check(s.Load()&&s.CompletionAcknowledged,"Continue acknowledgement persists to disk");yield return Wait(.6f);check(!p.IsOpen,"Acknowledged summary does not repeat after reload");
                s.persistence=false;s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryPlace(s.racks[0]);DOTween.CompleteAll();yield return Wait(.6f);check(!p.IsOpen&&s.Cash==expected,"Re-sorting after Continue creates no repeat summary or reward");
                s.Restore(initial);progress.Open();s.Restore(complete);yield return Wait(.6f);check(progress.IsOpen&&!p.IsOpen,"Completion waits while another modal is open");progress.Close();yield return Until(()=>p.IsOpen);check(p.IsOpen&&s.player.blocked,"Deferred completion opens after other modal closes");
                s.Restore(initial);check(!p.IsOpen&&!s.player.blocked&&!s.CompletionAcknowledged,"Restoring incomplete state closes summary and releases controls");
                string oldJson=JsonUtility.ToJson(complete).Replace("\"completionAcknowledged\":false,","");check(s.Restore(JsonUtility.FromJson<SortingSession.Snapshot>(oldJson)),"Older version-3 save without acknowledgement field still loads");yield return Until(()=>p.IsOpen);check(p.IsOpen&&s.Cash==expected,"Older completed save shows summary once without retroactive reward");
                p.enabled=false;check(!p.IsOpen&&!s.player.blocked,"Disabling completion UI safely releases input");p.enabled=true;yield return Until(()=>p.IsOpen);check(p.IsOpen,"Unacknowledged completion survives UI re-enable");p.continueButton.onClick.Invoke();
            }
            finally{p.autoShow=auto;p.enabled=true;progress.Close();settings.Close();s.Restore(initial);s.player.SetBlocked(false);s.persistence=persistence;}
        }
    }
}

