using System;
using System.Collections.Concurrent;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
namespace LabSorting.Services
{
    // Google UMP. The form only appears where a privacy message is required (EEA/UK/Switzerland, or US states when
    // configured); elsewhere Update returns NotRequired and ads may load at once. Update itself must still succeed.
    public sealed class LabConsent:MonoBehaviour
    {
        readonly ConcurrentQueue<Action> callbacks=new ConcurrentQueue<Action>();
        int requestVersion;bool updating;double updateDeadline;
        public bool Attempted {get;private set;}
        public bool Busy {get;private set;}
        // Editor test services only: UMP is a native SDK, so the Editor treats ads as allowed.
        public bool EditorBypass {get;private set;}
        public bool CanRequestAds=>Attempted&&!Busy&&(EditorBypass||ConsentInformation.CanRequestAds());
        public string Status {get;private set;}="";
        public event Action Changed;
        public void Gather(bool underAge)
        {
            if(Busy)return;
            if(Application.isEditor){Attempted=true;EditorBypass=true;Finish(null);return;}
            Busy=true;updating=true;updateDeadline=Time.realtimeSinceStartupAsDouble+30;int version=++requestVersion;
            try{ConsentInformation.Update(new ConsentRequestParameters{TagForUnderAgeOfConsent=underAge},error=>callbacks.Enqueue(()=>{if(!this||version!=requestVersion)return;updating=false;Attempted=true;if(error!=null){Finish(error.Message);return;}try{ConsentForm.LoadAndShowConsentFormIfRequired(e=>callbacks.Enqueue(()=>{if(version==requestVersion)Finish(e?.Message);}));}catch{Finish("Privacy service unavailable.");}}));}catch{Attempted=true;Finish("Privacy service unavailable.");}
        }
        public void PrivacyOptions(){if(Busy||Application.isEditor)return;if(ConsentInformation.PrivacyOptionsRequirementStatus!=PrivacyOptionsRequirementStatus.Required){Finish("No privacy form is required for this session.");return;}Busy=true;try{ConsentForm.ShowPrivacyOptionsForm(e=>callbacks.Enqueue(()=>Finish(e?.Message)));}catch{Finish("Privacy service unavailable.");}}
        // Short human summary for the development-build diagnostics line.
        public string Describe()=>!Attempted?"consent pending":Busy?"consent form open":EditorBypass?"consent editor":CanRequestAds?"consent OK ("+ConsentInformation.ConsentStatus+")":"consent blocked ("+ConsentInformation.ConsentStatus+(string.IsNullOrEmpty(Status)?"":": "+Status)+")";
        void Finish(string error){if(!this)return;updating=false;Busy=false;Status=error??"";Changed?.Invoke();}
        void Update(){for(int i=0;i<4&&callbacks.TryDequeue(out var callback);i++)callback();if(updating&&Time.realtimeSinceStartupAsDouble>=updateDeadline){requestVersion++;Attempted=true;Finish("Privacy connection timed out. Retrying.");}}
    }
}
