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
        public static bool AnalyticsAllowed=>PlayerPrefs.GetInt("Lab.AnalyticsConsent",0)==1;
        public static bool AttributionAllowed=>PlayerPrefs.GetInt("Lab.AttributionConsent",0)==1;
        public static bool DiagnosticsAllowed=>PlayerPrefs.GetInt("Lab.DiagnosticsConsent",0)==1;
        public static event Action<string> Observed;
        void Awake(){instance=this;}
        public void BindSession(SortingSession value){if(ReferenceEquals(session,value))return;OnDisable();session=value;if(isActiveAndEnabled)OnEnable();}
        void OnEnable(){if(session){session.SearchStarted+=Search;session.MagnetCollected+=Magnet;session.RackCompleted+=Rack;session.ItemPlaced+=Placed;session.MilestoneEarned+=Goal;}}
        void Start(){Initialize();}
        public async void Initialize()
        {
            if(starting||!settings||!settings.enableLiveServices||Application.isEditor)return;
            starting=true;
            try{
                if(settings.firebaseConfigurationAdded&&!ready&&(AnalyticsAllowed||DiagnosticsAllowed)&&await FirebaseApp.CheckAndFixDependenciesAsync()==DependencyStatus.Available){if(!this)return;app=FirebaseApp.DefaultInstance;ready=true;}
                if(ready){FirebaseAnalytics.SetConsent(new System.Collections.Generic.Dictionary<ConsentType,ConsentStatus>{{ConsentType.AnalyticsStorage,AnalyticsAllowed?ConsentStatus.Granted:ConsentStatus.Denied},{ConsentType.AdStorage,ConsentStatus.Denied},{ConsentType.AdUserData,ConsentStatus.Denied},{ConsentType.AdPersonalization,ConsentStatus.Denied}});FirebaseAnalytics.SetAnalyticsCollectionEnabled(AnalyticsAllowed);Crashlytics.IsCrashlyticsCollectionEnabled=DiagnosticsAllowed;Crashlytics.ReportUncaughtExceptionsAsFatal=true;Crashlytics.SetCustomKey("game","lab_sorting");}
                if(AttributionAllowed&&!tenjin&&!string.IsNullOrWhiteSpace(settings.tenjinApiKey)&&Application.platform==RuntimePlatform.Android){var go=new GameObject("Tenjin");go.transform.SetParent(transform);tenjin=go.AddComponent<AndroidTenjin>();tenjin.Init(settings.tenjinApiKey);tenjin.OptIn();tenjin.Connect();}
                if(tenjin){if(AttributionAllowed)tenjin.OptIn();else tenjin.OptOut();}
                Event("lab_session_start");
            }catch{Debug.LogWarning("Lab telemetry initialization unavailable; gameplay continues.");}finally{starting=false;}
        }
        public static void SetPrivacy(bool analytics,bool attribution,bool diagnostics){PlayerPrefs.SetInt("Lab.AnalyticsConsent",analytics?1:0);PlayerPrefs.SetInt("Lab.AttributionConsent",attribution?1:0);PlayerPrefs.SetInt("Lab.DiagnosticsConsent",diagnostics?1:0);PlayerPrefs.Save();if(instance)instance.Initialize();}
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
        [Serializable] sealed class AdRevenueData{public double revenue;public string ad_revenue_currency="USD",network_name,ad_unit_id,format,placement,revenue_precision;}
        public static void Revenue(MaxSdkBase.AdInfo info){if(info==null||info.Revenue<0)return;Event("ad_revenue",info.NetworkName,info.Revenue);if(instance&&instance.ready&&AnalyticsAllowed)FirebaseAnalytics.LogEvent("ad_impression",new Parameter("ad_platform","AppLovin"),new Parameter("ad_source",info.NetworkName),new Parameter("currency","USD"),new Parameter("value",info.Revenue));if(instance&&instance.tenjin&&AttributionAllowed)instance.tenjin.AppLovinImpressionFromJSON(JsonUtility.ToJson(new AdRevenueData{revenue=info.Revenue,network_name=info.NetworkName,ad_unit_id=info.AdUnitIdentifier,format=info.AdFormat,placement=info.Placement,revenue_precision=info.RevenuePrecision}));}
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
