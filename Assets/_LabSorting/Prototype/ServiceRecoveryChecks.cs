#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using LabSorting.Services;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
namespace LabSorting.Prototype
{
    public static class ServiceRecoveryChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        public static void ReceiptToken()
        {
            var receipt=new GooglePlayReceipt("coins","GPA.order","com.test","token",DateTime.UtcNow,GooglePurchaseState.Purchased);
            Check(SupplyCommerce.GoogleReceiptMatches(receipt,"coins","token"),"Valid signed receipt identity must use purchaseToken, not orderID");
            Check(!SupplyCommerce.GoogleReceiptMatches(receipt,"coins","GPA.order"),"Order ID is not a token");
            Check(!SupplyCommerce.GoogleReceiptMatches(receipt,"other","token")&&!SupplyCommerce.GoogleReceiptMatches(receipt,"coins",null),"Reject wrong product and missing token");
            var refunded=new GooglePlayReceipt("coins","GPA.order","com.test","token",DateTime.UtcNow,GooglePurchaseState.Refunded);
            Check(!SupplyCommerce.GoogleReceiptMatches(refunded,"coins","token"),"Reject refunded purchase");
        }
        public static void IndependentTelemetryRetry()
        {
            var box=new TelemetryOutbox();box.Add(new TelemetryMessage{kind="purchase",transaction="p1"});box.Add(new TelemetryMessage{kind="ad",value=.01});
            int firebase=0,tenjin=0;
            Check(!box.Drain(m=>false,m=>false)&&box.messages.Count==2,"Unavailable SDKs must retain events");
            box.Drain(m=>{firebase++;return true;},m=>throw new Exception("SDK failure"));
            Check(firebase==2&&box.messages.Count==2,"One failing destination must not block the other");
            box.Drain(m=>{firebase++;return true;},m=>{tenjin++;return true;});
            Check(firebase==2&&tenjin==2&&box.messages.Count==0,"Retry must not duplicate successful destination");
            Check(!box.Add(new TelemetryMessage{kind="purchase",transaction="p1"}),"Duplicate purchase callback suppressed after delivery");
        }
        public static void DurableTelemetry()
        {
            string path=Path.Combine(Application.temporaryCachePath,"telemetry-test-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                var box=new TelemetryOutbox();box.Add(new TelemetryMessage{kind="purchase",transaction="durable",value=2.99,currency="USD",receipt="signed"});
                box.Drain(m=>true,m=>false);Check(box.Save(path),"Outbox persists");
                box=TelemetryOutbox.Load(path);Check(box.messages.Count==1&&box.messages[0].firebaseSent&&!box.messages[0].tenjinSent,"Partial delivery survives restart");
                Check(!box.Add(new TelemetryMessage{kind="purchase",transaction="durable"}),"Persistent transaction deduplication");
                box.Drain(m=>throw new Exception("Already delivered"),m=>m.receipt=="signed");Check(box.messages.Count==0&&box.Save(path),"Remaining destination replays after restart");
                Check(TelemetryOutbox.Load(path).purchases.Contains("durable"),"Completed transaction retained for deduplication");
                File.WriteAllText(path,"invalid json");Check(TelemetryOutbox.Load(path).messages.Count==1,"Corrupt primary recovers backup");
            }
            finally{foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
        }
        public static void TelemetryCapacity()
        {
            var box=new TelemetryOutbox();box.Add(new TelemetryMessage{kind="purchase",transaction="keep"});
            for(int i=0;i<300;i++)box.Add(new TelemetryMessage{kind="event",name="test"});
            Check(box.messages.Count==257&&box.messages[0].transaction=="keep","Gameplay bound must not discard revenue");
        }
        public static void RetryAndAttTimeout()
        {
            Check(SupplyCommerce.RetryDelay(0)==5&&SupplyCommerce.RetryDelay(1)==10&&SupplyCommerce.RetryDelay(20)==60,"Backoff must be capped");
            Check(!LabTrackingAuthorization.WaitFinished(0,9,10)&&LabTrackingAuthorization.WaitFinished(0,10,10),"Missing ATT response ends at deadline");
            Check(LabTrackingAuthorization.WaitFinished(2,1,10)&&LabTrackingAuthorization.WaitFinished(3,1,10),"Denial and approval both release flow");
            var go=new GameObject("ATT test");try{var att=go.AddComponent<LabTrackingAuthorization>();int completions=0;att.Completed+=()=>completions++;att.Begin();var update=typeof(LabTrackingAuthorization).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);update.Invoke(att,null);update.Invoke(att,null);att.Begin();Check(completions==1,"ATT completion must only initialize downstream once");}finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        public static void ConfirmationWatchdog()
        {
            var go=new GameObject("Confirmation watchdog test");
            try
            {
                var c=go.AddComponent<SupplyCommerce>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var confirming=(HashSet<string>)typeof(SupplyCommerce).GetField("confirmRequested",flags).GetValue(c);
                var deadlines=(Dictionary<string,double>)typeof(SupplyCommerce).GetField("confirmationDeadlines",flags).GetValue(c);
                confirming.Add("stalled");deadlines.Add("stalled",30);
                confirming.Add("active");deadlines.Add("active",60);
                c.RecoverStalledConfirmations(29);Check(confirming.Contains("stalled"),"Do not retry an active confirmation");
                c.RecoverStalledConfirmations(30);
                Check(!confirming.Contains("stalled")&&!deadlines.ContainsKey("stalled"),"Missing callback must release confirmation guard");
                Check(confirming.Contains("active"),"Independent confirmations keep their deadlines");
                // A late success must make subsequent pending deliveries harmless.
                typeof(SupplyCommerce).GetMethod("Confirmed",flags).Invoke(c,new object[]{new ConfirmedOrder(new Cart(new List<CartItem>()),Info("stalled"))});
                var completed=(HashSet<string>)typeof(SupplyCommerce).GetField("confirmedTransactions",flags).GetValue(c);
                Check(completed.Contains("stalled"),"Late success must still complete the transaction");
                c.RecoverStalledConfirmations(60);Check(confirming.Count==0,"Second stalled confirmation also recovers");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        public static void SdkConfirmationTimeout()
        {
            // Exercise the actual installed IAP request list and failure handler, without
            // a live store. This also detects SDK layout changes breaking the adapter.
            var assembly=typeof(PendingOrder).Assembly;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var useCaseType=assembly.GetType("UnityEngine.Purchasing.ConfirmOrderUseCase");
            var serviceType=assembly.GetType("UnityEngine.Purchasing.PurchaseService");
            var useCase=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(useCaseType);
            var field=useCaseType.GetField("m_ConfirmationRequests",flags);
            var requests=(System.Collections.IList)Activator.CreateInstance(field.FieldType);field.SetValue(useCase,requests);
            var service=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(serviceType);
            serviceType.GetField("m_ConfirmOrderUseCase",flags).SetValue(service,useCase);
            var controller=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StoreController));
            typeof(StoreController).GetField("m_PurchaseService",flags).SetValue(controller,service);
            var order=new PendingOrder(new Cart(new List<CartItem>()),Info("sdk-stalled"));int failures=0;
            Action<PendingOrder,Order> completed=(pending,result)=>{Check(result is FailedOrder,"Timeout must never fake successful confirmation");failures++;};
            var request=Activator.CreateInstance(assembly.GetType("UnityEngine.Purchasing.ConfirmOrderRequest"),flags,null,new object[]{order,completed},null);
            requests.Add(request);
            var callback=SupplyCommerce.FindConfirmationCallback(controller);Check(callback!=null,"Installed IAP recovery binding must resolve");
            callback.OnConfirmOrderFailed(new FailedOrder(order,PurchaseFailureReason.Unknown,"timeout"));
            Check(failures==1&&requests.Count==0,"SDK pending request must clear so retry reaches native store");
            callback.OnConfirmOrderSucceeded("sdk-stalled");
            Check(failures==1&&requests.Count==0,"Late native callback must not repeat completion");
        }
        public static void PurchaseJournalCrashRecovery()
        {
            string dir=Path.Combine(Application.temporaryCachePath,"purchase-journal-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(dir,"outbox.json");
            try
            {
                var box=new TelemetryOutbox();
                var message=new TelemetryMessage{kind="purchase",transaction="before-confirm",product="coins",value=2.99,currency="USD",receipt="signed"};
                Check(!box.JournalPurchase(message,path),"Disk failure must block store confirmation");
                Directory.CreateDirectory(dir);
                Check(box.JournalPurchase(message,path),"Duplicate in-memory retry must actually save to disk");
                // Simulate process death after journaling and store consumption, before callback.
                box=TelemetryOutbox.Load(path);
                Check(box.messages.Count==1&&box.messages[0].transaction=="before-confirm","Revenue survives without a confirmation callback");
                Check(box.JournalPurchase(message,path)&&box.messages.Count==1,"Redelivered order must not duplicate revenue");
                int firebase=0,tenjin=0;
                box.Drain(m=>{firebase++;return true;},m=>false);Check(box.Save(path),"Persist partial handoff");
                box=TelemetryOutbox.Load(path);
                box.Drain(m=>{firebase++;return true;},m=>{tenjin++;return true;});Check(box.Save(path),"Persist full handoff");
                box=TelemetryOutbox.Load(path);Check(box.JournalPurchase(message,path),"Late confirmation can reuse durable entry");
                Check(firebase==1&&tenjin==1&&box.messages.Count==0,"Restart and late callback must not duplicate SDK handoff");
            }
            finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        public static void ConsentCallbackTimeout()
        {
            var go=new GameObject("Consent timeout test");
            try
            {
                var consent=go.AddComponent<LabConsent>();int changes=0;consent.Changed+=()=>changes++;
                int old=consent.BeginRequest(0,LabConsent.FormTimeoutSeconds);
                consent.CheckTimeout(119);Check(consent.Busy&&changes==0,"Form must retain time for user input");
                consent.CheckTimeout(120);Check(!consent.Busy&&consent.Attempted&&changes==1,"Missing form callback releases Busy and notifies recovery");
                Check(!consent.EditorBypass,"Timeout must not manufacture consent");
                int current=consent.BeginRequest(121,LabConsent.FormTimeoutSeconds);
                consent.CompleteRequest(old,null);Check(consent.Busy&&changes==1,"Late old callback cannot complete newer request");
                consent.CompleteRequest(current,null);Check(!consent.Busy&&changes==2,"Current callback completes once");
                consent.CompleteRequest(current,null);consent.CheckTimeout(999);Check(changes==2,"Duplicate callback and timer cannot complete twice");
                consent.BeginRequest(1000,LabConsent.RequestTimeoutSeconds);consent.CheckTimeout(1030);
                Check(!consent.Busy&&changes==3,"Initial UMP update timeout still recovers");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        public static void DebugCheckpointControls()
        {
            var go=new GameObject("Debug checkpoint test");
            try
            {
                var debug=go.AddComponent<CommerceDebugControls>();
                // EditMode does not invoke Awake automatically.
                typeof(CommerceDebugControls).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(debug,null);
                var pending=new PendingOrder(new Cart(new List<CartItem>()),Info("debug-journal"));
                Check(debug.Arm(CommerceDebugControls.TestMode.AfterJournal)&&CommerceDebugControls.HoldTelemetry,"Arming journal test holds telemetry before the purchase");
                Check(!debug.Arm(CommerceDebugControls.TestMode.ConfirmationStall),"Cannot overwrite an armed fault");
                Check(debug.AfterJournal(pending)&&debug.Paused&&debug.IsPaused("debug-journal"),"Checkpoint holds delivery only after journal step");
                Check(debug.AfterJournal(pending),"Automatic delivery retries cannot bypass checkpoint");
                debug.Resume();Check(!CommerceDebugControls.HoldTelemetry&&!debug.AfterJournal(pending),"Resume releases delivery and telemetry");
                Check(debug.Arm(CommerceDebugControls.TestMode.AfterStoreSuccess),"Can arm another test after resume");
                Check(!debug.AfterJournal(pending)&&CommerceDebugControls.HoldTelemetry,"Post-confirm test allows store finish but retains revenue");
                int resumed=0;
                Check(!debug.HoldSuccessfulConfirmation(new FailedOrder(pending,PurchaseFailureReason.Unknown,"network"),()=>resumed++),"Failure must follow real retry flow");
                Check(debug.HoldSuccessfulConfirmation(new ConfirmedOrder(pending.CartOrdered,pending.Info),()=>resumed++),"Success stops before application callback processing");
                Check(debug.Paused&&resumed==0,"Callback remains held at checkpoint");
                debug.Resume();debug.Resume();Check(resumed==1&&!CommerceDebugControls.HoldTelemetry,"Held callback resumes exactly once");
                debug.Arm(CommerceDebugControls.TestMode.AfterJournal);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
            Check(!CommerceDebugControls.HoldTelemetry,"Destroy/restart cannot retain a debug hold");
        }
        public static void DebugSdkStallInjection()
        {
            var assembly=typeof(PendingOrder).Assembly;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=assembly.GetType("UnityEngine.Purchasing.ConfirmOrderUseCase");
            var callback=(IStorePurchaseConfirmCallback)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            var field=type.GetField("m_ConfirmationRequests",flags);
            var requests=(System.Collections.IList)Activator.CreateInstance(field.FieldType);field.SetValue(callback,requests);
            var pending=new PendingOrder(new Cart(new List<CartItem>()),Info("injected-stall"));int failures=0;
            CommerceDebugControls.RegisterSilentConfirmation(callback,pending,result=>{Check(result is FailedOrder,"Watchdog releases injection as a failure, not a fake reward");failures++;});
            Check(requests.Count==1&&failures==0,"Injection retains an actual SDK request without finishing the native transaction");
            callback.OnConfirmOrderFailed(new FailedOrder(pending,PurchaseFailureReason.Unknown,"timeout"));
            Check(requests.Count==0&&failures==1,"Production timeout callback clears injected SDK lock");
        }
        public static void TenjinRevenueSerialization()
        {
            // Both layouts must exist on every target, including an Android-active
            // Editor exporting iOS. Preserve integer precision and iOS units.
            const long micros=9007199254740993;
            var android=new AdMobImpressionDataToJSON{currency_code="USD",value_micros=micros};
            var json=JsonUtility.ToJson(android);
            Check(JsonUtility.FromJson<AdMobImpressionDataToJSON>(json).value_micros==micros,"Android micros must retain 64-bit precision");
            var ios=new AdMobIosImpressionDataToJSON{currency_code="USD",value_micros=1234567/1000000.0};
            Check(Math.Abs(JsonUtility.FromJson<AdMobIosImpressionDataToJSON>(JsonUtility.ToJson(ios)).value_micros-1.234567)<.000000001,"iOS revenue retains fractional currency units");
            Check(typeof(AdMobImpressionDataToJSON).GetField("value_micros").FieldType==typeof(long)&&typeof(AdMobIosImpressionDataToJSON).GetField("value_micros").FieldType==typeof(double),"Revenue DTO types must not depend on active platform");
        }
        static IOrderInfo Info(string id)
        {
            var type=typeof(PendingOrder).Assembly.GetType("UnityEngine.Purchasing.OrderInfo");
            return (IOrderInfo)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{"",id,"FakeStore",null},null);
        }
        public static void PendingQueue()
        {
            var go=new GameObject("Purchase retry test");
            try
            {
                var c=go.AddComponent<SupplyCommerce>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var product=(Product)Activator.CreateInstance(typeof(Product),flags|BindingFlags.Public,null,new object[]{new ProductDefinition("coins",ProductType.Consumable),new ProductMetadata("$1","Coins","Coins","USD",1),true},null);
                var order=new PendingOrder(new Cart(product),Info("pending-token"));
                var method=typeof(SupplyCommerce).GetMethod("OnPending",flags);
                var queue=(Dictionary<string,PendingOrder>)typeof(SupplyCommerce).GetField("waitingForSession",flags).GetValue(c);
                method.Invoke(c,new object[]{order});method.Invoke(c,new object[]{order});
                Check(queue.Count==1,"Repeated pending callbacks queued once while no session exists");
                var confirming=(HashSet<string>)typeof(SupplyCommerce).GetField("confirmRequested",flags).GetValue(c);confirming.Add("pending-token");
                typeof(SupplyCommerce).GetMethod("Confirmed",flags).Invoke(c,new object[]{new FailedOrder(order,PurchaseFailureReason.Unknown,"network")});
                Check(queue.Count==1&&!confirming.Contains("pending-token"),"Failed confirmation stays queued and releases retry guard");
                typeof(SupplyCommerce).GetMethod("Confirmed",flags).Invoke(c,new object[]{new ConfirmedOrder(order.CartOrdered,order.Info)});
                method.Invoke(c,new object[]{order});Check(queue.Count==0,"Confirmed order removed; late duplicate must not confirm again");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
#endif
