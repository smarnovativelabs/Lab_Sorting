using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class BulkIconChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("BULK ICON: "+message);Debug.Log("BULK ICON PASS: "+message);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var h=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var panel=h.GetComponent<ItemsProgressPanel>();var initial=s.Capture();
            try
            {
                var types=s.items.Select(i=>i.definition).Distinct().ToArray();Check(types.Length==46&&types.All(d=>d.icon),"All 46 types have icons");Check(types.Select(d=>d.icon).Distinct().Count()==46,"Each type has its own sprite");
                Check(s.items.Length==4032&&s.items.Select(i=>i.id).Distinct().Count()==4032,"Item count and unique IDs preserved");
                var recovery=s.GetComponent<ItemRecovery>();Check(!s.items.Any(i=>recovery.NeedsRecovery(i)),"Bulk items stay in bounds and outside environment walls");
                foreach(var item in s.items.GroupBy(i=>i.definition).Take(7).Select(g=>g.First())){Check(s.TryPick(item),"Pick icon category");DOTween.CompleteAll();}
                yield return new WaitForSeconds(.2f);
                Check(h.inventoryIcons.Take(7).All(i=>i.enabled&&i.sprite),"Inventory category icons visible");Check(h.activeIcon.sprite==s.Active.definition.icon,"Selected card icon matches active type");
                ScreenCapture.CaptureScreenshot("Logs/BulkInventoryHUD.png");yield return new WaitForSeconds(.2f);panel.Open();yield return new WaitForSeconds(.3f);
                Check(panel.rowIcons.All(i=>i.enabled&&i.sprite),"Progress rows have icons");ScreenCapture.CaptureScreenshot("Logs/BulkProgressIcons.png");yield return new WaitForSeconds(.2f);panel.Close();
                var data=s.Capture();int cash=s.Cash,held=s.carried.Count;var loose=data.items.First(e=>e.location==ItemLocation.Loose);var actual=s.items.First(i=>i.id==loose.id);var authored=actual.transform.position;
                loose.position+=Vector3.up*10;data.layoutRevision=0;Check(s.RestoreLoadedLayout(data),"Legacy layout load accepted");Check(s.Cash==cash&&s.carried.Count==held&&actual.transform.position==authored,"Migration preserves cash/hand and restores loose scene pose");
                var snapshot=s.Capture();var writer=new System.IO.StringWriter();new SaveJsonStream().Write(writer,snapshot);var saved=JsonUtility.FromJson<SortingSession.Snapshot>(writer.ToString());Check(saved.layoutRevision==s.layoutRevision,"Layout revision survives streaming JSON save");
                var entry=saved.items.First(e=>e.id==actual.id);entry.position=authored+Vector3.right*.02f;Check(s.RestoreLoadedLayout(saved)&&actual.transform.position==entry.position,"Current layout preserves dropped positions");
                Debug.Log("BULK_ICONS_OK");
            }
            finally{if(panel.IsOpen)panel.Close();DOTween.CompleteAll();s.Restore(initial);s.InitializeSaveCache();}
        }
    }
}
