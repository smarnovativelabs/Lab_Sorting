using System;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using LabSorting.Sorting;
using UnityEngine;
using UnityEngine.Purchasing;
using System.Linq;
namespace LabSorting.Services
{
    public sealed class LabTelemetry:MonoBehaviour
    {
        public LabServiceSettings settings;
        public SortingSession session;
        static LabTelemetry instance;
        FirebaseApp app;BaseTenjin tenjin;bool ready,starting;int placements;
        readonly System.Collections.Generic.HashSet<string> purchaseEvents=new System.Collections.Generic.HashSet<string>();
        [Serializable] sealed class StoreReceipt{public string Payload;}
        [Serializable] sealed class GoogleReceipt{public string json,signature;}
        // Analytics, Tenjin attribution and crash reporting are app-managed, not player settings.
        public static bool AnalyticsAllowed=>true;
        public static bool AttributionAllowed=>true;
        public static bool DiagnosticsAllowed=>true;
        public static event Action<string> Observed;
        void Awake(){instance=this;}
        public void BindSession(SortingSession value){if(ReferenceEquals(session,value))return;OnDisable();session=value;if(isActiveAndEnabled)OnEnable();}
        void OnEnable(){if(session){session.SearchStarted+=Search;session.MagnetCollected+=Magnet;session.RackCompleted+=Rack;session.ItemPlaced+=Placed;session.MilestoneEarned+=Goal;}}
        void Start(){Initialize();}
        public async void Initialize()
        {
            if(starting||!settings||!settings.enableLiveServices||Application.isEditor)return;
            starting=true;
            // Tenjin starts first and independently so a Firebase failure never blocks install attribution.
            if(!tenjin&&!string.IsNullOrWhiteSpace(settings.tenjinApiKey)&&Application.platform==RuntimePlatform.Android)
            {
                var go=new GameObject("Tenjin");go.transform.SetParent(transform);
                // Same order as the Motel/Janitor projects: Init, OptIn, store type, then Connect (install/open event).
                try{var t=go.AddComponent<AndroidTenjin>();t.Init(settings.tenjinApiKey);t.OptIn();t.SetAppStoreType(AppStoreType.googleplay);t.Connect();tenjin=t;if(Debug.isDebugBuild)Debug.Log("[LabTelemetry] Tenjin connected (googleplay).");}
                catch{Destroy(go);Debug.LogWarning("Tenjin initialization unavailable; gameplay continues.");}
            }
            try{
                if(settings.firebaseConfigurationAdded&&!ready&&(AnalyticsAllowed||DiagnosticsAllowed)&&await FirebaseApp.CheckAndFixDependenciesAsync()==DependencyStatus.Available){if(!this)return;app=FirebaseApp.DefaultInstance;ready=true;}
                if(ready){FirebaseAnalytics.SetConsent(new System.Collections.Generic.Dictionary<ConsentType,ConsentStatus>{{ConsentType.AnalyticsStorage,AnalyticsAllowed?ConsentStatus.Granted:ConsentStatus.Denied},{ConsentType.AdStorage,ConsentStatus.Denied},{ConsentType.AdUserData,ConsentStatus.Denied},{ConsentType.AdPersonalization,ConsentStatus.Denied}});FirebaseAnalytics.SetAnalyticsCollectionEnabled(AnalyticsAllowed);Crashlytics.IsCrashlyticsCollectionEnabled=DiagnosticsAllowed;Crashlytics.ReportUncaughtExceptionsAsFatal=true;Crashlytics.SetCustomKey("game","lab_sorting");}
                Event("lab_session_start");
            }catch{Debug.LogWarning("Lab telemetry initialization unavailable; gameplay continues.");}finally{starting=false;}
        }
        public static void Event(string name,string context="",double value=0)
        {
            Observed?.Invoke(name);
            if(!instance||!instance.settings||!instance.settings.enableLiveServices||Application.isEditor)return;
            try{if(instance.ready&&AnalyticsAllowed)FirebaseAnalytics.LogEvent(name,new Parameter("context",context??""),new Parameter("value",value));if(instance.tenjin&&AttributionAllowed)instance.tenjin.SendEvent(name);}catch{ }
        }
        public static void Purchase(ConfirmedOrder order)
        {
            if(!instance||string.IsNullOrWhiteSpace(order.Info.TransactionID)||!instance.purchaseEvents.Add(order.Info.TransactionID))return;
            var product=order.CartOrdered.Items().FirstOrDefault()?.Product;if(product==null)return;
            Event("purchase_confirmed",product.definition.id,(double)product.metadata.localizedPrice);
            if(Application.isEditor||!instance.settings||!instance.settings.enableLiveServices)return;
            try{
                if(instance.ready&&AnalyticsAllowed)FirebaseAnalytics.LogEvent("lab_purchase_delivered",new Parameter("item_id",product.definition.id),new Parameter("currency",product.metadata.isoCurrencyCode),new Parameter("value",(double)product.metadata.localizedPrice));
                if(instance.tenjin&&AttributionAllowed){var receipt=JsonUtility.FromJson<StoreReceipt>(order.Info.Receipt);var google=JsonUtility.FromJson<GoogleReceipt>(receipt.Payload);if(!string.IsNullOrEmpty(google.json)&&!string.IsNullOrEmpty(google.signature))instance.tenjin.Transaction(product.definition.id,product.metadata.isoCurrencyCode,1,(double)product.metadata.localizedPrice,order.Info.TransactionID,google.json,google.signature);}
            }catch{Debug.LogWarning("Purchase delivered; revenue telemetry unavailable.");}
        }
        // Same fields as Tenjin's own TenjinAppLovinIntegration (ILRD), so Tenjin's ad revenue report is complete.
        [Serializable] sealed class AdRevenueData{public double revenue;public string ad_revenue_currency="USD",country,network_name,ad_unit_id,format,placement,network_placement,creative_id,revenue_precision;}
        public static void Revenue(MaxSdkBase.AdInfo info)
        {
            if(info==null||info.Revenue<0)return;Event("ad_revenue",info.NetworkName,info.Revenue);
            if(!instance||Application.isEditor)return;
            try{
                if(instance.ready&&AnalyticsAllowed)FirebaseAnalytics.LogEvent("ad_impression",new Parameter("ad_platform","AppLovin"),new Parameter("ad_source",info.NetworkName),new Parameter("ad_format",info.AdFormat??""),new Parameter("ad_unit_name",info.AdUnitIdentifier??""),new Parameter("currency","USD"),new Parameter("value",info.Revenue));
                if(instance.tenjin&&AttributionAllowed){string country="";try{country=MaxSdk.GetSdkConfiguration().CountryCode;}catch{}
                    instance.tenjin.AppLovinImpressionFromJSON(JsonUtility.ToJson(new AdRevenueData{revenue=info.Revenue,country=country,network_name=info.NetworkName,ad_unit_id=info.AdUnitIdentifier,format=info.AdFormat,placement=info.Placement,network_placement=info.NetworkPlacement,creative_id=info.CreativeIdentifier,revenue_precision=info.RevenuePrecision}));}
            }catch{Debug.LogWarning("Ad revenue telemetry unavailable.");}
        }
        void Search(ItemDefinition item,int count)=>Event("search_used",item.id,count);
        void Magnet(ItemDefinition item,Vector3[] origins)=>Event("magnet_used",item.id,origins.Length);
        void Rack(SortRack rack)=>Event("rack_completed",rack.id);
        void Goal(string name,int cash)=>Event("milestone_completed",name,cash);
        void Placed(SortItem item){if(++placements%25==0)Event("sorting_progress","placed_this_session",placements);}
        void OnApplicationPause(bool paused){if(paused)Event("lab_session_pause","placements",placements);else if(tenjin&&AttributionAllowed)tenjin.Connect();}
        void OnDisable(){if(session){session.SearchStarted-=Search;session.MagnetCollected-=Magnet;session.RackCompleted-=Rack;session.ItemPlaced-=Placed;session.MilestoneEarned-=Goal;}}
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
