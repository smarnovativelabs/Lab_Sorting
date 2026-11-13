#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LabSorting.UI;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class FullInventoryControlChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("FULL CONTROLS: "+message);Debug.Log("FULL CONTROLS PASS: "+message);}
        public static IEnumerator Run(SortingSession s)
        {
            var saved=s.Capture();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var progress=hud.GetComponent<ItemsProgressPanel>();
            var a=new GameObject("Test hold A",typeof(RectTransform),typeof(Image),typeof(Button),typeof(SortingHoldButton));
            var b=new GameObject("Test hold B",typeof(RectTransform),typeof(Image),typeof(Button),typeof(SortingHoldButton));
            try
            {
                while(s.carried.Count<s.capacity){Check(s.TryPick(s.items.First(i=>i.location==ItemLocation.Loose&&!i.Moving)),"Fill capacity");DG.Tweening.DOTween.CompleteAll();}
                int cash=s.Cash,magnet=s.MagnetCredits,version=s.FullNoticeVersion;var extra=s.items.First(i=>i.location==ItemLocation.Loose&&!i.Moving);
                for(int i=0;i<20;i++){s.TryPick(extra);s.TryMagnet();}
                Check(s.FullNoticeVersion==version+1,"Rapid full pick/magnet attempts emit one notice");
                Check(s.Cash==cash&&s.MagnetCredits==magnet&&s.carried.Count==s.capacity,"Full attempts never spend or collect");
                yield return new WaitForSecondsRealtime(.2f);
                Check(hud.inventorySummary.transform.localScale.x>1&&hud.inventorySummary.transform.localScale.x<=1.036f,"Capacity summary uses a bounded subtle pulse");
                var guide=hud.GetComponent<MatchingRackGuide>();Check(guide.GuidedRack,"Full hands retain matching rack guidance");
                yield return new WaitForSecondsRealtime(.5f);Check(hud.inventorySummary.transform.localScale==Vector3.one,"Capacity pulse returns to normal");
                var first=a.GetComponent<SortingHoldButton>();var second=b.GetComponent<SortingHoldButton>();first.session=second.session=s;
                var one=new PointerEventData(EventSystem.current){pointerId=10,button=PointerEventData.InputButton.Left};
                var two=new PointerEventData(EventSystem.current){pointerId=11,button=PointerEventData.InputButton.Left};
                first.OnPointerDown(one);int generation=s.HoldGeneration;first.OnPointerDown(two);Check(s.HoldGeneration==generation,"Second finger cannot restart an owned button");
                second.OnPointerDown(two);generation=s.HoldGeneration;first.OnPointerExit(one);Check(s.HoldGeneration==generation&&second.PointerHeld,"Old finger exit cannot cancel newer control intent");
                second.OnPointerUp(one);Check(second.PointerHeld,"Foreign pointer release is ignored");
                s.Select(0);second.SendMessage("Update");Check(!second.PointerHeld&&!s.Holding&&!s.BufferedTap,"Inventory switching cancels stale held gesture and buffered action");
                first.OnPointerDown(one);progress.Open();first.SendMessage("Update");Check(!first.PointerHeld&&!s.Holding&&!s.BufferedTap&&s.player.blocked,"Opening items panel cancels input immediately");progress.Close();
                first.OnPointerDown(one);first.OnPointerExit(one);Check(!first.PointerHeld&&!s.Holding&&!s.BufferedTap,"Leaving button cancels gesture without an extra action");
                Check(SearchDirection.Floor(4,0)=="UPPER FLOOR"&&SearchDirection.Route(4,0)=="FIND STAIRS UP"&&SearchDirection.Floor(0,4)=="LOWER FLOOR","Last-item guidance distinguishes upper and lower floor");
            }
            finally{if(progress.IsOpen)progress.Close();s.CancelHold();UnityEngine.Object.Destroy(a);UnityEngine.Object.Destroy(b);s.Restore(saved);}
        }
    }
}
#endif
