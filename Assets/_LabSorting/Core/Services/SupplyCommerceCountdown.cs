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
        bool BeginAdCountdown(bool rewarded)
        {
            RewardCountdown=rewarded;busy=true;adCountdownUntil=Time.unscaledTime+(rewarded?.15f:2);adCountdown=StartCoroutine(Countdown(rewarded));return true;
        }
        IEnumerator Countdown(bool rewarded)
        {
            var owner=session;
            while(Time.unscaledTime<adCountdownUntil)yield return null;
            adCountdown=null;
            bool ready=owner&&session==owner&&owner.Initialized&&owner.persistence&&initializedAds&&consent&&consent.CanRequestAds&&
                (rewarded?MaxSdk.IsRewardedAdReady(settings.androidRewardedId):!AdsRemoved&&MaxSdk.IsInterstitialReady(settings.androidInterstitialId));
            if(!ready){busy=false;rewardToken=null;Status="Video unavailable.";yield break;}
            try
            {
                if(rewarded){Status="Watching reward video...";LabTelemetry.Event("reward_ad_started",rewardKind.ToString());MaxSdk.ShowRewardedAd(settings.androidRewardedId);}
                else {interstitialPending=false;MaxSdk.ShowInterstitial(settings.androidInterstitialId);LabTelemetry.Event("interstitial_shown");}
            }
            catch {busy=false;rewardToken=null;Status="Video unavailable.";}
        }
        void CancelAdCountdown()
        {
            if(adCountdown==null)return;StopCoroutine(adCountdown);adCountdown=null;busy=false;rewardToken=null;rewardGranted=false;Status="Video cancelled. No reward used.";
        }
        void OnApplicationPause(bool paused){if(paused)CancelAdCountdown();}
        void OnApplicationFocus(bool focused){if(!focused)CancelAdCountdown();}
    }
}
