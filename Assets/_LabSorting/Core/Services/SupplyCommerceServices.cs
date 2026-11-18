using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
namespace LabSorting.Services
{
    public sealed partial class SupplyCommerce
    {
        LabConsent consent;bool rewardGranted;float interstitialTimer,nextReadyCheck;int consentRetries;
        LabSorting.UI.GuidedTutorial tutorial;bool tutorialSearched;
        public bool AdsRemoved=>CommerceEntitlements.RemoveAds;
        public string RemoveAdsPrice{get{var p=settings?store?.GetProductById(settings.removeAdsProductId):null;return p!=null&&p.availableToPurchase?p.metadata.localizedPriceString:null;}}
        public void BuyRemoveAds(){if(AdsRemoved||busy||store==null||RemoveAdsPrice==null||string.IsNullOrWhiteSpace(settings.googlePlayPublicKey)){Status="Store unavailable or already owned.";return;}BeginPurchase();try{store.PurchaseProduct(settings.removeAdsProductId);}catch{busy=false;Status="Store unavailable.";}}
        public void RestorePurchases(){if(store==null){Status="Store unavailable.";return;}store.RestoreTransactions((success,error)=>{Status=success?"Restore requested.":"Restore unavailable.";if(success)store.FetchPurchases();});}
        public void RetryServices(){if(consent&&!consent.CanRequestAds){consentRetries=0;consent.Gather(settings.underAgeOfConsent);}else if(initializedAds){LoadAd();LoadInterstitial();}if(store!=null)Reconnect();var telemetry=GetComponent<LabTelemetry>();if(telemetry)telemetry.Initialize();}
        async void Reconnect(){try{await store.Connect();}catch{Status="Store unavailable.";}}
        void AccountChanged(){store.FetchProducts(products);}
        void RestoreRemoveAds(ConfirmedOrder order)
        {
            if(!settings||string.IsNullOrWhiteSpace(settings.googlePlayPublicKey))return;
            try{if(VerifiedReceipt(order,settings.removeAdsProductId,null))CommerceEntitlements.GrantRemoveAds();}catch{Status="Restore verification pending.";}
        }
        public void ShowPrivacyOptions(){if(Application.isEditor){Status="Ad privacy options open on Android devices.";return;}if(consent)consent.PrivacyOptions();}
        void InitializeAds()
        {
            if(!string.IsNullOrEmpty(consent.Status))Status=consent.Status;
            if(!consent.CanRequestAds){CancelInvoke(nameof(LoadAd));CancelInvoke(nameof(LoadInterstitial));CancelAdCountdown();if(consent.Attempted&&!consent.Busy)ScheduleConsentRetry();return;}
            CancelInvoke(nameof(RetryConsent));consentRetries=0;
            if(initializedAds){LoadAd();LoadInterstitial();return;}
            if(subscribed||string.IsNullOrWhiteSpace(settings.maxSdkKey)||string.IsNullOrWhiteSpace(settings.androidRewardedId)||string.IsNullOrWhiteSpace(settings.googleMobileAdsAppId))return;
            MaxSdkCallbacks.OnSdkInitializedEvent+=OnAdsInitialized;MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent+=OnReward;MaxSdkCallbacks.Rewarded.OnAdHiddenEvent+=OnAdHidden;MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent+=OnAdFailed;MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent+=OnAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent+=AdRevenue;MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent+=AdRevenue;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent+=InterstitialHidden;MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent+=InterstitialFailed;MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent+=InterstitialLoadFailed;
            subscribed=true;ResetInterstitialTimer();
            // The SDK key is written into the Android manifest from AppLovinSettings (Integration Manager) at build time.
            // Development builds log every MAX request to logcat (tag AppLovinSdk).
            if(Debug.isDebugBuild)MaxSdk.SetVerboseLogging(true);
            if(settings.testDeviceAdvertisingIds!=null&&settings.testDeviceAdvertisingIds.Length>0)MaxSdk.SetTestDeviceAdvertisingIdentifiers(settings.testDeviceAdvertisingIds);
            MaxSdk.InitializeSdk();
        }
        // Like the Motel project: a failed UMP request retries by itself while online (5s, 10s, 20s, 40s, then every 60s)
        // instead of waiting for the player to find Settings > Retry. MAX only starts once UMP allows ads.
        void ScheduleConsentRetry(){if(IsInvoking(nameof(RetryConsent)))return;Invoke(nameof(RetryConsent),Mathf.Min(60,5*Mathf.Pow(2,Mathf.Min(consentRetries,4))));consentRetries++;}
        void RetryConsent()
        {
            if(!consent||consent.CanRequestAds||consent.Busy)return;
            var net=LabServiceRoot.Instance?LabServiceRoot.Instance.internet:null;if(!net)net=FindFirstObjectByType<InternetConnectivity>();
            if(net&&net.Status==ConnectivityStatus.Offline){ScheduleConsentRetry();return;}
            consent.Gather(settings.underAgeOfConsent);
        }
        void AdRevenue(string id,MaxSdkBase.AdInfo info)=>LabTelemetry.Revenue(info);
        void LoadInterstitial(){if(initializedAds&&consent&&consent.CanRequestAds&&!AdsRemoved&&!string.IsNullOrWhiteSpace(settings.androidInterstitialId))MaxSdk.LoadInterstitial(settings.androidInterstitialId);}
        void InterstitialLoadFailed(string id,MaxSdkBase.ErrorInfo error){if(id==settings.androidInterstitialId)Invoke(nameof(LoadInterstitial),30);}
        void InterstitialHidden(string id,MaxSdkBase.AdInfo info){if(id!=settings.androidInterstitialId)return;busy=false;ResetInterstitialTimer();LoadInterstitial();}
        // Nothing was shown: try again shortly instead of waiting a full interval.
        void InterstitialFailed(string id,MaxSdkBase.ErrorInfo error,MaxSdkBase.AdInfo info){if(id!=settings.androidInterstitialId)return;busy=false;interstitialTimer=10;LoadInterstitial();}
        public float InterstitialSecondsRemaining=>Mathf.Max(0,interstitialTimer);
        void ResetInterstitialTimer(){interstitialTimer=settings?Mathf.Max(30,settings.interstitialCooldownSeconds):120;}
        // Automatic interstitial every interstitialCooldownSeconds of active play, after a 5-4-3-2-1 on-screen warning.
        // The clock only runs while the player is actually playing (no menu/offer/settings panel, no guided tutorial,
        // no first-launch tour, no internet gate) and restarts after every interstitial and every rewarded video.
        void TickInterstitial()
        {
            if(!initializedAds||!settings||AdsRemoved||adCountdown!=null||!InterstitialMomentAllowed())return;
            if(interstitialTimer>0){interstitialTimer-=Mathf.Min(Time.unscaledDeltaTime,.25f);return;}
            if(busy||!consent||!consent.CanRequestAds||string.IsNullOrWhiteSpace(settings.androidInterstitialId)||Time.unscaledTime<nextReadyCheck)return;
            nextReadyCheck=Time.unscaledTime+.5f;
            // No fill yet: keep waiting at zero and show as soon as one loads.
            if(!MaxSdk.IsInterstitialReady(settings.androidInterstitialId))return;
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
                string rv=MaxSdk.IsRewardedAdReady(settings.androidRewardedId)?"ready":"loading";
                string inter=AdsRemoved?"removed":(MaxSdk.IsInterstitialReady(settings.androidInterstitialId)?"ready":"loading")+", next in "+Mathf.CeilToInt(InterstitialSecondsRemaining)+"s";
                return consent.Describe()+" | SDK ready | reward "+rv+" | interstitial "+inter;
            }
        }
        void CleanupAds()
        {
            if(consent)consent.Changed-=InitializeAds;
            if(!subscribed)return;MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent-=AdRevenue;MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent-=AdRevenue;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent-=InterstitialHidden;MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent-=InterstitialFailed;MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent-=InterstitialLoadFailed;
        }
    }
}
