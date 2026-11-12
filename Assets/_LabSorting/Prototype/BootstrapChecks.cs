#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using LabSorting.Services;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class BootstrapChecks
    {
        static void Check(bool value,string message){if(!value)throw new Exception("BOOT: "+message);Debug.Log("BOOT PASS: "+message);}
        public static IEnumerator Run()
        {
            yield return null;var root=LabServiceRoot.Instance;Check(root,"Services exist in first scene");int id=root.GetInstanceID();
            Check(root.commerce&&root.telemetry&&root.internet&&root.GetComponent<LabConsent>(),"First scene owns IAP, ads, telemetry, internet and consent");
            Check(root.internet.IsReady&&!root.commerce.session,"Internet ready before gameplay session");
            Check(!root.commerce.CanBuy(SupplyKind.Cash)&&!root.commerce.CanBuyLargeCash&&!root.commerce.Watch(SupplyKind.Cash),"No-session store/ad operations safely unavailable");
            Check(!root.commerce.settings.enableLiveServices,"Missing account IDs cannot start live services");
            var startup=UnityEngine.Object.FindFirstObjectByType<LabStartup>();startup.autoLoad=true;startup.StartCoroutine("Start");
            float until=Time.realtimeSinceStartup+60;while(SceneManager.GetActiveScene().path!="Assets/Scenes/Lab_Sorting.unity"&&Time.realtimeSinceStartup<until)yield return null;
            yield return null;yield return null;
            Check(LabServiceRoot.Instance&&LabServiceRoot.Instance.GetInstanceID()==id,"Same service root survives automatic scene transition");
            Check(UnityEngine.Object.FindObjectsByType<LabServiceRoot>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<SupplyCommerce>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<LabTelemetry>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<InternetConnectivity>(FindObjectsSortMode.None).Length==1,"No duplicate SDK/connectivity owners after transition");
            var session=UnityEngine.Object.FindFirstObjectByType<SortingSession>();Check(session&&session.Initialized&&root.commerce.session==session&&root.telemetry.session==session,"Services bind to initialized gameplay session");
            Check(UnityEngine.Object.FindFirstObjectByType<SupplyPanel>(FindObjectsInactive.Include).commerce==root.commerce&&UnityEngine.Object.FindFirstObjectByType<ServicesPanel>(FindObjectsInactive.Include).commerce==root.commerce,"Both UI panels reference persistent commerce owner");
        }
    }
}
#endif
