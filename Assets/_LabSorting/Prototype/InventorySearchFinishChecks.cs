#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.UI;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class InventorySearchFinishChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("INVENTORY SEARCH: "+message);Debug.Log("INVENTORY SEARCH PASS: "+message);}
        public static IEnumerator Run(SortingSession s)
        {
            var snapshot=s.Capture();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();
            try
            {
                foreach(var item in s.items.GroupBy(i=>i.definition).Take(10).Select(g=>g.First())){Check(s.TryPick(item),"Collect distinct row");DG.Tweening.DOTween.CompleteAll();}
                hud.SendMessage("Update");yield return new WaitForSecondsRealtime(.4f);
                var scroll=hud.inventoryButtons[0].GetComponentInParent<UnityEngine.UI.ScrollRect>();var auto=scroll.GetComponent<InventoryAutoScroll>();
                Check(auto&&!auto.IsAnimating&&scroll.verticalNormalizedPosition<.15f,"New selected type is revealed at bottom");
                s.Select(0);hud.SendMessage("Update");yield return new WaitForSecondsRealtime(.4f);
                Check(scroll.verticalNormalizedPosition>.85f,"Selection change reveals top row");
                s.Select(9);hud.SendMessage("Update");yield return new WaitForSecondsRealtime(.4f);
                auto.OnBeginDrag(null);float before=scroll.verticalNormalizedPosition;s.Select(0);hud.SendMessage("Update");yield return new WaitForSecondsRealtime(.3f);
                Check(Mathf.Abs(scroll.verticalNormalizedPosition-before)<.01f&&!auto.IsAnimating,"Manual drag suppresses automatic selection scroll");
                auto.OnEndDrag(null);yield return new WaitForSecondsRealtime(1f);Check(Mathf.Abs(scroll.verticalNormalizedPosition-before)<.01f,"Suppressed selection never jumps after manual scrolling ends");
                s.capacity=12;var same=s.ItemsOfType(s.Active.definition).First(i=>i.location==ItemLocation.Loose);Check(s.TryPick(same),"Pick another item of the same selected type");DG.Tweening.DOTween.CompleteAll();
                hud.SendMessage("Update");Check(!auto.IsAnimating,"Same selected type does not trigger scroll");
                auto.OnScroll(null);Check(auto.ManualBusy,"Mouse wheel reserves manual control");
                var guard=scroll.verticalScrollbar.GetComponent<InventoryAutoScroll>();guard.OnPointerDown(null);Check(auto.ManualBusy,"Scrollbar touch reserves manual control");guard.OnPointerUp(null);
                s.Restore(snapshot);var first=s.items.First(i=>i.location==ItemLocation.Loose);Check(s.TryPick(first),"Pick search fixture");DG.Tweening.DOTween.CompleteAll();Check(s.TrySearch(),"Start search fixture");
                int keep=0;foreach(var item in s.ItemsOfType(first.definition))if(item.location==ItemLocation.Loose&&++keep>5)item.location=ItemLocation.Rack;
                var locator=UnityEngine.Object.FindFirstObjectByType<ItemLocatorHUD>();yield return new WaitForSecondsRealtime(.2f);
                Check(locator.LooseSearchCount==5&&locator.FinalItemsMode,"Only last five loose items enable finish emphasis");
                foreach(var item in s.ItemsOfType(first.definition))if(item.location==ItemLocation.Loose)item.location=ItemLocation.Rack;
                yield return new WaitForSecondsRealtime(.2f);Check(!locator.FinalItemsMode&&locator.LooseSearchCount==0,"Finish emphasis clears with no loose results");var economy=hud.GetComponent<EconomyHUD>();Check(economy.GuidedSearchItem==null&&economy.marker.text.StartsWith("ALL FOUND")&&economy.marker.gameObject.activeSelf,"Finished search gives placement guidance instead of a stale target");Check(locator.beacons.All(b=>!b.gameObject.activeSelf),"No search markers remain on rack items");
            }
            finally{s.Restore(snapshot);}
        }
    }
}
#endif
