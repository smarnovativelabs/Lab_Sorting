using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
using LabSorting.Sorting;
namespace LabSorting.Services
{
    // Explicit opt-in only. Empty configuration never initializes a live SDK or grants a fake reward.
    public sealed partial class SupplyCommerce : MonoBehaviour
    {
        public LabServiceSettings settings;
        public SortingSession session;
        readonly System.Collections.Generic.Dictionary<string,PendingOrder> waitingForSession=new System.Collections.Generic.Dictionary<string,PendingOrder>();
        // Transactions already sent to ConfirmPurchase. IAP 5 hands a fetched pending order to OnPurchasePending and
        // PurchasesFetched passes it here again; a second ConfirmPurchase would fail with ExistingPurchasePending.
        readonly System.Collections.Generic.HashSet<string> confirmRequested=new System.Collections.Generic.HashSet<string>();
        readonly System.Collections.Generic.HashSet<string> confirmedTransactions=new System.Collections.Generic.HashSet<string>();
        public void BindSession(SortingSession value){if(ReferenceEquals(session,value))return;session=value;tutorial=null;tutorialSearched=false;}
        void Update(){TickInterstitial();TickServiceRecovery();}
        public const string SuppressEditorServicesKey="LabSorting.SuppressEditorTestServices";
        // Editor play only: MAX placeholder ads and the IAP fake store. Automated tests set the SessionState key so they
        // keep exercising the no-SDK path. Always false in device builds.
        public bool EditorTestServices
        {
            get
            {
#if UNITY_EDITOR
                return settings&&settings.editorTestServices&&!UnityEditor.SessionState.GetBool(SuppressEditorServicesKey,false);
#else
                return false;
#endif
            }
        }
        bool ReceiptValidationConfigured=>settings&&(Application.platform==RuntimePlatform.IPhonePlayer||!string.IsNullOrWhiteSpace(settings.googlePlayPublicKey)||EditorTestServices);
        // Apple: native StoreKit 2 verifies the signature before Unity delivers an order; then bind its
        // signed identity to this app/product/transaction. Android retains Google Play signature validation.
        bool VerifiedReceipt(Order order,string productId,string transactionId)
        {
            if(order?.Info==null)return false;
            var cart=order.CartOrdered.Items().ToList();
            if(cart.Count!=1||cart[0].Quantity!=1||cart[0].Product?.definition.id!=productId)return false;
            if(transactionId!=null&&order.Info.TransactionID!=transactionId)return false;
#if UNITY_EDITOR
            // Unity's fake store restores non-consumables with an empty transaction ID.
            // Device orders must always have one; this exception is only for the fake store.
            if(EditorTestServices)return order.CartOrdered.Items().Any(i=>i.Product!=null&&i.Product.definition.id==productId);
#endif
            if(string.IsNullOrWhiteSpace(order.Info.TransactionID))return false;
            if(Application.platform==RuntimePlatform.IPhonePlayer)
            {
                var apple=order.Info.Apple;
                return apple!=null&&apple.StoreName=="AppleAppStore"&&ApplePurchaseBinding.Matches(apple.jwsRepresentation,Application.identifier,productId,order.Info.TransactionID);
            }
            var receipts=new CrossPlatformValidator(Convert.FromBase64String(settings.googlePlayPublicKey),null,Application.identifier).Validate(order.Info.Receipt);
            return receipts.OfType<GooglePlayReceipt>().Any(r=>GoogleReceiptMatches(r,productId,order.Info.TransactionID));
        }
        internal static bool GoogleReceiptMatches(GooglePlayReceipt receipt,string productId,string token)=>receipt!=null&&receipt.productID==productId&&!string.IsNullOrWhiteSpace(token)&&receipt.purchaseToken==token&&receipt.purchaseState==GooglePurchaseState.Purchased;
        StoreController store;
        System.Collections.Generic.List<ProductDefinition> products;
        bool initializedAds,busy,subscribed;
        string rewardToken;
        SupplyKind rewardKind;
        int rewardAmount;
        string status="";
        public event Action<string> StatusChanged;
        public string Status {get=>status;private set{status=value;StatusChanged?.Invoke(value);}}
        public bool PurchaseBusy=>busy&&purchaseRequested;
        bool purchaseRequested;
        float purchaseBegan;
        public bool CanClosePurchaseUI=>PurchaseBusy&&Time.unscaledTime-purchaseBegan>=30;
        void BeginPurchase(){LabTelemetry.Event("purchase_flow_opened");purchaseRequested=true;purchaseBegan=Time.unscaledTime;busy=true;Status="Opening store...";}
        public bool Busy=>busy;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugCanArm=>!busy&&waitingForSession.Count==0&&confirmRequested.Count==0;
        public string DebugPurchaseState=>"Pending deliveries: "+waitingForSession.Count+" | confirming: "+confirmRequested.Count+" | confirmed this run: "+confirmedTransactions.Count;
#endif
        public bool RewardReady=>initializedAds&&consent&&consent.CanRequestAds&&!busy&&MaxSdk.IsRewardedAdReady(settings.RewardedId);
        public string ProductId(SupplyKind kind)=>!settings?"":kind==SupplyKind.Search?settings.searchPackProductId:kind==SupplyKind.Magnet?settings.magnetPackProductId:kind==SupplyKind.Slot?settings.slotPackProductId:settings.smallCashProductId;
        public int PackAmount(SupplyKind kind)=>!settings?0:kind==SupplyKind.Search?settings.searchPackAmount:kind==SupplyKind.Magnet?settings.magnetPackAmount:kind==SupplyKind.Slot?settings.slotPackAmount:settings.smallCashAmount;
        public int AdAmount(SupplyKind kind)=>!settings?0:kind==SupplyKind.Search?settings.rewardedSearchAmount:kind==SupplyKind.Magnet?settings.rewardedMagnetAmount:kind==SupplyKind.Slot?settings.rewardedSlotAmount:settings.rewardedCashAmount;
        public string Price(SupplyKind kind){var p=store?.GetProductById(ProductId(kind));return p!=null&&p.availableToPurchase?p.metadata.localizedPriceString:null;}
        public bool CanBuy(SupplyKind kind)=>!busy&&session&&session.Initialized&&session.persistence&&Price(kind)!=null&&ReceiptValidationConfigured&&(kind!=SupplyKind.Slot||PackAmount(kind)<=session.maxCapacity-session.capacity-session.SlotCredits);
        public string LargeCashPrice(){var p=settings?store?.GetProductById(settings.largeCashProductId):null;return p!=null&&p.availableToPurchase?p.metadata.localizedPriceString:null;}
        public bool CanBuyLargeCash=>!busy&&session&&session.Initialized&&session.persistence&&LargeCashPrice()!=null&&ReceiptValidationConfigured;
        public bool BuyLargeCash(){if(!CanBuyLargeCash){Status="Store unavailable. Please try later.";return false;}BeginPurchase();try{store.PurchaseProduct(settings.largeCashProductId);return true;}catch(Exception){busy=false;Status="Store unavailable.";return false;}}
        void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!GetComponent<CommerceDebugControls>())gameObject.AddComponent<CommerceDebugControls>();
#endif
            if(!FindFirstObjectByType<InternetConnectivity>()){var go=new GameObject("Lab Connectivity");DontDestroyOnLoad(go);var connection=go.AddComponent<InternetConnectivity>();connection.StartCoroutine(connection.InitializeRoutine());}
            if(!settings)return;
            if(!EditorTestServices&&(!settings.enableLiveServices||Application.isEditor||(Application.platform!=RuntimePlatform.Android&&Application.platform!=RuntimePlatform.IPhonePlayer)))return;
            consent=GetComponent<LabConsent>();if(!consent)consent=gameObject.AddComponent<LabConsent>();consent.Changed+=InitializeAds;consent.Gather(settings.underAgeOfConsent);
            products=Enum.GetValues(typeof(SupplyKind)).Cast<SupplyKind>().Select(ProductId).Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct().Select(id=>new ProductDefinition(id,ProductType.Consumable)).ToList();
            if(!string.IsNullOrWhiteSpace(settings.largeCashProductId)&&products.All(p=>p.id!=settings.largeCashProductId))products.Add(new ProductDefinition(settings.largeCashProductId,ProductType.Consumable));
            if(!string.IsNullOrWhiteSpace(settings.removeAdsProductId))products.Add(new ProductDefinition(settings.removeAdsProductId,ProductType.NonConsumable));
            if(products.Count==0)return;
            store=UnityIAPServices.StoreController();
            store.OnStoreConnected+=Connected;
            store.OnStoreDisconnected+=Disconnected;
            store.OnProductsFetched+=ProductsFetched;
            store.OnProductsFetchFailed+=ProductsFailed;
            store.OnPurchasePending+=OnPending;
            store.OnPurchaseFailed+=PurchaseFailed;
            store.OnPurchaseDeferred+=Deferred;
            store.OnPurchaseConfirmed+=Confirmed;
            store.OnPurchasesFetched+=PurchasesFetched;
            store.OnPurchasesFetchFailed+=PurchasesFailed;
            store.OnCheckEntitlement+=EntitlementChecked;
            store.OnAuthAccountChanged+=AccountChanged;
            Reconnect();
        }
        void Connected(){storeConnected=true;FetchStoreProducts();}
        void Disconnected(StoreConnectionFailureDescription error){storeConnected=false;busy=false;restoring=false;ScheduleStoreRetry(StoreRecovery.Connect);Status="Store unavailable. Please try later.";}
        void ProductsFetched(System.Collections.Generic.List<Product> list){if(list==null||list.Count<products.Count)ScheduleStoreRetry(StoreRecovery.Products);else storeRecovery=StoreRecovery.None;if(restoring)RestoreConnectedStore();else FetchStorePurchases();}
        void ProductsFailed(ProductFetchFailed error){busy=false;restoring=false;ScheduleStoreRetry(StoreRecovery.Products);Status="Store unavailable. Please try later.";}
        void PurchaseFailed(FailedOrder order){LabTelemetry.Event("purchase_failed_or_cancelled");busy=false;ScheduleStoreRetry(StoreRecovery.Purchases);Status="Purchase cancelled or unsuccessful.";}
        void Deferred(DeferredOrder order){LabTelemetry.Event("purchase_deferred");busy=false;Status="Purchase awaiting approval.";}
        // A failed confirmation leaves the order pending in the store, so the next fetch may confirm it again.
        void Confirmed(Order order)
        {
            if(order?.Info==null)return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var debug=GetComponent<CommerceDebugControls>();
            if(debug&&debug.HoldSuccessfulConfirmation(order,()=>Confirmed(order)))return;
#endif
            busy=false;
            confirmRequested.Remove(order.Info.TransactionID);
            confirmationDeadlines.Remove(order.Info.TransactionID);
            if(order is ConfirmedOrder)
            {
                waitingForSession.Remove(order.Info.TransactionID);
                if(confirmedTransactions.Add(order.Info.TransactionID))
                {
                    var product=order.CartOrdered.Items().FirstOrDefault()?.Product;
                    LabTelemetry.Event("purchase_confirmed",product?.definition.id??"",product==null?0:(double)product.metadata.localizedPrice);
                }
            }
            else ScheduleStoreRetry(StoreRecovery.Purchases);
            Status=order is ConfirmedOrder?"Purchase delivered.":"Purchase confirmation pending.";
        }
        void PurchasesFetched(Orders orders)
        {
            if(storeRecovery==StoreRecovery.Purchases){storeRecovery=StoreRecovery.None;storeRetryAttempt=0;}
            foreach(var pending in orders.PendingOrders)OnPending(pending);
            bool restored=false,unverified=false;
            foreach(var confirmed in orders.ConfirmedOrders){var result=RestoreRemoveAds(confirmed);restored|=result==true;unverified|=result==null;}
            if(unverified){Status="Restore verification pending.";ScheduleStoreRetry(StoreRecovery.Purchases);}
            if(!restoring)return;
            // Only a restore the player asked for reports a result. Pending orders report their own delivery.
            restoring=false;
            if(unverified)return;
            if(restored)Status="Purchases restored. Ads removed.";
            else if(!orders.PendingOrders.Any())Status="No purchases to restore.";
        }
        void PurchasesFailed(PurchasesFetchFailureDescription error){busy=false;Status=restoring?"Restore failed. Please try later.":"Could not restore pending purchases.";restoring=false;ScheduleStoreRetry(StoreRecovery.Purchases);}
        void EntitlementChecked(Entitlement entitlement){}
        void Confirm(PendingOrder order)
        {
            // The verified reward is already durable. Journal its revenue before the store
            // consumes it: a confirmed consumable cannot be recovered on next launch.
            if(!LabTelemetry.RecordDeliveredPurchase(order))throw new System.IO.IOException("Purchase telemetry save pending");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var debug=GetComponent<CommerceDebugControls>();
            if(debug&&debug.AfterJournal(order))return;
#endif
            if(!confirmRequested.Add(order.Info.TransactionID))return;
            confirmationDeadlines[order.Info.TransactionID]=Time.realtimeSinceStartupAsDouble+ConfirmationTimeoutSeconds;
            try
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(debug&&debug.InjectConfirmationStall(store,order,Confirmed))return;
#endif
                store.ConfirmPurchase(order);
            }
            catch{confirmRequested.Remove(order.Info.TransactionID);confirmationDeadlines.Remove(order.Info.TransactionID);throw;}
        }
        void OnPending(PendingOrder order)=>DeliverPending(order,true);
        void DeliverPending(PendingOrder order,bool releasePurchaseUI)
        {
            if(order?.Info==null||string.IsNullOrWhiteSpace(order.Info.TransactionID))return;
            if(confirmedTransactions.Contains(order.Info.TransactionID))return;
            if(!string.IsNullOrWhiteSpace(order.Info.TransactionID)&&confirmRequested.Contains(order.Info.TransactionID))return;
            // Keep every undelivered order until confirmation, including save and validation failures.
            waitingForSession[order.Info.TransactionID]=order;
            if(releasePurchaseUI&&purchaseRequested)busy=false;
            if(!session||!session.Initialized){if(!string.IsNullOrWhiteSpace(order.Info.TransactionID))waitingForSession[order.Info.TransactionID]=order;Status="Purchase delivery pending.";return;}
            if(!session||!session.persistence||string.IsNullOrWhiteSpace(order.Info.TransactionID)||!ReceiptValidationConfigured){Status="Purchase delivery pending.";return;}
            try
            {
                var cart=order.CartOrdered.Items().ToList();
                // The UI purchases a single configured consumable per transaction.
                if(cart.Count!=1||cart[0].Quantity!=1)return;
                string id=cart[0].Product.definition.id;
                if(!VerifiedReceipt(order,id,order.Info.TransactionID)){Status="Purchase verification pending. Please retry later.";return;}
                if(id==settings.removeAdsProductId){if(CommerceEntitlements.GrantRemoveAds()){Confirm(order);Status="Ads removed. Optional reward videos remain available.";LabTelemetry.Event("purchase_delivered",id);}return;}
                if(id==settings.largeCashProductId){if(session.GrantSupply(SupplyKind.Cash,settings.largeCashAmount,"iap:"+order.Info.TransactionID))Confirm(order);return;}
                foreach(SupplyKind kind in Enum.GetValues(typeof(SupplyKind)))if(ProductId(kind)==id)
                {if(session.GrantSupply(kind,PackAmount(kind),"iap:"+order.Info.TransactionID)){Confirm(order);Status="Purchase delivered.";}else Status="Saving purchase. Delivery will retry.";return;}
            }
            catch(Exception){LabTelemetry.Event("purchase_verification_pending");Status="Purchase verification pending. Please retry later.";}
        }
        public bool Buy(SupplyKind kind)
        {
            if(!CanBuy(kind)){Status="Store unavailable. Please try later.";return false;}
            BeginPurchase();LabTelemetry.Event("purchase_started",ProductId(kind));try{store.PurchaseProduct(ProductId(kind));return true;}catch(Exception){busy=false;Status="Store unavailable.";return false;}
        }
        public bool Watch(SupplyKind kind)
        {
            if(!session||!session.Initialized||!session.persistence||!RewardReady||kind==SupplyKind.Slot&&AdAmount(kind)>session.maxCapacity-session.capacity-session.SlotCredits){Status="No video available or capacity is full.";return false;}
            purchaseRequested=false;rewardGranted=false;rewardKind=kind;rewardAmount=AdAmount(kind);rewardToken="ad:"+Guid.NewGuid().ToString("N");Status="Preparing video...";
            return BeginAdCountdown(true);
        }
        void OnAdsInitialized(MaxSdkBase.SdkConfiguration config){if(initializedAds)return;initializedAds=true;maxStarting=false;maxRetryAttempt=0;consentRetries=0;if(Application.platform==RuntimePlatform.IPhonePlayer)GetComponent<LabTelemetry>()?.CompleteIosConsent();ResetInterstitialTimer();LoadAd();LoadInterstitial();}
        void LoadAd(){if(initializedAds&&consent&&consent.CanRequestAds)MaxSdk.LoadRewardedAd(settings.RewardedId);}
        void OnReward(string id,MaxSdkBase.Reward reward,MaxSdkBase.AdInfo info)
        {if(id!=settings.RewardedId||rewardToken==null||rewardGranted)return;rewardGranted=true;SaveAdReward();}
        void SaveAdReward(){if(session&&session.Initialized&&session.GrantSupply(rewardKind,rewardAmount,rewardToken)){Status="Reward added!";LabTelemetry.Event("reward_ad_completed",rewardKind.ToString(),rewardAmount);}else{Status="Reward saving. Please wait.";if(rewardSaveRetry==null)rewardSaveRetry=StartCoroutine(RetryAdReward());}}
        // A rewarded video also restarts the automatic interstitial clock, so players never get two ads back to back.
        void OnAdHidden(string id,MaxSdkBase.AdInfo info){if(id!=settings.RewardedId)return;ResetInterstitialTimer();if(rewardToken==null)return;QueueRewardClose();LoadAd();}
        void EndReward(){busy=false;if(!rewardGranted)Status="Video closed. Reward requires completion.";if(rewardSaveRetry==null){rewardToken=null;rewardGranted=false;}else{busy=true;QueueRewardClose();}}
        void OnAdFailed(string id,MaxSdkBase.ErrorInfo error,MaxSdkBase.AdInfo info){if(id!=settings.RewardedId||rewardToken==null)return;if(rewardGranted){QueueRewardClose();return;}if(rewardCloseRoutine!=null){StopCoroutine(rewardCloseRoutine);rewardCloseRoutine=null;}busy=false;rewardToken=null;Status="Video could not play. No reward used.";Invoke(nameof(LoadAd),5);}
        void OnAdLoadFailed(string id,MaxSdkBase.ErrorInfo error){if(id==settings.RewardedId)rewardLoadAt=Time.realtimeSinceStartupAsDouble+15;}
        void OnDestroy()
        {
            CancelInvoke();CleanupAds();if(subscribed){MaxSdkCallbacks.OnSdkInitializedEvent-=OnAdsInitialized;MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent-=OnReward;MaxSdkCallbacks.Rewarded.OnAdHiddenEvent-=OnAdHidden;MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent-=OnAdFailed;MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent-=OnAdLoadFailed;}
            // Persistent service owner releases subscriptions only when it is destroyed.
            if(store!=null){store.OnAuthAccountChanged-=AccountChanged;store.OnStoreConnected-=Connected;store.OnStoreDisconnected-=Disconnected;store.OnProductsFetched-=ProductsFetched;store.OnProductsFetchFailed-=ProductsFailed;store.OnPurchasePending-=OnPending;store.OnPurchaseFailed-=PurchaseFailed;store.OnPurchaseDeferred-=Deferred;store.OnPurchaseConfirmed-=Confirmed;store.OnPurchasesFetched-=PurchasesFetched;store.OnPurchasesFetchFailed-=PurchasesFailed;store.OnCheckEntitlement-=EntitlementChecked;}
        }
    }
}

