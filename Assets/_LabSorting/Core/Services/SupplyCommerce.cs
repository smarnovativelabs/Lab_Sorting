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
        public void BindSession(SortingSession value){if(ReferenceEquals(session,value))return;session=value;tutorial=null;tutorialSearched=false;}
        void Update(){TickInterstitial();if(waitingForSession.Count==0||!session||!session.Initialized||!session.persistence)return;var orders=waitingForSession.Values.ToArray();waitingForSession.Clear();foreach(var order in orders)OnPending(order);}
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
        // Device builds always check the Google Play signature. The Editor fake store has no signed receipt, so Editor
        // test mode only checks that the order really contains the product.
        bool VerifiedReceipt(Order order,string productId,string transactionId)
        {
#if UNITY_EDITOR
            if(EditorTestServices)return order.CartOrdered.Items().Any(i=>i.Product!=null&&i.Product.definition.id==productId);
#endif
            var receipts=new CrossPlatformValidator(Convert.FromBase64String(settings.googlePlayPublicKey),null,Application.identifier).Validate(order.Info.Receipt);
            return receipts.Any(r=>r.productID==productId&&(transactionId==null||r.transactionID==transactionId));
        }
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
        public bool RewardReady=>initializedAds&&consent&&consent.CanRequestAds&&!busy&&MaxSdk.IsRewardedAdReady(settings.androidRewardedId);
        public string ProductId(SupplyKind kind)=>!settings?"":kind==SupplyKind.Search?settings.searchPackProductId:kind==SupplyKind.Magnet?settings.magnetPackProductId:kind==SupplyKind.Slot?settings.slotPackProductId:settings.smallCashProductId;
        public int PackAmount(SupplyKind kind)=>!settings?0:kind==SupplyKind.Search?settings.searchPackAmount:kind==SupplyKind.Magnet?settings.magnetPackAmount:kind==SupplyKind.Slot?settings.slotPackAmount:settings.smallCashAmount;
        public int AdAmount(SupplyKind kind)=>!settings?0:kind==SupplyKind.Search?settings.rewardedSearchAmount:kind==SupplyKind.Magnet?settings.rewardedMagnetAmount:kind==SupplyKind.Slot?settings.rewardedSlotAmount:settings.rewardedCashAmount;
        public string Price(SupplyKind kind){var p=store?.GetProductById(ProductId(kind));return p!=null&&p.availableToPurchase?p.metadata.localizedPriceString:null;}
        public bool CanBuy(SupplyKind kind)=>!busy&&session&&session.Initialized&&session.persistence&&Price(kind)!=null&&!string.IsNullOrWhiteSpace(settings.googlePlayPublicKey)&&(kind!=SupplyKind.Slot||PackAmount(kind)<=session.maxCapacity-session.capacity-session.SlotCredits);
        public string LargeCashPrice(){var p=settings?store?.GetProductById(settings.largeCashProductId):null;return p!=null&&p.availableToPurchase?p.metadata.localizedPriceString:null;}
        public bool CanBuyLargeCash=>!busy&&session&&session.Initialized&&session.persistence&&LargeCashPrice()!=null&&!string.IsNullOrWhiteSpace(settings.googlePlayPublicKey);
        public bool BuyLargeCash(){if(!CanBuyLargeCash){Status="Store unavailable. Please try later.";return false;}BeginPurchase();try{store.PurchaseProduct(settings.largeCashProductId);return true;}catch(Exception){busy=false;Status="Store unavailable.";return false;}}
        async void Start()
        {
            if(!FindFirstObjectByType<InternetConnectivity>()){var go=new GameObject("Lab Connectivity");DontDestroyOnLoad(go);var connection=go.AddComponent<InternetConnectivity>();connection.StartCoroutine(connection.InitializeRoutine());}
            if(!settings)return;
            if(!EditorTestServices&&(!settings.enableLiveServices||Application.isEditor||Application.platform!=RuntimePlatform.Android))return;
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
            try{await store.Connect();}catch(Exception){Status="Store unavailable. Please try later.";}
        }
        void Connected(){store.FetchProducts(products);}
        void Disconnected(StoreConnectionFailureDescription error){busy=false;Status="Store unavailable. Please try later.";}
        void ProductsFetched(System.Collections.Generic.List<Product> list){store.FetchPurchases();}
        void ProductsFailed(ProductFetchFailed error){busy=false;Status="Store unavailable. Please try later.";}
        void PurchaseFailed(FailedOrder order){LabTelemetry.Event("purchase_failed_or_cancelled");busy=false;Status="Purchase cancelled or unsuccessful.";}
        void Deferred(DeferredOrder order){LabTelemetry.Event("purchase_deferred");busy=false;Status="Purchase awaiting approval.";}
        void Confirmed(Order order){busy=false;Status=order is ConfirmedOrder?"Purchase delivered.":"Purchase confirmation pending.";if(order is ConfirmedOrder confirmed)LabTelemetry.Purchase(confirmed);}
        void PurchasesFetched(Orders orders){foreach(var pending in orders.PendingOrders)OnPending(pending);foreach(var confirmed in orders.ConfirmedOrders)RestoreRemoveAds(confirmed);}
        void PurchasesFailed(PurchasesFetchFailureDescription error){busy=false;Status="Could not restore pending purchases.";}
        void EntitlementChecked(Entitlement entitlement){}
        void OnPending(PendingOrder order)
        {
            busy=false;
            if(!session||!session.Initialized){if(!string.IsNullOrWhiteSpace(order.Info.TransactionID))waitingForSession[order.Info.TransactionID]=order;Status="Purchase delivery pending.";return;}
            if(!session||!session.persistence||string.IsNullOrWhiteSpace(order.Info.TransactionID)||string.IsNullOrWhiteSpace(settings.googlePlayPublicKey)){Status="Purchase delivery pending.";return;}
            try
            {
                var cart=order.CartOrdered.Items().ToList();
                // The UI purchases a single configured consumable per transaction.
                if(cart.Count!=1||cart[0].Quantity!=1)return;
                string id=cart[0].Product.definition.id;
                if(!VerifiedReceipt(order,id,order.Info.TransactionID))return;
                if(id==settings.removeAdsProductId){if(CommerceEntitlements.GrantRemoveAds()){store.ConfirmPurchase(order);Status="Ads removed. Optional reward videos remain available.";LabTelemetry.Event("purchase_delivered",id);}return;}
                if(id==settings.largeCashProductId){if(session.GrantSupply(SupplyKind.Cash,settings.largeCashAmount,"iap:"+order.Info.TransactionID))store.ConfirmPurchase(order);return;}
                foreach(SupplyKind kind in Enum.GetValues(typeof(SupplyKind)))if(ProductId(kind)==id)
                {if(session.GrantSupply(kind,PackAmount(kind),"iap:"+order.Info.TransactionID)){store.ConfirmPurchase(order);Status="Purchase delivered.";}else Status="Saving purchase. Delivery will retry.";return;}
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
        void OnAdsInitialized(MaxSdkBase.SdkConfiguration config){initializedAds=true;consentRetries=0;ResetInterstitialTimer();LoadAd();LoadInterstitial();}
        void LoadAd(){if(initializedAds&&consent&&consent.CanRequestAds)MaxSdk.LoadRewardedAd(settings.androidRewardedId);}
        void OnReward(string id,MaxSdkBase.Reward reward,MaxSdkBase.AdInfo info)
        {if(id!=settings.androidRewardedId||rewardToken==null||rewardGranted)return;rewardGranted=true;SaveAdReward();}
        void SaveAdReward(){if(session&&session.Initialized&&session.GrantSupply(rewardKind,rewardAmount,rewardToken)){Status="Reward added!";LabTelemetry.Event("reward_ad_completed",rewardKind.ToString(),rewardAmount);}else{Status="Reward saving. Please wait.";if(rewardSaveRetry==null)rewardSaveRetry=StartCoroutine(RetryAdReward());}}
        // A rewarded video also restarts the automatic interstitial clock, so players never get two ads back to back.
        void OnAdHidden(string id,MaxSdkBase.AdInfo info){if(id!=settings.androidRewardedId)return;ResetInterstitialTimer();if(rewardToken==null)return;QueueRewardClose();LoadAd();}
        void EndReward(){busy=false;if(!rewardGranted)Status="Video closed. Reward requires completion.";if(rewardSaveRetry==null){rewardToken=null;rewardGranted=false;}else{busy=true;QueueRewardClose();}}
        void OnAdFailed(string id,MaxSdkBase.ErrorInfo error,MaxSdkBase.AdInfo info){if(id!=settings.androidRewardedId||rewardToken==null)return;if(rewardGranted){QueueRewardClose();return;}if(rewardCloseRoutine!=null){StopCoroutine(rewardCloseRoutine);rewardCloseRoutine=null;}busy=false;rewardToken=null;Status="Video could not play. No reward used.";Invoke(nameof(LoadAd),5);}
        void OnAdLoadFailed(string id,MaxSdkBase.ErrorInfo error){if(id==settings.androidRewardedId)Invoke(nameof(LoadAd),15);}
        void OnDestroy()
        {
            CancelInvoke();CleanupAds();if(subscribed){MaxSdkCallbacks.OnSdkInitializedEvent-=OnAdsInitialized;MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent-=OnReward;MaxSdkCallbacks.Rewarded.OnAdHiddenEvent-=OnAdHidden;MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent-=OnAdFailed;MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent-=OnAdLoadFailed;}
            // Persistent service owner releases subscriptions only when it is destroyed.
            if(store!=null){store.OnAuthAccountChanged-=AccountChanged;store.OnStoreConnected-=Connected;store.OnStoreDisconnected-=Disconnected;store.OnProductsFetched-=ProductsFetched;store.OnProductsFetchFailed-=ProductsFailed;store.OnPurchasePending-=OnPending;store.OnPurchaseFailed-=PurchaseFailed;store.OnPurchaseDeferred-=Deferred;store.OnPurchaseConfirmed-=Confirmed;store.OnPurchasesFetched-=PurchasesFetched;store.OnPurchasesFetchFailed-=PurchasesFailed;store.OnCheckEntitlement-=EntitlementChecked;}
        }
    }
}

