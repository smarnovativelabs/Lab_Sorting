#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
using LabSorting.Services;
namespace LabSorting.Prototype
{
    public static class OffersReplayChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("OFFERS REPLAY: "+message);Debug.Log("OFFERS REPLAY PASS: "+message);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var initial=s.Capture();var oldName=s.saveName;bool persist=s.persistence;var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var p=hud.GetComponent<LabCompletionPanel>();var supply=hud.GetComponent<SupplyPanel>();var menus=hud.GetComponent<DesignerMenus>();bool auto=p.autoShow;
            s.saveName="offers-replay-test-"+Guid.NewGuid().ToString("N")+".json";string path=s.SavePath;
            string entitlementFile=CommerceEntitlements.StorageFile;
            try
            {
                CommerceEntitlements.StorageFile=s.saveName+".entitlements";CommerceEntitlements.ResetCacheForTests();Check(CommerceEntitlements.GrantRemoveAds(),"Separate test ledger grants remove-ads without touching player purchases");
                var animation=hud.GetComponent<SupplyRewardMotion>();Check(animation,"Supply reward animation wired");
                float clock=Time.timeScale;
                try
                {
                    Time.timeScale=0;supply.OpenSlots();yield return new WaitForSecondsRealtime(.3f);
                    var panelMotion=supply.root.GetComponent<DesignerPanelMotion>();Check(panelMotion&&panelMotion.card.localScale==Vector3.one,"Panel opening finishes while game clock is paused");
                    var close=supply.closeButton.GetComponent<AnimatedPanelClose>();Check(close,"Animated close wired");close.Close();close.Close();
                    Check(panelMotion.Closing&&s.player.blocked,"Repeated close taps keep gameplay blocked during exit");yield return new WaitForSecondsRealtime(.2f);
                    Check(!supply.IsOpen&&!s.player.blocked,"Animated exit restores controls once");
                }
                finally{Time.timeScale=clock;if(supply.IsOpen)supply.Close();}
                int played=animation.Played;s.persistence=false;
                Check(s.GrantSupply(SupplyKind.Search,2,"animation-receipt")&&animation.Played==played+1,"Verified reward starts feedback");
                Check(s.GrantSupply(SupplyKind.Search,2,"animation-receipt")&&animation.Played==played+1,"Duplicate receipt does not replay reward feedback");
                Check(!s.GrantSupply(SupplyKind.Search,0,"invalid-animation")&&animation.Played==played+1,"Invalid reward has no success feedback");s.Restore(initial);s.persistence=persist;
                Check(s.items.Length==4032&&s.capacity==10&&s.Cash==150&&s.SearchCredits==5&&s.MagnetCredits==2,"Fresh 4032-item session receives starting economy and ten slots");
                var legacySlots=s.Capture();legacySlots.slotCredits=3;Check(s.Restore(legacySlots)&&s.capacity==13&&s.SlotCredits==0,"Legacy slot purchases become usable permanent capacity");var migrated=s.Capture();Check(s.Restore(migrated)&&s.capacity==13,"Migrated slots are not applied twice");s.Restore(initial);
                Check(!hud.GetComponent<EconomyHUD>().upgradeButton.gameObject.activeSelf,"Standalone capacity control removed");
                supply.OpenCash();Check(menus.storeRoot.activeSelf&&!supply.IsOpen,"Cash opens store");menus.Close();
                s.persistence=true;
                Check(s.BuyOffer(SupplyKind.Search)&&s.SearchCredits==10&&s.Cash==120,"30 cash buys five searches");
                Check(s.BuyOffer(SupplyKind.Magnet)&&s.MagnetCredits==5&&s.Cash==30,"90 cash buys three magnets");
                Check(!s.BuyOffer(SupplyKind.Slot)&&s.Cash==30&&s.capacity==10,"Unaffordable capacity changes nothing");
                Check(s.GrantSupply(SupplyKind.Cash,150,"test-cash")&&s.GrantSupply(SupplyKind.Cash,150,"test-cash")&&s.Cash==180,"Verified delivery is durable and duplicate receipt does not pay twice");
                Check(s.BuyOffer(SupplyKind.Slot)&&s.capacity==14&&s.Cash==30,"150 cash adds four permanent slots immediately");
                Check(s.GrantSupply(SupplyKind.Search,3,"test-search")&&s.GrantSupply(SupplyKind.Magnet,2,"test-magnet")&&s.GrantSupply(SupplyKind.Slot,2,"test-slots"),"Multi-use rewards delivered");
                s.Save();Check(s.Load()&&s.capacity==16&&s.SearchCredits==13&&s.MagnetCredits==7&&s.Cash==30,"Disk reload preserves all economy fields");
                var economy=s.Capture();var cap=s.Capture();cap.capacity=59;Check(s.Restore(cap)&&!s.BuyOffer(SupplyKind.Slot)&&!s.GrantSupply(SupplyKind.Slot,2,"overflow")&&s.capacity==59,"Offers reject overflow without partial charge");s.Restore(economy);
                foreach(var kind in new[]{SupplyKind.Search,SupplyKind.Magnet,SupplyKind.Slot}){supply.Open(kind);yield return new WaitForSecondsRealtime(.3f);Check(supply.cashButton.gameObject.activeInHierarchy&&supply.adButton.gameObject.activeInHierarchy&&!supply.storeButton.gameObject.activeSelf&&!supply.useButton.gameObject.activeSelf,"Exactly cash/ad choices for "+kind);SortingSmokeChecks.Capture(s,"Logs/NewOffer-"+kind+".png");supply.Close();}
                var complete=s.Capture();var used=new HashSet<SortRack>();SortRack lastRack=null;SortItem last=null;
                foreach(var group in s.items.GroupBy(i=>i.definition))
                {
                    var rack=s.racks.FirstOrDefault(r=>!used.Contains(r)&&r.CapacityFor(group.Key)>=group.Count());Check(rack,"Enough rack space for "+group.Key.displayName);used.Add(rack);int slot=0;
                    foreach(var item in group){var e=complete.items.First(x=>x.id==item.id);e.location=ItemLocation.Rack;e.rack=rack.id;e.slot=slot++;last=item;lastRack=rack;}
                }
                var final=complete.items.First(e=>e.id==last.id);final.location=ItemLocation.Loose;final.rack=null;final.slot=-1;
                Check(s.Restore(complete),"4031 sorted fixture validates");p.autoShow=true;Check(s.TryPick(last),"Pick final item");DOTween.CompleteAll();Check(s.TryPlace(lastRack),"Final actual placement completes area");DOTween.CompleteAll();
                float until=Time.realtimeSinceStartup+5;while(!p.IsOpen&&Time.realtimeSinceStartup<until)yield return null;Check(p.confetti.Any(piece=>piece.gameObject.activeInHierarchy),"Completion starts visible confetti particles");Check(p.IsOpen&&s.player.blocked&&s.CompletionCount==1&&s.CompletedArea==s.areaId,"Completion opens panel and records persistent history once");SortingSmokeChecks.Capture(s,"Logs/NewCompletion.png");
                s.Save();var finished=s.Capture();Check(s.Load()&&s.CompletionCount==1,"Reload does not duplicate completion count");yield return new WaitForSecondsRealtime(.6f);
                int cash=s.Cash,search=s.SearchCredits,magnet=s.MagnetCredits,capacity=s.capacity;p.continueButton.onClick.Invoke();
                Check(p.ConfirmingReplay&&s.SortedCount==4032&&s.Cash==cash,"First replay tap only shows confirmation; save and economy untouched");
                p.ContinueExploring();Check(p.IsOpen&&!p.ConfirmingReplay&&s.SortedCount==4032,"Back cancels replay safely");
                yield return ReplayRecoveryChecks.Run(s,p,true);cash=s.Cash;search=s.SearchCredits;magnet=s.MagnetCredits;capacity=s.capacity;finished=s.Capture();
                ReplayWriteFailureChecks.BeforeReplay(s,p);
                p.PlayAgain();yield return new WaitForSecondsRealtime(.2f);SortingSmokeChecks.Capture(s,"Logs/ReplayConfirmation.png");p.PlayAgain();
                Check(!p.IsOpen&&!s.player.blocked&&s.SortedCount==0&&s.carried.Count==0,"Replay clears run and restores control");
                Check(s.Cash==cash&&s.SearchCredits==search&&s.MagnetCredits==magnet&&s.capacity==capacity&&s.HasCompletedArea&&s.CompletionCount==1,"Replay preserves economy and historical completion");
                Check(s.Load()&&s.SortedCount==0&&s.HasCompletedArea,"Replayed run and history survive disk reload");
                ReplayWriteFailureChecks.AfterReplay(s);
                yield return ReplayRecoveryChecks.Run(s,p,false);
                Check(s.GrantSupply(SupplyKind.Cash,150,"test-cash")&&s.Cash==cash,"Replay preserves receipt deduplication");
                Check(!s.ReplayCompletedArea(),"Incomplete run cannot be reset through completion API");
                var legacy=finished;legacy.completedArea=null;legacy.completionCount=0;legacy.completedContentVersion=0;legacy.runCompletionRecorded=false;Check(s.Restore(legacy)&&s.CompletionCount==1&&s.Cash==cash,"Legacy completed save migrates without new cash");
                SaveStreamChecks.Run(s,Check);
            }
            finally{if(supply.IsOpen)supply.Close();if(menus.IsOpen)menus.Close();s.FlushSaves();s.persistence=false;s.Restore(initial);p.autoShow=auto;s.saveName=oldName;s.persistence=persist;foreach(var suffix in new[]{"",".bak",".tmp"}){if(File.Exists(path+suffix))File.Delete(path+suffix);if(File.Exists(CommerceEntitlements.PathName+suffix))File.Delete(CommerceEntitlements.PathName+suffix);}CommerceEntitlements.StorageFile=entitlementFile;CommerceEntitlements.ResetCacheForTests();}
        }
    }
}
#endif
