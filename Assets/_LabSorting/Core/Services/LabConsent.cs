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
        internal const double RequestTimeoutSeconds=30,FormTimeoutSeconds=120;
        int requestVersion;double updateDeadline;
        internal int BeginRequest(double now,double timeout)
        {
            Busy=true;updateDeadline=now+timeout;return ++requestVersion;
        }
        internal void CompleteRequest(int version,string error)
        {
            if(!this||!Busy||version!=requestVersion)return;
            Finish(error);
        }
        internal void CheckTimeout(double now)
        {
            if(!Busy||now<updateDeadline)return;
            requestVersion++;Attempted=true;Finish("Privacy connection timed out. Retrying.");
        }
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
            int version=BeginRequest(Time.realtimeSinceStartupAsDouble,RequestTimeoutSeconds);
            try
            {
                ConsentInformation.Update(new ConsentRequestParameters{TagForUnderAgeOfConsent=underAge},error=>callbacks.Enqueue(()=>
                {
                    if(!this||!Busy||version!=requestVersion)return;
                    Attempted=true;
                    if(error!=null){CompleteRequest(version,error.Message);return;}
                    // Give the form its own deadline and generation. Duplicate/late update
                    // callbacks cannot show another form or finish a newer request.
                    int formVersion=BeginRequest(Time.realtimeSinceStartupAsDouble,FormTimeoutSeconds);
                    try{ConsentForm.LoadAndShowConsentFormIfRequired(e=>callbacks.Enqueue(()=>CompleteRequest(formVersion,e?.Message)));}
                    catch{CompleteRequest(formVersion,"Privacy service unavailable.");}
                }));
            }
            catch{Attempted=true;CompleteRequest(version,"Privacy service unavailable.");}
        }
        public void PrivacyOptions()
        {
            if(Busy||Application.isEditor)return;
            if(ConsentInformation.PrivacyOptionsRequirementStatus!=PrivacyOptionsRequirementStatus.Required)
            {Finish("No privacy form is required for this session.");return;}
            int version=BeginRequest(Time.realtimeSinceStartupAsDouble,FormTimeoutSeconds);
            try{ConsentForm.ShowPrivacyOptionsForm(e=>callbacks.Enqueue(()=>CompleteRequest(version,e?.Message)));}
            catch{CompleteRequest(version,"Privacy service unavailable.");}
        }
        // Short human summary for the development-build diagnostics line.
        public string Describe()=>!Attempted?"consent pending":Busy?"consent form open":EditorBypass?"consent editor":CanRequestAds?"consent OK ("+ConsentInformation.ConsentStatus+")":"consent blocked ("+ConsentInformation.ConsentStatus+(string.IsNullOrEmpty(Status)?"":": "+Status)+")";
        void Finish(string error){if(!this)return;Busy=false;Status=error??"";Changed?.Invoke();}
        void Update(){for(int i=0;i<4&&callbacks.TryDequeue(out var callback);i++)callback();CheckTimeout(Time.realtimeSinceStartupAsDouble);}
    }
}
