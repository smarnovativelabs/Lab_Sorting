using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using LabSorting.Sorting;
using UnityEngine;
using UnityEngine.Purchasing;
namespace LabSorting.Services
{
    public sealed class LabTelemetry:MonoBehaviour
    {
        public LabServiceSettings settings;
        public SortingSession session;
        static LabTelemetry instance;
        FirebaseApp app;BaseTenjin tenjin;bool ready,starting,sessionStarted,iosConsentComplete;
        int placements,retryAttempt;
        double retryAt,flushAt;
        Task<DependencyStatus> dependencies;
        TelemetryOutbox outbox;
        bool dirty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int debugPurchaseEventsLoaded;
        public string DebugOutboxState=>outbox==null?"Outbox not loaded":
            "Purchase events restored at startup: "+debugPurchaseEventsLoaded+" | queued now: "+outbox.messages.Count(m=>m.kind=="purchase")+
            " | Firebase waiting: "+outbox.messages.Count(m=>m.kind=="purchase"&&!m.firebaseSent)+
            " | Tenjin waiting: "+outbox.messages.Count(m=>m.kind=="purchase"&&!m.tenjinSent);
#endif
        string OutboxPath=>Path.Combine(Application.persistentDataPath,"lab-telemetry-outbox.json");
        [Serializable] sealed class StoreReceipt{public string Payload;}
        [Serializable] sealed class GoogleReceipt{public string json,signature;}
        [Serializable] sealed class AdRevenueData{public double revenue;public string ad_revenue_currency="USD",country,network_name,ad_unit_id,format,placement,network_placement,creative_id,revenue_precision;}
        public static bool AnalyticsAllowed=>true;
        public static bool AttributionAllowed=>true;
        public static bool DiagnosticsAllowed=>true;
        public static event Action<string> Observed;
        bool Live=>settings&&settings.enableLiveServices&&!Application.isEditor;
        bool AttributionReady=>Application.platform!=RuntimePlatform.IPhonePlayer||iosConsentComplete;
        void Awake()
        {
            instance=this;outbox=Application.isEditor?new TelemetryOutbox():TelemetryOutbox.Load(OutboxPath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugPurchaseEventsLoaded=outbox.messages.Count(m=>m.kind=="purchase");
            Debug.Log("[IAP TEST] "+DebugOutboxState);
#endif
        }
        public void BindSession(SortingSession value){if(ReferenceEquals(session,value))return;OnDisable();session=value;if(isActiveAndEnabled)OnEnable();}
        void OnEnable(){if(session){session.SearchStarted+=Search;session.MagnetCollected+=Magnet;session.RackCompleted+=Rack;session.ItemPlaced+=Placed;session.MilestoneEarned+=Goal;session.ItemFirstSorted+=FirstSorted;session.SuppliesEmpty+=Empty;}}
        void Start(){activeSince=Time.realtimeSinceStartupAsDouble;Initialize();}
        public void Initialize()
        {
            if(starting||!Live)return;
            starting=true;
            try
            {
                // Firebase is independent of ads/ATT; attribution waits for the bounded ATT flow.
                if(!tenjin&&AttributionReady&&!string.IsNullOrWhiteSpace(settings.TenjinApiKey)&&(Application.platform==RuntimePlatform.Android||Application.platform==RuntimePlatform.IPhonePlayer))
                {
                    var go=new GameObject("Tenjin");go.transform.SetParent(transform);
                    try{BaseTenjin t=Application.platform==RuntimePlatform.IPhonePlayer?(BaseTenjin)go.AddComponent<IosTenjin>():go.AddComponent<AndroidTenjin>();t.Init(settings.TenjinApiKey);t.OptIn();if(Application.platform==RuntimePlatform.Android)t.SetAppStoreType(AppStoreType.googleplay);t.Connect();tenjin=t;}
                    catch{Destroy(go);Debug.LogWarning("Tenjin initialization unavailable; will retry.");}
                }
                if(settings.firebaseConfigurationAdded&&!ready)
                {
                    // Poll one task on the main thread; never start parallel native dependency repair.
                    if(dependencies==null)dependencies=FirebaseApp.CheckAndFixDependenciesAsync();
                    if(dependencies.IsCompleted)
                    {
                        var completed=dependencies;dependencies=null;
                        if(completed.GetAwaiter().GetResult()==DependencyStatus.Available)
                        {
                            app=FirebaseApp.DefaultInstance;
                            FirebaseAnalytics.SetConsent(new System.Collections.Generic.Dictionary<ConsentType,ConsentStatus>{{ConsentType.AnalyticsStorage,ConsentStatus.Granted},{ConsentType.AdStorage,ConsentStatus.Denied},{ConsentType.AdUserData,ConsentStatus.Denied},{ConsentType.AdPersonalization,ConsentStatus.Denied}});
                            FirebaseAnalytics.SetAnalyticsCollectionEnabled(AnalyticsAllowed);
                            Crashlytics.IsCrashlyticsCollectionEnabled=DiagnosticsAllowed;Crashlytics.ReportUncaughtExceptionsAsFatal=true;Crashlytics.SetCustomKey("game","lab_sorting");ready=true;
                        }
                    }
                }
                if(!sessionStarted){sessionStarted=true;Event("lab_session_start");}
            }
            catch{Debug.LogWarning("Firebase initialization unavailable; will retry.");}
            finally{starting=false;retryAt=Time.realtimeSinceStartupAsDouble+SupplyCommerce.RetryDelay(retryAttempt++);}
        }
        void Update()
        {
            if(!Live)return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(dependencies!=null&&dependencies.IsCompleted||now>=retryAt&&(!ready||!tenjin))Initialize();
            if(now<flushAt)return;flushAt=now+2;Flush();
        }
        void Enqueue(TelemetryMessage message)
        {
            if(!Live)return;
            if(outbox==null)outbox=TelemetryOutbox.Load(OutboxPath);
            if(outbox.Add(message)){dirty=true;if(message.kind!="event")Persist();}
        }
        bool Persist(){if(!dirty)return true;if(!outbox.Save(OutboxPath))return false;dirty=false;return true;}
        void Flush()
        {
            if(outbox==null||!Persist())return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(CommerceDebugControls.HoldTelemetry)return;
#endif
            if(outbox.Drain(SendFirebase,SendTenjin)){dirty=true;Persist();}
        }
        bool SendFirebase(TelemetryMessage m)
        {
            if(!ready||!AnalyticsAllowed)return false;
            if(m.kind=="event")FirebaseAnalytics.LogEvent(m.name,new Parameter("context",m.context??""),new Parameter("value",m.value));
            else if(m.kind=="purchase")FirebaseAnalytics.LogEvent("lab_purchase_delivered",new Parameter("item_id",m.product),new Parameter("currency",m.currency),new Parameter("value",m.value),new Parameter("transaction_id",m.transaction));
            else{var ad=JsonUtility.FromJson<AdRevenueData>(m.payload);FirebaseAnalytics.LogEvent("ad_impression",new Parameter("ad_platform","AppLovin"),new Parameter("ad_source",ad.network_name??""),new Parameter("ad_format",ad.format??""),new Parameter("ad_unit_name",ad.ad_unit_id??""),new Parameter("currency","USD"),new Parameter("value",ad.revenue));}
            return true;
        }
        bool SendTenjin(TelemetryMessage m)
        {
            if(!tenjin||!AttributionAllowed)return false;
            if(m.kind=="event")tenjin.SendEvent(m.name);
            else if(m.kind=="ad")tenjin.AppLovinImpressionFromJSON(m.payload);
            else if(m.apple)tenjin.Transaction(m.product,m.currency,1,m.value,m.transaction,null,null);
            else
            {
                var receipt=JsonUtility.FromJson<StoreReceipt>(m.receipt);var google=JsonUtility.FromJson<GoogleReceipt>(receipt.Payload);
                if(string.IsNullOrEmpty(google.json)||string.IsNullOrEmpty(google.signature))return false;
                tenjin.Transaction(m.product,m.currency,1,m.value,m.transaction,google.json,google.signature);
            }
            return true;
        }
        public void CompleteIosConsent(){iosConsentComplete=true;Initialize();}
        public static void Event(string name,string context="",double value=0)
        {
            Observed?.Invoke(name);
            if(instance)instance.Enqueue(new TelemetryMessage{kind="event",name=name,context=context,value=value});
        }
        // Called only after receipt verification and durable reward delivery, before
        // ConfirmPurchase. Failure leaves the order pending so delivery can retry.
        internal static bool RecordDeliveredPurchase(Order order)
        {
            if(Application.isEditor)return true;
            if(!instance)return false;
            if(!instance.settings)return false;
            if(!instance.Live)return true;
            if(order?.Info==null||string.IsNullOrWhiteSpace(order.Info.TransactionID))return false;
            var product=order.CartOrdered.Items().FirstOrDefault()?.Product;if(product==null)return false;
            if(instance.outbox==null)instance.outbox=TelemetryOutbox.Load(instance.OutboxPath);
            var message=new TelemetryMessage{kind="purchase",transaction=order.Info.TransactionID,product=product.definition.id,currency=product.metadata.isoCurrencyCode,value=(double)product.metadata.localizedPrice,receipt=order.Info.Receipt,apple=Application.platform==RuntimePlatform.IPhonePlayer};
            // Always save, including duplicate retries: an earlier Add may have succeeded
            // in memory while its disk write failed. Never acknowledge that as durable.
            instance.dirty=true;
            if(!instance.outbox.JournalPurchase(message,instance.OutboxPath))return false;
            instance.dirty=false;
            return true;
        }
        public static void Revenue(MaxSdkBase.AdInfo info)
        {
            if(info==null||info.Revenue<0||double.IsNaN(info.Revenue)||double.IsInfinity(info.Revenue))return;
            Event("ad_revenue",info.NetworkName,info.Revenue);if(!instance)return;
            string country="";try{country=MaxSdk.GetSdkConfiguration().CountryCode;}catch{}
            instance.Enqueue(new TelemetryMessage{kind="ad",payload=JsonUtility.ToJson(new AdRevenueData{revenue=info.Revenue,country=country,network_name=info.NetworkName,ad_unit_id=info.AdUnitIdentifier,format=info.AdFormat,placement=info.Placement,network_placement=info.NetworkPlacement,creative_id=info.CreativeIdentifier,revenue_precision=info.RevenuePrecision})});
        }
        void Search(ItemDefinition item,int count)=>Event("search_used",item.id,count);
        void Magnet(ItemDefinition item,Vector3[] origins)=>Event("magnet_used",item.id,origins.Length);
        void Rack(SortRack rack)=>Event("rack_completed",rack.id);
        void Goal(string name,int cash)=>Event("milestone_completed",name,cash);
        void Placed(SortItem item){if(++placements%25==0)Event("sorting_progress","placed_this_session",placements);}
        // Run funnel: unique items sorted in this run, once per checkpoint (sorting_progress counts re-placements per session).
        static readonly int[] SortedCheckpoints={10,25,50,100,250,500,1000,2000,3000};
        void FirstSorted(int count)
        {
            bool all=session&&count==session.items.Length;
            if(all||Array.IndexOf(SortedCheckpoints,count)>=0)Event("items_sorted",all?"all":"checkpoint",count);
        }
        // A tool refused for lack of uses and cash: once a minute per tool at most, so repeated taps do not flood.
        readonly System.Collections.Generic.Dictionary<SupplyKind,double> emptyReported=new System.Collections.Generic.Dictionary<SupplyKind,double>();
        void Empty(SupplyKind kind)
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(emptyReported.TryGetValue(kind,out var at)&&now-at<60)return;
            emptyReported[kind]=now;Event("out_of_supplies",kind.ToString().ToLowerInvariant(),session?session.Cash:0);
        }
        // Foreground play time from start/resume to pause, in seconds.
        double activeSince=-1;
        void ReportSessionLength(){if(activeSince<0)return;Event("lab_session_length","seconds",Math.Round(Time.realtimeSinceStartupAsDouble-activeSince));activeSince=-1;}
        void OnApplicationPause(bool paused){if(paused){ReportSessionLength();Event("lab_session_pause","placements",placements);if(Live)Persist();}else{activeSince=Time.realtimeSinceStartupAsDouble;retryAt=0;try{if(tenjin&&AttributionAllowed)tenjin.Connect();}catch{Destroy(tenjin.gameObject);tenjin=null;}}}
        void OnApplicationQuit(){ReportSessionLength();if(Live&&outbox!=null)Persist();}
        void OnDisable(){if(session){session.SearchStarted-=Search;session.MagnetCollected-=Magnet;session.RackCompleted-=Rack;session.ItemPlaced-=Placed;session.MilestoneEarned-=Goal;session.ItemFirstSorted-=FirstSorted;session.SuppliesEmpty-=Empty;}}
        void OnDestroy(){if(Live&&outbox!=null)Persist();if(instance==this)instance=null;}
    }
}
