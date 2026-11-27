#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using DG.Tweening;
using LabSorting.Services;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    // Gameplay telemetry: run funnel checkpoints, out-of-supplies, tutorial steps, store opens and session length.
    public static class TelemetryChecks
    {
        const string Folder="Logs/Telemetry";
        static void Check(bool ok,string text){File.AppendAllText(Folder+"/checks.txt",(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception("TELEMETRY: "+text);}
        static void Settle(SortItem item){DOTween.CompleteAll();if(item)item.CompleteMotion();}
        public static IEnumerator Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/checks.txt","");
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var saved=s.Capture();bool persistence=s.persistence;s.persistence=false;
            var gates=UnityEngine.Object.FindObjectsByType<ServicePresentation>(FindObjectsSortMode.None);var gateStates=gates.Select(g=>g.enabled).ToArray();foreach(var gate in gates)gate.enabled=false;
            var seen=new List<string>();Action<string> observe=name=>seen.Add(name);LabTelemetry.Observed+=observe;
            var telemetry=UnityEngine.Object.FindFirstObjectByType<LabTelemetry>();GameObject temporary=null;
            if(!telemetry){temporary=new GameObject("Telemetry check");telemetry=temporary.AddComponent<LabTelemetry>();}
            telemetry.BindSession(s);
            var tutorial=UnityEngine.Object.FindFirstObjectByType<GuidedTutorial>();string prefix=tutorial?tutorial.preferencesPrefix:"LabSorting.";
            var prefKeys=new[]{prefix+"GuidedDone",prefix+"GuidedStep",prefix+"GuidedToolPage"};
            var prefs=prefKeys.ToDictionary(k=>k,k=>PlayerPrefs.HasKey(k)?(int?)PlayerPrefs.GetInt(k):null);
            Action unblock=()=>{s.player.SetNetworkBlocked(false);s.player.SetTutorialBlocked(false);s.player.SetBlocked(false);s.player.SetPickTutorialLocked(false);};
            try
            {
                unblock();
                // items_sorted: once when the run reaches a checkpoint (10), never between checkpoints or on a re-placement.
                var loose=s.items.Where(i=>i.location==ItemLocation.Loose&&!i.definition.isRat).ToList();
                ItemDefinition type=null;SortRack rack=null;
                foreach(var group in loose.GroupBy(i=>i.definition).Where(g=>g.Count()>=2))
                {
                    rack=s.racks.FirstOrDefault(r=>r.gameObject.activeInHierarchy&&r.CanAccept(group.Key)&&(r.Type?r.Capacity:r.CapacityFor(group.Key))-r.Count>=2);
                    if(rack){type=group.Key;break;}
                }
                Check(type&&rack,"Fixture: a type with two loose items and a rack with room");
                var pair=loose.Where(i=>i.definition==type).Take(2).ToArray();
                var data=s.Capture();data.rewarded=s.items.Where(i=>!pair.Contains(i)).Take(9).Select(i=>i.id).ToList();
                Check(s.Restore(data),"Fixture: nine unique items already sorted");unblock();
                seen.Clear();Check(s.TryPick(pair[0]),"Pick the tenth item");Settle(pair[0]);
                Check(s.TryPlace(rack),"Place the tenth unique item");
                Check(seen.Count(n=>n=="items_sorted")==1,"items_sorted fires once at checkpoint 10: "+string.Join(",",seen));
                seen.Clear();Settle(null);Check(s.TryPick(pair[1]),"Pick the eleventh item");Settle(pair[1]);
                Check(s.TryPlace(rack)&&!seen.Contains("items_sorted"),"No items_sorted between checkpoints");
                Settle(null);seen.Clear();Check(s.TryPick(pair[0]),"Take the tenth item back off the rack");Settle(pair[0]);
                Check(s.TryPlace(rack)&&!seen.Contains("items_sorted"),"Re-placing an already sorted item does not repeat the checkpoint");
                Settle(null);

                // out_of_supplies: a tool refused for lack of uses and cash, once a minute per tool.
                var broke=s.Capture();broke.cash=0;broke.searchCredits=0;broke.magnetCredits=0;broke.slotCredits=0;
                Check(s.Restore(broke),"Fixture: no cash and no tool uses");unblock();
                var held=s.items.First(i=>i.location==ItemLocation.Loose&&!i.definition.isRat&&s.ItemsOfType(i.definition).Count(o=>o!=i&&o.location==ItemLocation.Loose)>0);
                Check(s.TryPick(held),"Hold an item that still has loose matches");Settle(held);
                seen.Clear();
                Check(!s.TrySearch()&&seen.Count(n=>n=="out_of_supplies")==1,"Search with no uses or cash reports out_of_supplies");
                yield return new WaitForSecondsRealtime(.9f);
                Check(!s.TrySearch()&&seen.Count(n=>n=="out_of_supplies")==1,"Repeated taps within a minute report once");
                Check(!s.TryMagnet()&&seen.Count(n=>n=="out_of_supplies")==2,"Magnet reports separately");
                Check(!s.TryUpgradeCapacity()&&seen.Count(n=>n=="out_of_supplies")==3,"Capacity upgrade reports separately");

                // Tutorial funnel.
                Check(tutorial,"Guided tutorial present");
                seen.Clear();tutorial.Begin(true);
                Check(seen.Contains("tutorial_step"),"Tutorial start reports its first step: "+string.Join(",",seen));
                seen.Clear();tutorial.Skip();
                Check(seen.Contains("tutorial_skipped"),"Skipping reports the step it was on");
                unblock();yield return null;

                // Store opens.
                var supply=UnityEngine.Object.FindFirstObjectByType<SupplyPanel>();Check(supply,"Supply panel present");
                seen.Clear();supply.Open(SupplyKind.Search);
                Check(supply.IsOpen&&seen.Contains("supply_panel_opened"),"Opening the supply panel reports it");
                supply.Close();unblock();
                var menus=UnityEngine.Object.FindFirstObjectByType<DesignerMenus>();
                if(menus){seen.Clear();menus.OpenStore();Check(seen.Contains("store_opened"),"Opening the store reports it");menus.Close();unblock();}

                // Foreground session length on pause, not counted twice.
                var pause=typeof(LabTelemetry).GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic);
                seen.Clear();pause.Invoke(telemetry,new object[]{true});
                Check(seen.Contains("lab_session_length")&&seen.Contains("lab_session_pause"),"Pause reports foreground session length");
                seen.Clear();pause.Invoke(telemetry,new object[]{true});
                Check(!seen.Contains("lab_session_length"),"A second pause without a resume does not count again");
                pause.Invoke(telemetry,new object[]{false});
                seen.Clear();pause.Invoke(telemetry,new object[]{true});
                Check(seen.Contains("lab_session_length"),"After resuming, the next pause reports again");
                pause.Invoke(telemetry,new object[]{false});
            }
            finally
            {
                LabTelemetry.Observed-=observe;DOTween.CompleteAll();s.Restore(saved);s.persistence=persistence;
                foreach(var p in prefs){if(p.Value.HasValue)PlayerPrefs.SetInt(p.Key,p.Value.Value);else PlayerPrefs.DeleteKey(p.Key);}PlayerPrefs.Save();
                for(int i=0;i<gates.Length;i++)if(gates[i])gates[i].enabled=gateStates[i];
                if(temporary)UnityEngine.Object.Destroy(temporary);
            }
        }
    }
}
#endif
