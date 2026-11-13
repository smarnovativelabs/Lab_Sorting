#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using LabSorting.UI;
using LabSorting.Services;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class ServiceExperienceChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("SERVICE UX: "+text);Debug.Log("SERVICE UX PASS: "+text);}
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var original=s.Capture();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var ui=hud.GetComponent<ServicePresentation>();var review=hud.GetComponent<LabReviewPrompt>();var guide=hud.GetComponent<GuidedTutorial>();var turn=hud.GetComponent<TutorialRackTurn>();var c=LabServiceRoot.Instance.commerce;var internet=LabServiceRoot.Instance.internet;
            string reviewPrefix=review.preferencesPrefix,guidePrefix=guide.preferencesPrefix;bool connected=internet.enabled,motor=s.player.character.Motor.enabled;
            review.preferencesPrefix="LabSorting.TestReview.";guide.preferencesPrefix="LabSorting.TestRackTurn.";
            foreach(var key in new[]{"Attempts","Last","Stop"})PlayerPrefs.DeleteKey(review.preferencesPrefix+key);foreach(var key in new[]{"GuidedDone","GuidedStep","RackTurnShown.v1"})PlayerPrefs.DeleteKey(guide.preferencesPrefix+key);
            var events=new System.Collections.Generic.List<string>();Action<string> observed=events.Add;LabTelemetry.Observed+=observed;
            string storeUrl=review.androidStoreUrl;
            try
            {
                yield return null;yield return internet.InitializeRoutine();s.player.SendMessage("OnApplicationFocus",true);
                typeof(SupplyCommerce).GetMethod("BeginPurchase",Private).Invoke(c,null);yield return null;Check(ui.loadingRoot.activeSelf&&c.PurchaseBusy,"Purchase loading overlay follows real transaction state");SortingSmokeChecks.Capture(s,"Logs/PurchaseLoading.png");
                typeof(SupplyCommerce).GetField("busy",Private).SetValue(c,false);typeof(SupplyCommerce).GetProperty("Status").SetValue(c,"Purchase cancelled or unsuccessful.");yield return null;Check(!ui.loadingRoot.activeSelf&&ui.toastText.text.Contains("cancelled")&&ui.toast.alpha>0,"Failed or cancelled purchase hides loader and shows toast");
                typeof(SupplyCommerce).GetProperty("Status").SetValue(c,"Purchase delivered.");yield return null;Check(ui.toastText.text=="Purchase delivered.","Confirmed purchase shows success feedback");
                ui.automaticConnectivity=true;internet.enabled=false;typeof(InternetConnectivity).GetMethod("SetStatus",Private).Invoke(internet,new object[]{ConnectivityStatus.Offline});ui.SendMessage("Update");Check(ui.offlineRoot.activeSelf&&s.player.blocked&&s.player.NetworkBlocked,"Offline state opens mandatory gate and releases pointer");yield return new WaitForSecondsRealtime(.3f);Check(ui.offlineRoot.GetComponent<CanvasGroup>().alpha>.95f,"Connection panel fades in above gameplay and tour");SortingSmokeChecks.Capture(s,"Logs/NoConnection.png");
                s.player.SetBlocked(false);Check(s.player.blocked&&!s.TryPick(s.items[0]),"Closing another modal cannot bypass online-only gate");
                typeof(InternetConnectivity).GetMethod("SetStatus",Private).Invoke(internet,new object[]{ConnectivityStatus.NetworkAvailable});ui.SendMessage("Update");Check(s.player.NetworkBlocked,"Wi-Fi adapter alone does not unlock gameplay");
                typeof(InternetConnectivity).GetMethod("SetStatus",Private).Invoke(internet,new object[]{ConnectivityStatus.Online});ui.SendMessage("Update");Check(!ui.offlineRoot.activeSelf&&!s.player.blocked&&!s.player.NetworkBlocked,"Verified internet restores gameplay");internet.SendMessage("OnApplicationPause",true);ui.SendMessage("Update");Check(internet.Status==ConnectivityStatus.Online&&!ui.offlineRoot.activeSelf,"Background pause preserves verified internet without offline popup");internet.SendMessage("OnApplicationPause",false);internet.SendMessage("OnApplicationFocus",false);ui.SendMessage("Update");Check(!ui.offlineRoot.activeSelf,"Focus loss alone never opens offline panel");ui.automaticConnectivity=false;internet.enabled=connected;
                var bootObject=new GameObject("Test online boot");var boot=bootObject.AddComponent<LabStartup>();boot.connectionRoot=new GameObject("Test boot gate");typeof(LabStartup).GetField("connectivity",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,internet);internet.enabled=false;typeof(InternetConnectivity).GetMethod("SetStatus",Private).Invoke(internet,new object[]{ConnectivityStatus.Offline});
                var startup=(IEnumerator)typeof(LabStartup).GetMethod("Start",Private).Invoke(boot,null);Check(startup.MoveNext()&&boot.connectionRoot.activeSelf,"Boot cannot load gameplay while offline");boot.autoLoad=false;
                typeof(InternetConnectivity).GetMethod("SetStatus",Private).Invoke(internet,new object[]{ConnectivityStatus.NetworkAvailable});Check(startup.MoveNext()&&boot.connectionRoot.activeSelf,"Boot waits for verified internet, not just Wi-Fi");
                typeof(InternetConnectivity).GetMethod("SetStatus",Private).Invoke(internet,new object[]{ConnectivityStatus.Online});Check(startup.MoveNext()&&!boot.connectionRoot.activeSelf,"Boot proceeds only after successful connection probe");(startup as IDisposable)?.Dispose();UnityEngine.Object.Destroy(boot.connectionRoot);UnityEngine.Object.Destroy(bootObject);internet.enabled=connected;
                long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();Check(!review.Eligible(149,600,now)&&!review.Eligible(500,299,now)&&review.Eligible(150,300,now),"Review waits for meaningful progress and five minutes");yield return review.TryNative();Check(review.Attempts==1&&!review.IsOpen&&!review.NativePending&&!review.Eligible(500,600,now+86400),"Native attempt counted once; no immediate fallback or daily repeat");Check(review.Eligible(500,600,now+31L*86400),"Later prompt only eligible after 30-day cooldown");
                review.androidStoreUrl="https://example.com/not-a-store";Check(!review.ValidStoreUrl(out _),"Untrusted review destination rejected");review.androidStoreUrl="https://play.google.com/store/apps/details?id=com.example.lab";Check(review.ValidStoreUrl(out _),"Configured store listing accepted");
                s.player.character.Motor.enabled=false;s.player.character.Motor.BaseVelocity=Vector3.zero;review.ShowFallback();Check(review.IsOpen&&s.player.blocked&&review.Attempts==2,"Neutral fallback panel opens with lifetime attempt cap");SortingSmokeChecks.Capture(s,"Logs/ReviewPrompt.png");review.Dismiss();Check(!review.IsOpen&&!s.player.blocked&&review.OptedOut&&!review.Eligible(4032,9999,now+365L*86400),"No Thanks permanently stops automatic prompts across replay");
                var rack=s.racks[0];var item=s.items.First(i=>rack.CanAccept(i.definition));s.player.character.Motor.SetPosition(rack.transform.TransformPoint(new Vector3(0,0,-2.5f)));s.player.look.SnapToDirection(rack.label.transform.position-s.player.view.transform.position);Physics.SyncTransforms();
                guide.Begin(false);Check(s.TryPick(item),"Tutorial first pickup accepted");DG.Tweening.DOTween.CompleteAll();yield return null;Check(guide.Current==GuidedTutorial.Step.Place,"Pickup advances tutorial to rack placement");
                float limit=Time.realtimeSinceStartup+4;while(!turn.Turning&&Time.realtimeSinceStartup<limit)yield return null;Check(turn.Turning&&guide.GuidedRack,"First pickup gently turns toward a visible compatible rack");s.player.AddLook(new Vector2(10,0));yield return null;Check(!turn.Turning,"Manual camera input cancels guided rotation");
                guide.Skip();s.Restore(original);Check(!turn.Turning,"Tutorial end restores normal camera ownership");
                Check(new[]{"purchase_flow_opened","internet_gate_shown","internet_restored","native_review_requested","review_prompt_shown","review_prompt_dismissed","tutorial_rack_turn_started","tutorial_rack_turn_cancelled"}.All(events.Contains),"Firebase telemetry hooks cover service, review and guidance lifecycle");
                yield return DesignerLayoutChecks.Run(s);
            }
            finally
            {
                LabTelemetry.Observed-=observed;typeof(SupplyCommerce).GetField("busy",Private).SetValue(c,false);guide.Skip();ui.automaticConnectivity=false;ui.SendMessage("Update");if(review.IsOpen)review.Dismiss();internet.enabled=connected;ui.automaticConnectivity=false;s.player.character.Motor.enabled=motor;s.Restore(original);review.androidStoreUrl=storeUrl;
                foreach(var key in new[]{"Attempts","Last","Stop"})PlayerPrefs.DeleteKey(review.preferencesPrefix+key);foreach(var key in new[]{"GuidedDone","GuidedStep","RackTurnShown.v1"})PlayerPrefs.DeleteKey(guide.preferencesPrefix+key);review.preferencesPrefix=reviewPrefix;guide.preferencesPrefix=guidePrefix;PlayerPrefs.Save();
            }
        }
    }
}
#endif
