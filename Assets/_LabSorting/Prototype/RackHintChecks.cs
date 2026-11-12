using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class RackHintChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("RACK HINT: "+message);Debug.Log("RACK HINT PASS: "+message);}
        static IEnumerator WaitForHint(SortRack rack,bool value){float until=Time.realtimeSinceStartup+3;while(rack.CompatibleHint!=value&&Time.realtimeSinceStartup<until)yield return null;}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var initial=s.Capture();var racks=s.racks;
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Disposable hint rack";go.transform.position=new Vector3(1000,1000,1000);
            var rack=go.AddComponent<SortRack>();rack.id="hint-test";rack.slots=new[]{go.transform,go.transform,go.transform};rack.indicator=go.GetComponent<Renderer>();
            rack.neutral=racks[0].neutral;rack.valid=racks[0].valid;rack.invalid=racks[0].invalid;rack.compatible=racks[0].compatible;
            rack.label=new GameObject("Label").AddComponent<TextMesh>();rack.label.transform.SetParent(go.transform);rack.Initialize();s.racks=new[]{rack};
            try
            {
                Check(rack.compatible,"Shared compatibility material wired");
                Check(racks.All(r=>r.compatible==rack.compatible),"All designer racks share one hint material");
                var item=s.items[0];var same=s.items.Where(i=>i.definition==item.definition).Take(4).ToArray();
                Check(s.TryPick(item),"Pick selected type");DOTween.CompleteAll();yield return null;yield return null;
                Check(rack.CompatibleHint&&rack.indicator.sharedMaterial==rack.compatible,"Compatible empty rack receives subtle highlight");
                rack.Preview(item.definition);Check(rack.indicator.sharedMaterial==rack.valid,"Direct valid hover overrides compatibility highlight");rack.Preview(null);
                Check(rack.indicator.sharedMaterial==rack.compatible,"Leaving hover restores compatibility highlight");
                Check(s.TryPlace(rack),"Place first item");DOTween.CompleteAll();
                Check(!rack.AlmostFull&&rack.AvailabilityHint=="","No premature almost-full warning");
                Check(s.TryPick(same[1])&&s.TryPlace(rack)==false,"Moving pickup cannot place prematurely");DOTween.CompleteAll();Check(s.TryPlace(rack),"Place second item");DOTween.CompleteAll();
                Check(rack.AlmostFull&&rack.AvailabilityHint=="1 SLOT LEFT"&&rack.label.text.Contains("1 SLOT LEFT"),"Exact remaining-slot hint on almost-full rack");
                Check(s.TryPick(same[2]),"Pick last slot item");DOTween.CompleteAll();Check(s.TryPlace(rack),"Fill rack");DOTween.CompleteAll();
                Check(s.TryPick(same[3]),"Carry extra compatible item");DOTween.CompleteAll();yield return null;yield return null;
                Check(!rack.CompatibleHint&&rack.AvailabilityHint=="FULL","Full rack excludes highlight and reports FULL");
                rack.Preview(same[3].definition);Check(rack.indicator.sharedMaterial==rack.invalid,"Full hover stays red");rack.Preview(null);
                Check(s.TryPick(item),"Retrieve from full rack");DOTween.CompleteAll();yield return null;yield return null;
                // EditMode runner yields can advance before the next runtime Update. Wait for the observable update.
                s.player.SendMessage("OnApplicationFocus",true);float hintDeadline=Time.realtimeSinceStartup+3;while(!rack.CompatibleHint&&Time.realtimeSinceStartup<hintDeadline)yield return null;
                Check(rack.CompatibleHint&&rack.AvailabilityHint=="1 SLOT LEFT","Retrieval refreshes capacity and compatibility; blocked="+s.player.blocked+", count="+rack.Count+", hint="+rack.AvailabilityHint);
                var other=s.items.First(i=>i.definition!=item.definition);Check(s.TryPick(other),"Select different item type");DOTween.CompleteAll();yield return WaitForHint(rack,false);
                Check(!rack.CompatibleHint,"Wrong type removes highlight");
                s.Select(s.carried.IndexOf(item));yield return WaitForHint(rack,true);Check(rack.CompatibleHint,"Inventory reselection refreshes highlight");
                s.player.SetBlocked(true);yield return WaitForHint(rack,false);Check(!rack.CompatibleHint,"Settings/modal block removes hint");s.player.SetBlocked(false);
                Debug.Log("RACK_HINTS_OK");
            }
            finally{DOTween.CompleteAll();s.racks=racks;s.player.SetBlocked(false);s.Restore(initial);s.InitializeSaveCache();UnityEngine.Object.Destroy(go);}
        }
    }
}
