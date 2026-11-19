using System;
using System.Linq;
using System.Reflection;
using UnityEngine.Purchasing;
using UnityEngine;
namespace LabSorting.Services
{
    public sealed partial class SupplyCommerce
    {
        internal const double ConfirmationTimeoutSeconds=30;
        readonly System.Collections.Generic.Dictionary<string,double> confirmationDeadlines=new System.Collections.Generic.Dictionary<string,double>();
        internal void RecoverStalledConfirmations(double now)
        {
            foreach(var pair in confirmationDeadlines.ToArray())
            {
                if(now<pair.Value)continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var debug=GetComponent<CommerceDebugControls>();
                if(debug&&debug.IsPaused(pair.Key))continue;
#endif
                // IAP 5.5 also retains an internal confirmation request. Reporting its
                // timeout through the SDK failure callback releases that request without
                // consuming, refunding, or granting anything. A late native success is safe.
                if(store!=null&&waitingForSession.TryGetValue(pair.Key,out var order))
                {
                    try
                    {
                        var callback=FindConfirmationCallback(store);
                        if(callback==null)Debug.LogError("IAP confirmation recovery binding unavailable; check installed IAP version.");
                        else callback.OnConfirmOrderFailed(new FailedOrder(order,PurchaseFailureReason.Unknown,"Confirmation callback timed out; retrying."));
                    }
                    catch(Exception e){Debug.LogWarning("IAP confirmation recovery failed: "+e.GetType().Name);}
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(debug)debug.WatchdogExpired(pair.Key);
#endif
                confirmationDeadlines.Remove(pair.Key);
                confirmRequested.Remove(pair.Key);
                // Keep the saved reward/transaction id. Re-fetch and retry confirmation;
                // GrantSupply's durable receipt prevents a second reward.
                ScheduleStoreRetry(StoreRecovery.Purchases);
            }
        }
        // Version-bound adapter for Unity IAP 5.5, which exposes the callback interface
        // but no cancellation API. link.xml preserves these fields in IL2CPP builds.
        internal static IStorePurchaseConfirmCallback FindConfirmationCallback(object owner)
        {
            if(owner==null)return null;
            if(owner is IStorePurchaseConfirmCallback callback)return callback;
            for(var type=owner.GetType();type!=null;type=type.BaseType)
            {
                foreach(var name in new[]{"m_PurchaseService","m_BaseInternalPurchaseService","m_ConfirmOrderUseCase"})
                {
                    var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
                    if(field!=null)return FindConfirmationCallback(field.GetValue(owner));
                }
            }
            return null;
        }
        enum StoreRecovery { None, Connect, Products, Purchases }
        StoreRecovery storeRecovery;
        bool storeConnecting,maxStarting;
        int storeRetryAttempt,maxRetryAttempt;
        double storeRetryAt,nextDeliveryRetry,maxRetryAt;
        double consentRetryAt=double.PositiveInfinity,rewardLoadAt=double.PositiveInfinity,interstitialLoadAt=double.PositiveInfinity;
        LabTrackingAuthorization tracking;
        internal static double RetryDelay(int attempt)=>Math.Min(60,5*Math.Pow(2,Math.Min(Math.Max(0,attempt),4)));
        void ScheduleStoreRetry(StoreRecovery stage)
        {
            if(store==null)return;
            // Preserve a broader recovery (connection/products) when purchase fetching also fails.
            if(storeRecovery!=StoreRecovery.None&&storeRecovery<stage)return;
            storeRecovery=stage;storeRetryAt=Time.realtimeSinceStartupAsDouble+RetryDelay(storeRetryAttempt++);
        }
        void FetchStoreProducts()
        {
            storeRecovery=StoreRecovery.Products;storeRetryAt=Time.realtimeSinceStartupAsDouble+30;
            try{store.FetchProducts(products);}catch{ScheduleStoreRetry(StoreRecovery.Products);}
        }
        void FetchStorePurchases()
        {
            if(storeRecovery!=StoreRecovery.Products){storeRecovery=StoreRecovery.Purchases;storeRetryAt=Time.realtimeSinceStartupAsDouble+30;}
            try{store.FetchPurchases();}catch{ScheduleStoreRetry(StoreRecovery.Purchases);}
        }
        bool ServiceOnline=>Application.internetReachability!=NetworkReachability.NotReachable;
        void TickServiceRecovery()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            RecoverStalledConfirmations(now);
            if(now>=consentRetryAt){consentRetryAt=double.PositiveInfinity;RetryConsent();}
            if(now>=rewardLoadAt){rewardLoadAt=double.PositiveInfinity;LoadAd();}
            if(now>=interstitialLoadAt){interstitialLoadAt=double.PositiveInfinity;LoadInterstitial();}
            if(store!=null&&ServiceOnline&&now>=storeRetryAt&&!storeConnecting)
            {
                var stage=storeRecovery;storeRecovery=StoreRecovery.None;
                if(stage==StoreRecovery.Connect||stage!=StoreRecovery.None&&!storeConnected)Reconnect();
                else if(stage==StoreRecovery.Products)FetchStoreProducts();
                else if(stage==StoreRecovery.Purchases)FetchStorePurchases();
            }
            if(now>=nextDeliveryRetry&&session&&session.Initialized&&session.persistence)
            {
                nextDeliveryRetry=now+5;
                foreach(var order in waitingForSession.Values.ToArray())DeliverPending(order,false);
            }
            if(subscribed&&!initializedAds&&now>=maxRetryAt&&ServiceOnline&&consent&&consent.CanRequestAds)
            {
                // Recover a missed managed callback without pretending initialization succeeded.
                if(MaxSdk.IsInitialized())OnAdsInitialized(MaxSdk.GetSdkConfiguration());
                else{maxStarting=false;TryInitializeMax();}
            }
        }
        bool PrepareIosTracking()
        {
            if(Application.platform!=RuntimePlatform.IPhonePlayer)return true;
            if(!tracking){tracking=GetComponent<LabTrackingAuthorization>();if(!tracking)tracking=gameObject.AddComponent<LabTrackingAuthorization>();tracking.Completed+=InitializeAds;}
            tracking.Begin();return tracking.Complete;
        }
        void TryInitializeMax()
        {
            if(initializedAds||maxStarting||Time.realtimeSinceStartupAsDouble<maxRetryAt)return;
            maxStarting=true;maxRetryAt=Time.realtimeSinceStartupAsDouble+30+RetryDelay(maxRetryAttempt++);
            try{MaxSdk.InitializeSdk();}catch(Exception e){maxStarting=false;Debug.LogWarning("MAX initialization will retry: "+e.GetType().Name);}
        }
    }
}
