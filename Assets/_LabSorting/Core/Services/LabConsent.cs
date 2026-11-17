using System;
using System.Collections.Concurrent;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
namespace LabSorting.Services
{
    public sealed class LabConsent:MonoBehaviour
    {
        readonly ConcurrentQueue<Action> callbacks=new ConcurrentQueue<Action>();
        public bool Attempted {get;private set;}
        public bool Busy {get;private set;}
        public bool CanRequestAds=>Attempted&&!Busy&&ConsentInformation.CanRequestAds();
        public string Status {get;private set;}="";
        public event Action Changed;
        public void Gather(bool underAge)
        {
            if(Busy||Application.isEditor)return;Busy=true;
            try{ConsentInformation.Update(new ConsentRequestParameters{TagForUnderAgeOfConsent=underAge},error=>callbacks.Enqueue(()=>{if(!this)return;Attempted=true;if(error!=null){Finish(error.Message);return;}try{ConsentForm.LoadAndShowConsentFormIfRequired(e=>callbacks.Enqueue(()=>Finish(e?.Message)));}catch{Finish("Privacy service unavailable.");}}));}catch{Attempted=true;Finish("Privacy service unavailable.");}
        }
        public void PrivacyOptions(){if(Busy||Application.isEditor)return;if(ConsentInformation.PrivacyOptionsRequirementStatus!=PrivacyOptionsRequirementStatus.Required){Finish("No privacy form is required for this session.");return;}Busy=true;try{ConsentForm.ShowPrivacyOptionsForm(e=>callbacks.Enqueue(()=>Finish(e?.Message)));}catch{Finish("Privacy service unavailable.");}}
        void Finish(string error){if(!this)return;Busy=false;Status=error??"";Changed?.Invoke();}
        void Update(){for(int i=0;i<4&&callbacks.TryDequeue(out var callback);i++)callback();}
    }
}
