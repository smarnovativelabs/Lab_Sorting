using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace LabSorting.Services
{
    // Own ATT separately from MAX initialization: a missing callback must not block ads.
    public sealed class LabTrackingAuthorization:MonoBehaviour
    {
        public const double TimeoutSeconds=10;
        public bool Complete {get;private set;}
        public event Action Completed;
        bool requested;double deadline;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int LabTrackingStatus();
        [DllImport("__Internal")] static extern void LabRequestTracking();
#endif
        public void Begin()
        {
            if(requested)return;requested=true;deadline=Time.realtimeSinceStartupAsDouble+TimeoutSeconds;
            try{
#if UNITY_IOS && !UNITY_EDITOR
                LabRequestTracking();
#endif
            }catch(Exception e){Debug.LogWarning("ATT unavailable; bounded wait continues: "+e.GetType().Name);}
        }
        internal static bool WaitFinished(int status,double now,double deadline)=>status!=0||now>=deadline;
        void Update()
        {
            if(!requested||Complete)return;
            int status=0;
            try{
#if UNITY_IOS && !UNITY_EDITOR
                status=LabTrackingStatus();
#else
                status=2;
#endif
            }catch{ }
            if(!WaitFinished(status,Time.realtimeSinceStartupAsDouble,deadline))return;
            Complete=true;GetComponent<LabTelemetry>()?.CompleteIosConsent();Completed?.Invoke();
        }
    }
}
