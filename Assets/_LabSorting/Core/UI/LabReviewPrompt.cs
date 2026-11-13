using System;
using System.Collections;
using UnityEngine;
using LabSorting.Services;
namespace LabSorting.UI
{
    public sealed class LabReviewPrompt:MonoBehaviour
    {
        public SortingHUD hud;
        public GameObject root;
        public string androidStoreUrl,appleStoreUrl;
        public string preferencesPrefix="LabSorting.Review.v1.";
        public bool automatic=true;
        public bool IsOpen=>root&&root.activeSelf;
        public bool NativePending {get;private set;}
        float sessionTime,nextCheck;
        int sortedAtAttempt;
        bool previousBlocked,cursorVisible;
        CursorLockMode cursor;
        public int Attempts=>PlayerPrefs.GetInt(preferencesPrefix+"Attempts",0);
        public bool OptedOut=>PlayerPrefs.GetInt(preferencesPrefix+"Stop",0)!=0;
        long LastAttempt{get{long.TryParse(PlayerPrefs.GetString(preferencesPrefix+"Last","0"),out var value);return value;}}
        public bool Eligible(int sorted,float played,long now)=>!OptedOut&&Attempts<2&&sorted>=150&&played>=300&&(LastAttempt==0||now-LastAttempt>=30L*86400);
        public bool ValidStoreUrl(out string url)
        {
            url=Application.platform==RuntimePlatform.IPhonePlayer?appleStoreUrl:androidStoreUrl;
            return Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&(uri.Host=="play.google.com"||uri.Host=="apps.apple.com")&&string.IsNullOrEmpty(uri.UserInfo);
        }
        bool SafeMoment()
        {
            var s=hud.session;var c=LabServiceRoot.Instance?LabServiceRoot.Instance.commerce:null;var tutorial=hud.GetComponent<GuidedTutorial>();
            return s.Initialized&&!s.player.blocked&&(!c||!c.Busy)&&(!tutorial||!tutorial.IsRunning)&&s.carried.Count==0&&s.player.character.Motor.BaseVelocity.sqrMagnitude<.04f&&Time.unscaledTime-s.player.LastLookInputTime>3&&!NativePending&&!IsOpen;
        }
        void Update()
        {
            if(!hud.session.player.blocked)sessionTime+=Time.unscaledDeltaTime;
            if(!automatic||Application.isEditor||Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+4;
            if(!SafeMoment()||!Eligible(hud.session.SortedCount,sessionTime,DateTimeOffset.UtcNow.ToUnixTimeSeconds()))return;
            if(Attempts==0)StartCoroutine(TryNative());else if(hud.session.SortedCount>=Mathf.Max(300,sortedAtAttempt+50)&&ValidStoreUrl(out _))ShowFallback();
        }
        public IEnumerator TryNative()
        {
            if(NativePending||OptedOut||Attempts>=2)yield break;
            RecordAttempt();sortedAtAttempt=hud.session.SortedCount;NativePending=true;
            LabTelemetry.Event("native_review_requested");yield return NativeStoreReview.Request(result=>LabTelemetry.Event("native_review_result",result.ToString()));NativePending=false;
            // No immediate second popup: native APIs intentionally conceal display/review outcome.
        }
        void RecordAttempt(){PlayerPrefs.SetInt(preferencesPrefix+"Attempts",Attempts+1);PlayerPrefs.SetString(preferencesPrefix+"Last",DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));PlayerPrefs.Save();}
        public void ShowFallback()
        {
            if(IsOpen||OptedOut||Attempts>=2||!SafeMoment()||!ValidStoreUrl(out _))return;
            RecordAttempt();LabTelemetry.Event("review_prompt_shown");previousBlocked=hud.session.player.blocked;cursor=Cursor.lockState;cursorVisible=Cursor.visible;hud.session.player.SetBlocked(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;root.SetActive(true);root.transform.SetAsLastSibling();
        }
        public void OpenStore(){if(!IsOpen||!ValidStoreUrl(out var url))return;PlayerPrefs.SetInt(preferencesPrefix+"Stop",1);PlayerPrefs.Save();LabTelemetry.Event("review_store_opened");Close();Application.OpenURL(url);}
        public void Dismiss(){LabTelemetry.Event("review_prompt_dismissed");PlayerPrefs.SetInt(preferencesPrefix+"Stop",1);PlayerPrefs.Save();Close();}
        void Close(){if(!IsOpen)return;root.SetActive(false);hud.session.player.SetBlocked(previousBlocked);Cursor.lockState=cursor;Cursor.visible=cursorVisible;}
        void OnDisable(){Close();NativePending=false;}
    }
}
