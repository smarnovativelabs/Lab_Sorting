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
            var go=new GameObject("ATT test");try{var att=go.AddComponent<LabTrackingAuthorization>();int completions=0;att.Completed+=()=>completions++;att.Begin();att.SendMessage("Update");att.SendMessage("Update");att.Begin();Check(completions==1,"ATT completion must only initialize downstream once");}finally{UnityEngine.Object.DestroyImmediate(go);}
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
