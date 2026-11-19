#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using LabSorting.Services;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class AppleCommerceChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("APPLE IAP: "+text);Debug.Log("APPLE IAP PASS: "+text);}
        static string Payload(string json)=>"header."+Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+','-').Replace('/','_')+".signature";
        // Synthetic JWS only exercises identity binding; native cryptographic verification requires an Apple device.
        static void BindingChecks()
        {
            const string bundle="com.peri.games.quarantine.lab.sort.simulator",product="lab_sorting_cash_500",txn="123456";
            string body="{\"bundleId\":\""+bundle+"\",\"productId\":\""+product+"\",\"transactionId\":\""+txn+"\",\"quantity\":1}";
            Check(ApplePurchaseBinding.Matches(Payload(body),bundle,product,txn),"Matching native-verified payload identity accepted");
            Check(!ApplePurchaseBinding.Matches(Payload(body),"wrong.bundle",product,txn)&&!ApplePurchaseBinding.Matches(Payload(body),bundle,"other.product",txn)&&!ApplePurchaseBinding.Matches(Payload(body),bundle,product,"other.transaction"),"Wrong bundle, product and transaction rejected");
            Check(!ApplePurchaseBinding.Matches(Payload(body.Replace("\"quantity\":1","\"quantity\":2")),bundle,product,txn),"Multiple quantity rejected");
            Check(!ApplePurchaseBinding.Matches(Payload(body.Replace("\"quantity\":1","\"quantity\":1,\"revocationDate\":100")),bundle,product,txn),"Revoked transaction rejected");
            Check(!ApplePurchaseBinding.Matches("invalid",bundle,product,txn)&&!ApplePurchaseBinding.Matches(null,bundle,product,txn),"Missing and malformed payload rejected");
        }
        static IEnumerator Respond(bool accept)
        {
            GameObject dialog=null;float limit=Time.realtimeSinceStartup+15;
            while(!(dialog=GameObject.Find("UIFakeStoreWindow"))&&Time.realtimeSinceStartup<limit)yield return null;
            Check(dialog,"Unity fake store presents purchase dialog");
            var window=dialog.GetComponents<MonoBehaviour>().First(c=>c.GetType().Name=="UIFakeStoreWindow");
            window.SendMessage(accept?"OnOkClicked":"OnCancelClicked");yield return null;
        }
        public static IEnumerator Run()
        {
            BindingChecks();var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var c=UnityEngine.Object.FindFirstObjectByType<SupplyCommerce>();var config=c.settings;var initial=s.Capture();string save=s.saveName,ledger=CommerceEntitlements.StorageFile;
            bool persist=s.persistence,suppress=SessionState.GetBool(SupplyCommerce.SuppressEditorServicesKey,false);
            var isolated=UnityEngine.Object.Instantiate(config);isolated.googlePlayPublicKey="";isolated.maxSdkKey="";isolated.editorTestServices=true;
            string unique="apple-commerce-test-"+Guid.NewGuid().ToString("N");
            try
            {
                s.saveName=unique+".json";s.persistence=true;CommerceEntitlements.StorageFile=unique+"-entitlements.json";CommerceEntitlements.ResetCacheForTests();
                c.settings=isolated;SessionState.SetBool(SupplyCommerce.SuppressEditorServicesKey,false);c.SendMessage("Start");
                float limit=Time.realtimeSinceStartup+20;while(!c.CanBuy(SupplyKind.Cash)&&Time.realtimeSinceStartup<limit)yield return null;
                Check(c.CanBuy(SupplyKind.Cash)&&c.CanBuyLargeCash,"Cash products load without an Android licensing key in fake-store mode");
                int cash=s.Cash;Check(c.Buy(SupplyKind.Cash),"Small cash purchase starts");yield return Respond(false);
                Check(!c.Busy&&s.Cash==cash,"Cancelled purchase grants no cash");
                Check(c.Buy(SupplyKind.Cash),"Small cash retry starts");yield return Respond(true);
                limit=Time.realtimeSinceStartup+10;while(c.Busy&&Time.realtimeSinceStartup<limit)yield return null;
                Check(!c.Busy&&s.Cash==cash+isolated.smallCashAmount,"Small cash delivered and confirmed");
                Check(c.BuyLargeCash(),"Large cash purchase starts");yield return Respond(true);
                limit=Time.realtimeSinceStartup+10;while(c.Busy&&Time.realtimeSinceStartup<limit)yield return null;
                int paid=cash+isolated.smallCashAmount+isolated.largeCashAmount;
                Check(!c.Busy&&s.Cash==paid,"Large cash delivered and confirmed");
                s.Restore(initial);Check(s.Load()&&s.Cash==paid,"Both cash purchases survive disk reload");
                c.BuyRemoveAds();yield return Respond(true);yield return null;
                Check(c.AdsRemoved,"Non-consumable remove ads delivered");
                foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(CommerceEntitlements.PathName+suffix))File.Delete(CommerceEntitlements.PathName+suffix);
                CommerceEntitlements.ResetCacheForTests();Check(!c.AdsRemoved,"Isolated entitlement reset for restore test");
                c.RestorePurchases();yield return null;yield return null;
                Check(c.AdsRemoved&&s.Cash==paid,"Restore recovers remove ads without regranting consumed cash");
                c.RestorePurchases();yield return null;Check(s.Cash==paid,"Repeated restore does not duplicate cash");
            }
            finally
            {
                s.FlushSaves();s.persistence=false;s.Restore(initial);
                foreach(var suffix in new[]{"",".bak",".tmp"}){if(File.Exists(s.SavePath+suffix))File.Delete(s.SavePath+suffix);if(File.Exists(CommerceEntitlements.PathName+suffix))File.Delete(CommerceEntitlements.PathName+suffix);}
                s.saveName=save;s.persistence=persist;CommerceEntitlements.StorageFile=ledger;CommerceEntitlements.ResetCacheForTests();
                c.settings=config;SessionState.SetBool(SupplyCommerce.SuppressEditorServicesKey,suppress);UnityEngine.Object.Destroy(isolated);
            }
        }
    }
}
#endif
