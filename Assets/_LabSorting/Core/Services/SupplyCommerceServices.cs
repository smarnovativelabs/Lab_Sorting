using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
namespace LabSorting.Services
{
    public sealed partial class SupplyCommerce
    {
        LabConsent consent;bool rewardGranted,restoring,storeConnected;float interstitialTimer,nextReadyCheck;int consentRetries;
        LabSorting.UI.GuidedTutorial tutorial;bool tutorialSearched;
        public bool AdsRemoved=>CommerceEntitlements.RemoveAds;
        public string RemoveAdsPrice{get{var p=settings?store?.GetProductById(settings.removeAdsProductId):null;return p!=null&&p.availableToPurchase?p.metadata.localizedPriceString:null;}}
        public void BuyRemoveAds(){if(AdsRemoved||busy||store==null||RemoveAdsPrice==null||!ReceiptValidationConfigured){Status="Store unavailable or already owned.";return;}BeginPurchase();try{store.PurchaseProduct(settings.removeAdsProductId);}catch{busy=false;Status="Store unavailable.";}}
        // Apple syncs the App Store; Google Play completes immediately. IAP itself runs
        // FetchPurchases, so PurchasesFetched reports the result. A disconnected store reconnects first
        // (Connected -> FetchProducts -> FetchPurchases). Where restore is unsupported (Editor fake store) it falls back
        // to a plain fetch.
        public void RestorePurchases()
        {
            if(store==null){Status="Store unavailable.";return;}
            restoring=true;
            if(!storeConnected){Status="Connecting to store...";Reconnect();return;}
            RestoreConnectedStore();
        }
        void RestoreConnectedStore()
        {
            Status="Restoring purchases...";
            // IAP 5 fetches purchases itself after successful Apple AppStore.sync().
            store.RestoreTransactions((success,error)=>{if(success)return;if(Application.isEditor){store.FetchPurchases();return;}restoring=false;Status="Restore cancelled or unsuccessful. Please try again.";});
        }
        public void RetryServices(){if(consent&&!consent.CanRequestAds){consentRetries=0;consent.Gather(settings.underAgeOfConsent);}else if(initializedAds){LoadAd();LoadInterstitial();}if(store!=null)Reconnect();var telemetry=GetComponent<LabTelemetry>();if(telemetry)telemetry.Initialize();}
        async void Reconnect(){if(store==null||storeConnecting)return;if(storeConnected){FetchStoreProducts();return;}storeConnecting=true;try{await store.Connect();}catch{restoring=false;Status="Store unavailable.";ScheduleStoreRetry(StoreRecovery.Connect);}finally{storeConnecting=false;}}
        void AccountChanged(){FetchStoreProducts();}
        // true: Remove Ads verified and granted again. false: not a Remove Ads order. null: verification or saving failed.
        bool? RestoreRemoveAds(ConfirmedOrder order)
        {
            if(!settings||!ReceiptValidationConfigured)return false;
            if(!order.CartOrdered.Items().Any(i=>i.Product?.definition.id==settings.removeAdsProductId))return false;
            try{if(!VerifiedReceipt(order,settings.removeAdsProductId,null))return null;return CommerceEntitlements.GrantRemoveAds()?true:(bool?)null;}catch{return null;}
        }
        public void ShowPrivacyOptions(){if(Application.isEditor){Status="Ad privacy options open on mobile devices.";return;}if(consent)consent.PrivacyOptions();}
        void InitializeAds()
        {
            if(!string.IsNullOrEmpty(consent.Status))Status=consent.Status;
            if(!consent.CanRequestAds){CancelInvoke(nameof(LoadAd));CancelInvoke(nameof(LoadInterstitial));CancelAdCountdown();if(consent.Attempted&&!consent.Busy)ScheduleConsentRetry();return;}
            CancelInvoke(nameof(RetryConsent));consentRetryAt=double.PositiveInfinity;consentRetries=0;
            if(initializedAds){LoadAd();LoadInterstitial();return;}
            if(string.IsNullOrWhiteSpace(settings.maxSdkKey)||string.IsNullOrWhiteSpace(settings.RewardedId)||string.IsNullOrWhiteSpace(settings.MobileAdsAppId))return;
            if(!PrepareIosTracking())return;
            // MAX revenue callbacks default to a background thread. Our handlers
            // persist telemetry and update game state, so dispatch all callbacks on Unity's thread.
            MaxSdkBase.InvokeEventsOnUnityMainThread=true;
            if(subscribed){TryInitializeMax();return;}
            MaxSdkCallbacks.OnSdkInitializedEvent+=OnAdsInitialized;MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent+=OnReward;MaxSdkCallbacks.Rewarded.OnAdHiddenEvent+=OnAdHidden;MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent+=OnAdFailed;MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent+=OnAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent+=AdRevenue;MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent+=AdRevenue;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent+=InterstitialHidden;MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent+=InterstitialFailed;MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent+=InterstitialLoadFailed;
            // First interstitial of the launch comes sooner (90 s); every later one waits the full cooldown (120 s).
            subscribed=true;interstitialTimer=settings?Mathf.Max(30,settings.firstInterstitialSeconds):90;
            // The SDK key is written into the Android manifest from AppLovinSettings (Integration Manager) at build time.
            // Development builds log every MAX request to logcat (tag AppLovinSdk).
            if(Debug.isDebugBuild)MaxSdk.SetVerboseLogging(true);
            if(settings.testDeviceAdvertisingIds!=null&&settings.testDeviceAdvertisingIds.Length>0)MaxSdk.SetTestDeviceAdvertisingIdentifiers(settings.testDeviceAdvertisingIds);
            TryInitializeMax();
        }
        // Like the Motel project: a failed UMP request retries by itself while online (5s, 10s, 20s, 40s, then every 60s)
        // instead of waiting for the player to find Settings > Retry. MAX only starts once UMP allows ads.
        void ScheduleConsentRetry(){if(!double.IsPositiveInfinity(consentRetryAt))return;consentRetryAt=Time.realtimeSinceStartupAsDouble+RetryDelay(consentRetries++);}
        void RetryConsent()
        {
            if(!consent||consent.CanRequestAds||consent.Busy)return;
            var net=LabServiceRoot.Instance?LabServiceRoot.Instance.internet:null;if(!net)net=FindFirstObjectByType<InternetConnectivity>();
            if(net&&net.Status==ConnectivityStatus.Offline){ScheduleConsentRetry();return;}
            consent.Gather(settings.underAgeOfConsent);
        }
        void AdRevenue(string id,MaxSdkBase.AdInfo info)=>LabTelemetry.Revenue(info);
        void LoadInterstitial(){if(initializedAds&&consent&&consent.CanRequestAds&&!AdsRemoved&&!string.IsNullOrWhiteSpace(settings.InterstitialId))MaxSdk.LoadInterstitial(settings.InterstitialId);}
        void InterstitialLoadFailed(string id,MaxSdkBase.ErrorInfo error){if(id==settings.InterstitialId)interstitialLoadAt=Time.realtimeSinceStartupAsDouble+30;}
        void InterstitialHidden(string id,MaxSdkBase.AdInfo info){if(id!=settings.InterstitialId)return;busy=false;ResetInterstitialTimer();LoadInterstitial();}
        // Nothing was shown: try again shortly instead of waiting a full interval.
        void InterstitialFailed(string id,MaxSdkBase.ErrorInfo error,MaxSdkBase.AdInfo info){if(id!=settings.InterstitialId)return;busy=false;interstitialTimer=10;LoadInterstitial();}
        public float InterstitialSecondsRemaining=>Mathf.Max(0,interstitialTimer);
        void ResetInterstitialTimer(){interstitialTimer=settings?Mathf.Max(30,settings.interstitialCooldownSeconds):120;}
        // Automatic interstitial every interstitialCooldownSeconds of active play, after a 5-4-3-2-1 on-screen warning.
        // The clock only runs while the player is actually playing (no menu/offer/settings panel, no guided tutorial,
        // no first-launch tour, no internet gate) and restarts after every interstitial and every rewarded video.
        void TickInterstitial()
        {
            if(!initializedAds||!settings||AdsRemoved||adCountdown!=null||!InterstitialMomentAllowed())return;
            if(interstitialTimer>0){interstitialTimer-=Mathf.Min(Time.unscaledDeltaTime,.25f);return;}
            if(busy||!consent||!consent.CanRequestAds||string.IsNullOrWhiteSpace(settings.InterstitialId)||Time.unscaledTime<nextReadyCheck)return;
            nextReadyCheck=Time.unscaledTime+.5f;
            // No fill yet: keep waiting at zero and show as soon as one loads.
            if(!MaxSdk.IsInterstitialReady(settings.InterstitialId))return;
            purchaseRequested=false;BeginAdCountdown(false);
        }
        bool InterstitialMomentAllowed()
        {
            if(!session||!session.Initialized||!session.player||session.player.blocked)return false;
            if(!tutorialSearched){tutorialSearched=true;tutorial=FindFirstObjectByType<LabSorting.UI.GuidedTutorial>(FindObjectsInactive.Include);}
            return !(tutorial&&tutorial.IsRunning);
        }
        // Development builds: MAX Mediation Debugger shows the SDK key, package, ad units, adapters and test mode on device.
        public void ShowAdDebugger(){if(!initializedAds){Status="Ads SDK not started: "+AdDiagnostics;return;}MaxSdk.ShowMediationDebugger();}
        public string AdDiagnostics
        {
            get
            {
                if(!settings)return "no service settings";
                if(!consent)return Application.isEditor?(EditorTestServices?"starting":"Editor test services off"):settings.enableLiveServices?"starting":"live services off";
                if(!initializedAds)return consent.Describe()+" | SDK not initialized";
                string rv=MaxSdk.IsRewardedAdReady(settings.RewardedId)?"ready":"loading";
                string inter=AdsRemoved?"removed":(MaxSdk.IsInterstitialReady(settings.InterstitialId)?"ready":"loading")+", next in "+Mathf.CeilToInt(InterstitialSecondsRemaining)+"s";
                return consent.Describe()+" | SDK ready | reward "+rv+" | interstitial "+inter;
            }
        }
        void CleanupAds()
        {
            if(tracking)tracking.Completed-=InitializeAds;
            if(consent)consent.Changed-=InitializeAds;
            if(!subscribed)return;MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent-=AdRevenue;MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent-=AdRevenue;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent-=InterstitialHidden;MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent-=InterstitialFailed;MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent-=InterstitialLoadFailed;
        }
    }
}
