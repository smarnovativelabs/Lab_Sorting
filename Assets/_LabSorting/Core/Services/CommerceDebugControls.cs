#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Purchasing;

namespace LabSorting.Services
{
    // Entire fault injector is excluded from non-development players. Nothing is
    // persisted: restarting always resumes normal delivery and outbox flushing.
    public sealed class CommerceDebugControls : MonoBehaviour
    {
        public enum TestMode { None, ConfirmationStall, AfterJournal, AfterStoreSuccess }
        static CommerceDebugControls active;
        TestMode armed;
        string target;
        bool awaitingSuccess,checkpoint;
        Action resumeConfirmation;
        public string Notice {get;private set;}="Choose one test, close this panel, then make a sandbox purchase.";
        public bool Paused=>checkpoint;
        public bool Armed=>armed!=TestMode.None||target!=null;
        public static bool HoldTelemetry=>active&&(active.checkpoint||active.awaitingSuccess||active.armed==TestMode.AfterJournal||active.armed==TestMode.AfterStoreSuccess);
        public event Action CheckpointReached;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){active=null;}
        void Awake(){active=this;}
        void OnDestroy(){if(active==this)active=null;}
        void Report(string message){Notice=message;Debug.Log("[IAP TEST] "+message);}
        public bool Arm(TestMode mode)
        {
            var commerce=GetComponent<SupplyCommerce>();
            if(Armed||mode==TestMode.None||commerce&& !commerce.DebugCanArm)return false;
            armed=mode;
            Report("Armed: "+mode+". Next verified purchase only. "+(HoldTelemetry?"Telemetry handoff is held until resume/relaunch.":"Watchdog should recover after 30 seconds."));
            return true;
        }
        internal bool AfterJournal(PendingOrder order)
        {
            if(checkpoint&&target==order.Info.TransactionID)return true;
            if(armed!=TestMode.AfterJournal&&armed!=TestMode.AfterStoreSuccess)return false;
            var mode=armed;armed=TestMode.None;target=order.Info.TransactionID;
            if(mode==TestMode.AfterStoreSuccess){awaitingSuccess=true;Report("Revenue journal saved. Waiting for store confirmation; telemetry held.");return false;}
            ReachCheckpoint("Revenue journal saved BEFORE store confirmation. Force-close/relaunch now, or Resume.");
            return true;
        }
        internal bool HoldSuccessfulConfirmation(Order order,Action resume)
        {
            if(!awaitingSuccess||!(order is ConfirmedOrder)||order.Info.TransactionID!=target)return false;
            awaitingSuccess=false;resumeConfirmation=resume;
            ReachCheckpoint("Store confirmed; revenue is still on disk and unsent. Force-close/relaunch now, or Resume.");
            return true;
        }
        void ReachCheckpoint(string message){checkpoint=true;Report(message);CheckpointReached?.Invoke();}
        internal bool IsPaused(string transaction)=>checkpoint&&target==transaction;
        internal bool InjectConfirmationStall(StoreController store,PendingOrder order,Action<Order> completed)
        {
            if(armed!=TestMode.ConfirmationStall)return false;
            armed=TestMode.None;
            try
            {
                RegisterSilentConfirmation(SupplyCommerce.FindConfirmationCallback(store),order,completed);
                target=order.Info.TransactionID;
                Report("SDK confirmation request held; native finish not sent. Waiting for the real 30-second watchdog.");
                return true;
            }
            catch(Exception e){Report("Stall injection unavailable ("+e.GetType().Name+"). Purchase continues normally.");return false;}
        }
        // Insert the same request the SDK would create, without calling native finish.
        // Payment and receipt verification are real; the watchdog must clear both SDK
        // and game guards before the retry can actually confirm/consume this purchase.
        internal static void RegisterSilentConfirmation(IStorePurchaseConfirmCallback callback,PendingOrder order,Action<Order> completed)
        {
            if(callback==null)throw new InvalidOperationException("Missing IAP callback binding");
            var field=callback.GetType().GetField("m_ConfirmationRequests",BindingFlags.Instance|BindingFlags.NonPublic);
            if(field==null)throw new MissingFieldException("IAP 5.5 confirmation requests");
            var requests=(IList)field.GetValue(callback);
            var requestType=field.FieldType.GetGenericArguments()[0];
            Action<PendingOrder,Order> handler=(pending,result)=>completed(result);
            var request=Activator.CreateInstance(requestType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{order,handler},null);
            requests.Add(request);
        }
        internal void WatchdogExpired(string transaction)
        {
            if(target!=transaction||checkpoint||awaitingSuccess)return;
            target=null;Report("Watchdog released the stalled request. Normal confirmation retry is running.");
        }
        public void Resume()
        {
            var callback=resumeConfirmation;
            resumeConfirmation=null;armed=TestMode.None;checkpoint=false;awaitingSuccess=false;target=null;
            Report("Debug hold cleared. Normal delivery and telemetry resume; an injected stall still follows its watchdog.");
            callback?.Invoke();
        }
        public string State=>"Mode: "+armed+" | checkpoint: "+checkpoint+" | telemetry held: "+HoldTelemetry;
        public void CrashAtCheckpoint()
        {
            if(!checkpoint)return;
#if UNITY_EDITOR
            Report("Editor: crash disabled. Stop/restart Play Mode, or Resume.");
#else
            Debug.Log("[IAP TEST] Deliberate crash at durable purchase checkpoint.");
            UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.FatalError);
#endif
        }
    }
}
#endif
