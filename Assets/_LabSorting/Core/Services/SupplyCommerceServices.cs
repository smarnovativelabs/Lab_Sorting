using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
namespace LabSorting.Services
{
    public sealed partial class SupplyCommerce
    {
        LabConsent consent;bool rewardGranted,interstitialPending;float nextInterstitial;
        public bool AdsRemoved=>CommerceEntitlements.RemoveAds;
        public string RemoveAdsPrice{get{var p=settings?store?.GetProductById(settings.removeAdsProductId):null;return p!=null&&p.availableToPurchase?p.metadata.localizedPriceString:null;}}
        public void BuyRemoveAds(){if(AdsRemoved||busy||store==null||RemoveAdsPrice==null||string.IsNullOrWhiteSpace(settings.googlePlayPublicKey)){Status="Store unavailable or already owned.";return;}busy=true;try{store.PurchaseProduct(settings.removeAdsProductId);}catch{busy=false;Status="Store unavailable.";}}
        public void RestorePurchases(){if(store==null){Status="Store unavailable.";return;}store.RestoreTransactions((success,error)=>{Status=success?"Restore requested.":"Restore unavailable.";if(success)store.FetchPurchases();});}
        public void RetryServices(){if(consent&&!consent.CanRequestAds)consent.Gather(settings.underAgeOfConsent);if(store!=null)Reconnect();}
        async void Reconnect(){try{await store.Connect();}catch{Status="Store unavailable.";}}
        void AccountChanged(){store.FetchProducts(products);}
        void RestoreRemoveAds(ConfirmedOrder order)
        {
            if(!settings||string.IsNullOrWhiteSpace(settings.googlePlayPublicKey))return;
            try{var receipts=new CrossPlatformValidator(Convert.FromBase64String(settings.googlePlayPublicKey),null,Application.identifier).Validate(order.Info.Receipt);if(receipts.Any(r=>r.productID==settings.removeAdsProductId))CommerceEntitlements.GrantRemoveAds();}catch{Status="Restore verification pending.";}
        }
        public void ShowPrivacyOptions(){if(consent)consent.PrivacyOptions();}
        void InitializeAds()
        {
            if(subscribed||!consent.CanRequestAds||string.IsNullOrWhiteSpace(settings.maxSdkKey)||string.IsNullOrWhiteSpace(settings.androidRewardedId)||string.IsNullOrWhiteSpace(settings.googleMobileAdsAppId))return;
            MaxSdkCallbacks.OnSdkInitializedEvent+=OnAdsInitialized;MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent+=OnReward;MaxSdkCallbacks.Rewarded.OnAdHiddenEvent+=OnAdHidden;MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent+=OnAdFailed;MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent+=OnAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent+=AdRevenue;MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent+=AdRevenue;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent+=InterstitialHidden;MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent+=InterstitialFailed;MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent+=InterstitialLoadFailed;
            subscribed=true;nextInterstitial=Time.unscaledTime+Mathf.Max(180,settings.interstitialCooldownSeconds);
#pragma warning disable CS0618
            MaxSdk.SetSdkKey(settings.maxSdkKey);
#pragma warning restore CS0618
            if(settings.testDeviceAdvertisingIds!=null&&settings.testDeviceAdvertisingIds.Length>0)MaxSdk.SetTestDeviceAdvertisingIdentifiers(settings.testDeviceAdvertisingIds);
            MaxSdk.InitializeSdk();
        }
        void AdRevenue(string id,MaxSdkBase.AdInfo info)=>LabTelemetry.Revenue(info);
        void CompletedRack(LabSorting.Sorting.SortRack rack){interstitialPending=true;}
        void LoadInterstitial(){if(initializedAds&&!AdsRemoved&&!string.IsNullOrWhiteSpace(settings.androidInterstitialId))MaxSdk.LoadInterstitial(settings.androidInterstitialId);}
        void InterstitialLoadFailed(string id,MaxSdkBase.ErrorInfo error){if(id==settings.androidInterstitialId)Invoke(nameof(LoadInterstitial),30);}
        void InterstitialHidden(string id,MaxSdkBase.AdInfo info){if(id!=settings.androidInterstitialId)return;busy=false;nextInterstitial=Time.unscaledTime+Mathf.Max(180,settings.interstitialCooldownSeconds);LoadInterstitial();}
        void InterstitialFailed(string id,MaxSdkBase.ErrorInfo error,MaxSdkBase.AdInfo info){InterstitialHidden(id,info);}
        // Only at an explicit pause/settings boundary after completing a rack, never mid-sort.
        public bool TryInterstitialAtBreak()
        {
            if(!initializedAds||!consent.CanRequestAds||busy||AdsRemoved||!interstitialPending||Time.unscaledTime<nextInterstitial||!MaxSdk.IsInterstitialReady(settings.androidInterstitialId))return false;
            busy=true;interstitialPending=false;try{MaxSdk.ShowInterstitial(settings.androidInterstitialId);LabTelemetry.Event("interstitial_shown");return true;}catch{busy=false;return false;}
        }
        void CleanupAds()
        {
            if(consent)consent.Changed-=InitializeAds;if(session)session.RackCompleted-=CompletedRack;
            if(!subscribed)return;MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent-=AdRevenue;MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent-=AdRevenue;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent-=InterstitialHidden;MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent-=InterstitialFailed;MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent-=InterstitialLoadFailed;
        }
    }
}
