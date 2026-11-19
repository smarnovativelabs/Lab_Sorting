using System.Collections;
using UnityEngine;
namespace LabSorting.Services
{
    public sealed partial class SupplyCommerce
    {
        Coroutine adCountdown;
        float adCountdownUntil;
        public bool RewardCountdown {get;private set;}
        public float AdCountdownSeconds=>adCountdown==null?0:Mathf.Max(0,adCountdownUntil-Time.unscaledTime);
        // Rewarded videos start almost at once (the player asked for them). Automatic interstitials show the
        // "Displaying Ad In: 5, 4, 3, 2, 1" warning first (DesignerToasts reads AdCountdownSeconds).
        bool BeginAdCountdown(bool rewarded)
        {
            float warning=rewarded?.15f:(settings?Mathf.Clamp(settings.interstitialWarningSeconds,1,10):5);
            RewardCountdown=rewarded;busy=true;adCountdownUntil=Time.unscaledTime+warning;adCountdown=StartCoroutine(Countdown(rewarded));return true;
        }
        IEnumerator Countdown(bool rewarded)
        {
            var owner=session;
            while(Time.unscaledTime<adCountdownUntil)yield return null;
            adCountdown=null;
            bool ready=owner&&session==owner&&owner.Initialized&&owner.persistence&&initializedAds&&consent&&consent.CanRequestAds&&
                (rewarded?MaxSdk.IsRewardedAdReady(settings.RewardedId):!AdsRemoved&&InterstitialMomentAllowed()&&MaxSdk.IsInterstitialReady(settings.InterstitialId));
            // An interstitial that became unavailable (menu opened, ad expired) simply waits for the next allowed moment.
            if(!ready){busy=false;rewardToken=null;if(rewarded)Status="Video unavailable.";yield break;}
            try
            {
                if(rewarded){Status="Watching reward video...";LabTelemetry.Event("reward_ad_started",rewardKind.ToString());MaxSdk.ShowRewardedAd(settings.RewardedId);}
                else {MaxSdk.ShowInterstitial(settings.InterstitialId);LabTelemetry.Event("interstitial_shown");}
            }
            catch {busy=false;rewardToken=null;if(rewarded)Status="Video unavailable.";else interstitialTimer=10;}
        }
        void CancelAdCountdown()
        {
            if(adCountdown==null)return;StopCoroutine(adCountdown);adCountdown=null;busy=false;bool rewarded=RewardCountdown;rewardToken=null;rewardGranted=false;if(rewarded)Status="Video cancelled. No reward used.";
        }
        void OnApplicationPause(bool paused){if(paused)CancelAdCountdown();else if(store!=null&&storeRecovery==StoreRecovery.None)ScheduleStoreRetry(storeConnected?StoreRecovery.Purchases:StoreRecovery.Connect);}
        void OnApplicationFocus(bool focused){if(!focused)CancelAdCountdown();}
    }
}
