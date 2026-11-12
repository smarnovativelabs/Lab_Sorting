#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using LabSorting.Services;
using LabSorting.Sorting;
using LabSorting.UI;
using UnityEngine.Localization.Settings;
namespace LabSorting.Prototype
{
    public static class ServiceChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("SERVICES: "+text);Debug.Log("SERVICES PASS: "+text);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var initial=s.Capture();var c=UnityEngine.Object.FindFirstObjectByType<SupplyCommerce>();var f=BindingFlags.Instance|BindingFlags.NonPublic;string ledger=CommerceEntitlements.StorageFile;var oldLocale=LocalizationSettings.SelectedLocale;string oldLanguage=PlayerPrefs.GetString("Lab.Language","en");string oldSave=s.saveName,testSave="services-test-"+Guid.NewGuid().ToString("N")+".json";
            try
            {
                Check(s.items.Length==4032,"Designer catalog still contains 4032 items");Check(c&&c.settings&&!c.settings.enableLiveServices,"No live services or fake purchases with missing account configuration");
                var ids=new[]{c.settings.removeAdsProductId,c.settings.smallCashProductId,c.settings.largeCashProductId,c.settings.searchPackProductId,c.settings.magnetPackProductId,c.settings.slotPackProductId};Check(ids.All(v=>!string.IsNullOrWhiteSpace(v))&&ids.Distinct().Count()==6,"Six unique configured store product IDs");
                Check(!c.Watch(SupplyKind.Cash)&&!c.Buy(SupplyKind.Cash)&&!c.CanBuyLargeCash&&c.Price(SupplyKind.Cash)==null,"Unavailable store/ad does not grant rewards or fabricate prices");
                int cash=s.Cash;Check(s.GrantSupply(SupplyKind.Cash,25,"test-reward"),"Reward grant succeeds");Check(s.GrantSupply(SupplyKind.Cash,25,"test-reward")&&s.Cash==cash+25,"Repeated transaction grants once");var saved=s.Capture();s.Restore(saved);s.GrantSupply(SupplyKind.Cash,25,"test-reward");Check(s.Cash==cash+25,"Receipt survives snapshot restore");
                var type=c.GetType();type.GetField("rewardToken",f).SetValue(c,"test-sdk-reward");type.GetField("rewardKind",f).SetValue(c,SupplyKind.Search);type.GetField("rewardAmount",f).SetValue(c,2);int search=s.SearchCredits;
                var reward=type.GetMethod("OnReward",f);reward.Invoke(c,new object[]{c.settings.androidRewardedId,default(MaxSdkBase.Reward),null});reward.Invoke(c,new object[]{c.settings.androidRewardedId,default(MaxSdkBase.Reward),null});Check(s.SearchCredits==search+2,"Repeated SDK reward callbacks deliver exactly once");
                s.capacity=s.maxCapacity;s.GrantSupply(SupplyKind.Slot,1,"test-slot");int before=s.Cash;Check(!s.BuySupply(SupplyKind.Slot)&&s.Cash==before&&!c.CanBuy(SupplyKind.Slot),"Full capacity blocks additional slot purchases without charging");
                CommerceEntitlements.StorageFile="lab-entitlements-test-"+Guid.NewGuid().ToString("N")+".json";CommerceEntitlements.ResetCacheForTests();Check(!CommerceEntitlements.RemoveAds&&CommerceEntitlements.GrantRemoveAds(),"Remove Ads writes isolated durable entitlement");CommerceEntitlements.ResetCacheForTests();Check(CommerceEntitlements.RemoveAds,"Remove Ads survives a fresh disk read");s.Restore(initial);Check(CommerceEntitlements.RemoveAds,"Game progress restore cannot remove paid entitlement");
                var settings=UnityEngine.Object.FindFirstObjectByType<SettingsTutorialPanel>();var panel=settings.GetComponent<ServicesPanel>();settings.OpenSettings();panel.Open();Check(panel.root.activeSelf&&s.player.blocked,"Store/privacy panel opens inside settings and blocks movement");yield return null;Canvas.ForceUpdateCanvases();SortingSmokeChecks.Capture(s,"Logs/LabServicesPanel.png");panel.Close();settings.Close();
                yield return LocalizationSettings.InitializationOperation;float timeout=Time.realtimeSinceStartup+10;while(!LabLocalization.Ready&&Time.realtimeSinceStartup<timeout)yield return null;Check(LabLocalization.Ready,"Localization initialization completes");LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("es");timeout=Time.realtimeSinceStartup+10;while(LabLocalization.Text("SETTINGS")!="AJUSTES"&&Time.realtimeSinceStartup<timeout)yield return null;Check(LabLocalization.Text("SETTINGS")=="AJUSTES","Spanish String Table loads and translates UI; actual="+LabLocalization.Text("SETTINGS")+" locale="+LocalizationSettings.SelectedLocale.Identifier.Code);LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("en");timeout=Time.realtimeSinceStartup+10;while(!LabLocalization.Ready&&Time.realtimeSinceStartup<timeout)yield return null;Check(LabLocalization.Text("SETTINGS")=="SETTINGS","English locale can be restored");
                Check(UnityEngine.Object.FindObjectsByType<InternetConnectivity>(FindObjectsSortMode.None).Length==1,"Exactly one advisory connectivity service");
                Check(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="Firebase.Analytics")&&AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="Firebase.Crashlytics"),"Firebase Analytics and Crashlytics SDK assemblies loaded");
                Check(UnityEngine.Object.FindFirstObjectByType<LabTelemetry>()&&typeof(AndroidTenjin)!=null&&typeof(GoogleMobileAds.Ump.Api.ConsentInformation)!=null,"Telemetry, Tenjin and Google UMP are available");
                s.saveName=testSave;s.persistence=true;int wallet=s.Cash;Check(s.GrantSupply(SupplyKind.Cash,500,"test-durable-purchase"),"Purchase reward and receipt durably saved before confirmation");s.Restore(initial);Check(s.Load()&&s.Cash==wallet+500,"Purchase cash survives actual disk reload");Check(s.GrantSupply(SupplyKind.Cash,500,"test-durable-purchase")&&s.Cash==wallet+500,"Re-delivered purchase after disk reload grants no duplicate");s.persistence=false;
                Debug.Log("LAB_SERVICES_CHECKS_OK");
            }
            finally
            {
                c.CancelInvoke();typeReset(c);s.FlushSaves();s.persistence=false;s.Restore(initial);s.saveName=oldSave;if(CommerceEntitlements.StorageFile!=ledger){foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(CommerceEntitlements.PathName+suffix))File.Delete(CommerceEntitlements.PathName+suffix);}foreach(var suffix in new[]{"",".bak",".tmp"}){var path=Path.Combine(Application.persistentDataPath,testSave)+suffix;if(File.Exists(path))File.Delete(path);}CommerceEntitlements.StorageFile=ledger;CommerceEntitlements.ResetCacheForTests();if(oldLocale)LocalizationSettings.SelectedLocale=oldLocale;PlayerPrefs.SetString("Lab.Language",oldLanguage);PlayerPrefs.Save();
            }
        }
        static void typeReset(SupplyCommerce c){var flags=BindingFlags.Instance|BindingFlags.NonPublic;c.GetType().GetField("rewardToken",flags).SetValue(c,null);c.GetType().GetField("rewardGranted",flags).SetValue(c,false);}
    }
}
#endif
