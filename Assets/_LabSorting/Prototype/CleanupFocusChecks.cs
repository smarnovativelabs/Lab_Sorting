#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class CleanupFocusChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("CLEANUP FOCUS: "+text);Debug.Log("CLEANUP FOCUS PASS: "+text);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var original=s.Capture();var panel=UnityEngine.Object.FindFirstObjectByType<ItemsProgressPanel>();
            try
            {
                s.player.SendMessage("OnApplicationFocus",true);Check(s.items.Length==4032,"Full designer catalog");
                Check(s.ShortGoalFocus().Contains("new items"),"Initial goal keeps simple sorting instruction");
                var checkpoint=s.Capture();checkpoint.rewarded=s.items.Take(25).Select(i=>i.id).ToList();Check(s.Restore(checkpoint),"Existing unique-item reward history loads");
                var item=s.items.First(i=>s.racks.Any(r=>r.CapacityFor(i.definition)>1&&r.CapacityFor(i.definition)<=s.ItemsOfType(i.definition).Count()));
                var rack=s.racks.First(r=>r.CapacityFor(item.definition)>1&&r.CapacityFor(item.definition)<=s.ItemsOfType(item.definition).Count());
                Check(s.TryPick(item),"Focus fixture uses actual pickup");DG.Tweening.DOTween.CompleteAll();Check(s.TryPlace(rack),"Focus fixture uses actual placement");DG.Tweening.DOTween.CompleteAll();
                Check(s.ShortGoalFocus().StartsWith("Focus: finish a rack"),"Rack focus only suggests a rack with enough remaining items");
                string focus=s.ShortGoalFocus();int cash=s.Cash;var saved=s.Capture();Check(s.Restore(saved)&&s.ShortGoalFocus()==focus&&s.Cash==cash,"Focus survives save restoration without new rewards");
                saved.rewarded=s.items.Take(50).Select(i=>i.id).ToList();Check(s.Restore(saved),"Next reward batch restores");Check(s.ShortGoalFocus().StartsWith("Focus: ")&&!s.ShortGoalFocus().Contains("finish a rack"),"Third batch suggests an available item category");
                yield return null;SortingSmokeChecks.Capture(s,"Logs/ContextualGoal.png");
                s.Restore(original);
                // Synchronous presentation fixture: no frames/saves run with synthetic rack locations.
                var last=s.items.Skip(s.items.Length-15).ToArray();
                foreach(var i in s.items)i.location=ItemLocation.Rack;foreach(var i in last)i.location=ItemLocation.Loose;
                Check(s.ShortGoalFocus()=="ALMOST DONE!  15 left to sort","Cleanup activates at fifteen remaining");
                panel.Open();panel.Refresh();Check(panel.heading.text=="ALMOST DONE!"&&panel.rows[0].text==last[0].definition.displayName,"Remaining types appear on first page ahead of completed types");
                Check(s.Cash==original.cash&&s.SearchCredits==original.searchCredits,"Cleanup guidance does not spend cash or search supplies");
                foreach(var i in last)i.location=ItemLocation.Rack;panel.Refresh();Check(panel.heading.text=="ALL ITEMS","No almost-done prompt after completion");
                panel.Close();s.Restore(original);
            }
            finally{if(panel.IsOpen)panel.Close();s.Restore(original);}
        }
    }
}
#endif
