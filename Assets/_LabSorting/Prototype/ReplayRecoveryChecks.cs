#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
using LabSorting.Services;
namespace LabSorting.Prototype
{
    public static class ReplayRecoveryChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("REPLAY RECOVERY: "+text);Debug.Log("REPLAY RECOVERY PASS: "+text);}
        static readonly SupplyKind[] kinds={SupplyKind.Cash,SupplyKind.Search,SupplyKind.Magnet,SupplyKind.Slot};
        static readonly int[] amounts={500,5,3,2};
        static string Receipt(int i)=>"replay-recovery-receipt-"+i;
        static void Compare(SortingSession s,SortingSession.Snapshot expected,int sorted)
        {
            var now=s.Capture();Check(s.SortedCount==sorted&&now.cash==expected.cash&&now.capacity==expected.capacity&&now.searchCredits==expected.searchCredits&&now.magnetCredits==expected.magnetCredits,"Progress, cash, capacity and tools preserved");
            Check(now.completionCount==expected.completionCount&&now.completedArea==expected.completedArea&&now.completedContentVersion==expected.completedContentVersion,"Completion history and content version preserved");
            Check(now.supplyReceipts.OrderBy(x=>x).SequenceEqual(expected.supplyReceipts.OrderBy(x=>x)),"All purchase delivery receipts preserved");
            CommerceEntitlements.ResetCacheForTests();Check(CommerceEntitlements.RemoveAds,"Remove-ads entitlement survives disk reread independently of replay");
        }
        public static IEnumerator Run(SortingSession s,LabCompletionPanel panel,bool beforeReplay)
        {
            var ui=panel.GetComponent<ServicePresentation>();var internet=LabServiceRoot.Instance.internet;bool automatic=ui.automaticConnectivity,enabled=internet.enabled;var originalStatus=internet.Status;
            var setStatus=typeof(InternetConnectivity).GetMethod("SetStatus",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                internet.enabled=false;typeof(InternetConnectivity).GetProperty("IsReady").SetValue(internet,true);setStatus.Invoke(internet,new object[]{ConnectivityStatus.Online});ui.automaticConnectivity=true;yield return null;
                if(beforeReplay){for(int i=0;i<kinds.Length;i++)Check(s.GrantSupply(kinds[i],amounts[i],Receipt(i)),"Verified purchase delivery before replay: "+kinds[i]);panel.PlayAgain();Check(panel.ConfirmingReplay,"Replay confirmation open before interruption");}
                var expected=s.Capture();int sorted=s.SortedCount;
                panel.SendMessage("OnApplicationPause",true);s.SendMessage("OnApplicationPause",true);s.player.SendMessage("OnApplicationPause",true);panel.PlayAgain();
                Check(s.FlushSaves(),"Background flush completes durably");Compare(s,expected,sorted);
                yield return new WaitForSecondsRealtime(.1f);panel.SendMessage("OnApplicationPause",false);s.SendMessage("OnApplicationPause",false);s.player.SendMessage("OnApplicationPause",false);
                for(int cycle=0;cycle<3;cycle++)
                {
                    setStatus.Invoke(internet,new object[]{ConnectivityStatus.Offline});Tick(ui);yield return new WaitForSecondsRealtime(.05f);Check(ui.offlineRoot.activeSelf&&s.player.NetworkBlocked,"Disconnect blocks gameplay");panel.PlayAgain();panel.ContinueExploring();Compare(s,expected,sorted);
                    if(beforeReplay)Check(panel.IsOpen&&panel.ConfirmingReplay,"Queued replay/back input cannot bypass offline gate");
                    setStatus.Invoke(internet,new object[]{ConnectivityStatus.NetworkAvailable});Tick(ui);yield return new WaitForSecondsRealtime(.05f);Check(s.player.NetworkBlocked,"Wi-Fi alone does not unlock replay");
                    setStatus.Invoke(internet,new object[]{ConnectivityStatus.Online});Tick(ui);yield return new WaitForSecondsRealtime(.05f);Check(!ui.offlineRoot.activeSelf&&!s.player.NetworkBlocked,"Verified reconnect releases network gate");Check(s.player.blocked==beforeReplay,"Reconnect restores completion-modal or gameplay ownership correctly");
                }
                Check(s.Load(),"Interrupted state reloads from real disk save");Compare(s,expected,sorted);
                for(int i=0;i<kinds.Length;i++)Check(s.GrantSupply(kinds[i],amounts[i],Receipt(i)),"Retried delivery acknowledged: "+kinds[i]);Compare(s,expected,sorted);
                if(beforeReplay){float until=Time.realtimeSinceStartup+3;while(!panel.IsOpen&&Time.realtimeSinceStartup<until)yield return null;Check(panel.IsOpen&&!panel.ConfirmingReplay,"Reload returns to completion screen without automatically replaying");}
                else
                {
                    Check(!panel.IsOpen&&!s.player.blocked,"New run stays playable after recovery");
                    Check(s.GrantSupply(SupplyKind.Cash,100,"late-replay-purchase"),"Late verified purchase callback delivers into the new run");var late=s.Capture();s.SendMessage("OnApplicationPause",true);Check(s.Load(),"Late purchase reloads after background");Check(s.GrantSupply(SupplyKind.Cash,100,"late-replay-purchase"),"Retried late callback is acknowledged");Compare(s,late,0);s.SendMessage("OnApplicationPause",false);
                    var item=s.items.First(i=>i.location==ItemLocation.Loose);Check(s.TryPick(item),"Recovered replay accepts new pickup");DG.Tweening.DOTween.CompleteAll();Check(s.TryPlace(s.racks.First(r=>r.CanAccept(item.definition))),"Recovered replay accepts new placement");DG.Tweening.DOTween.CompleteAll();s.SendMessage("OnApplicationPause",true);Check(s.Load()&&s.SortedCount==1,"New-run progress survives second background and disk load");s.SendMessage("OnApplicationPause",false);
                    // Restore the durable zero-progress replay checkpoint for the enclosing suite.
                    Check(s.Restore(expected),"Restore isolated replay checkpoint");s.Save();
                }
            }
            finally{panel.SendMessage("OnApplicationPause",false);s.player.SendMessage("OnApplicationPause",false);s.SendMessage("OnApplicationPause",false);ui.automaticConnectivity=false;setStatus.Invoke(internet,new object[]{ConnectivityStatus.Online});Tick(ui);internet.enabled=enabled;setStatus.Invoke(internet,new object[]{originalStatus});ui.automaticConnectivity=automatic;}
        }
        static void Tick(ServicePresentation ui){typeof(ServicePresentation).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);}
    }
}
#endif
