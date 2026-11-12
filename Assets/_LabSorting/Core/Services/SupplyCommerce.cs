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
        public void BindSession(SortingSession value){if(ReferenceEquals(session,value))return;if(!ReferenceEquals(session,null))session.RackCompleted-=CompletedRack;session=value;interstitialPending=false;if(session)session.RackCompleted+=CompletedRack;}
        void Update(){if(waitingForSession.Count==0||!session||!session.Initialized||!session.persistence)return;var orders=waitingForSession.Values.ToArray();waitingForSession.Clear();foreach(var order in orders)OnPending(order);}
        StoreController store;
        System.Collections.Generic.List<ProductDefinition> products;
        bool initializedAds,busy,subscribed;
        string rewardToken;
        SupplyKind rewardKind;
        int rewardAmount;
        public string Status {get;private set;}="";
        public bool Busy=>busy;
        public bool RewardReady=>initializedAds&&consent&&consent.CanRequestAds&&!busy&&MaxSdk.IsRewardedAdReady(settings.androidRewardedId);
        public string ProductId(SupplyKind kind)=>!settings?"":kind==SupplyKind.Search?settings.searchPackProductId:kind==SupplyKind.Magnet?settings.magnetPackProductId:kind==SupplyKind.Slot?settings.slotPackProductId:settings.smallCashProductId;
        public int PackAmount(SupplyKind kind)=>!settings?0:kind==SupplyKind.Search?settings.searchPackAmount:kind==SupplyKind.Magnet?settings.magnetPackAmount:kind==SupplyKind.Slot?settings.slotPackAmount:settings.smallCashAmount;
        public int AdAmount(SupplyKind kind)=>!settings?0:kind==SupplyKind.Search?settings.rewardedSearchAmount:kind==SupplyKind.Magnet?settings.rewardedMagnetAmount:kind==SupplyKind.Slot?settings.rewardedSlotAmount:settings.rewardedCashAmount;
        public string Price(SupplyKind kind){var p=store?.GetProductById(ProductId(kind));return p!=null&&p.availableToPurchase?p.metadata.localizedPriceString:null;}
        public bool CanBuy(SupplyKind kind)=>!busy&&session&&session.Initialized&&session.persistence&&Price(kind)!=null&&!string.IsNullOrWhiteSpace(settings.googlePlayPublicKey)&&(kind!=SupplyKind.Slot||PackAmount(kind)<=session.maxCapacity-session.capacity-session.SlotCredits);
        public string LargeCashPrice(){var p=settings?store?.GetProductById(settings.largeCashProductId):null;return p!=null&&p.availableToPurchase?p.metadata.localizedPriceString:null;}
        public bool CanBuyLargeCash=>!busy&&session&&session.Initialized&&session.persistence&&LargeCashPrice()!=null&&!string.IsNullOrWhiteSpace(settings.googlePlayPublicKey);
        public bool BuyLargeCash(){if(!CanBuyLargeCash){Status="Store unavailable. Please try later.";return false;}busy=true;try{store.PurchaseProduct(settings.largeCashProductId);return true;}catch(Exception){busy=false;Status="Store unavailable.";return false;}}
        async void Start()
        {
            if(!FindFirstObjectByType<InternetConnectivity>()){var go=new GameObject("Lab Connectivity");DontDestroyOnLoad(go);var connection=go.AddComponent<InternetConnectivity>();connection.StartCoroutine(connection.InitializeRoutine());}
            if(!settings||!settings.enableLiveServices||Application.isEditor||Application.platform!=RuntimePlatform.Android)return;
            consent=GetComponent<LabConsent>()??gameObject.AddComponent<LabConsent>();consent.Changed+=InitializeAds;consent.Gather(settings.underAgeOfConsent);
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
        void PurchaseFailed(FailedOrder order){busy=false;Status="Purchase cancelled or unsuccessful.";}
        void Deferred(DeferredOrder order){busy=false;Status="Purchase awaiting approval.";}
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
                var receipts=new CrossPlatformValidator(Convert.FromBase64String(settings.googlePlayPublicKey),null,Application.identifier).Validate(order.Info.Receipt);
                var cart=order.CartOrdered.Items().ToList();
                // The UI purchases a single configured consumable per transaction.
                if(cart.Count!=1||cart[0].Quantity!=1)return;
                string id=cart[0].Product.definition.id;
                if(!receipts.Any(r=>r.productID==id&&r.transactionID==order.Info.TransactionID))return;
                if(id==settings.removeAdsProductId){if(CommerceEntitlements.GrantRemoveAds()){store.ConfirmPurchase(order);Status="Ads removed. Optional reward videos remain available.";LabTelemetry.Event("purchase_delivered",id);}return;}
                if(id==settings.largeCashProductId){if(session.GrantSupply(SupplyKind.Cash,settings.largeCashAmount,"iap:"+order.Info.TransactionID))store.ConfirmPurchase(order);return;}
                foreach(SupplyKind kind in Enum.GetValues(typeof(SupplyKind)))if(ProductId(kind)==id)
                {if(session.GrantSupply(kind,PackAmount(kind),"iap:"+order.Info.TransactionID)){store.ConfirmPurchase(order);Status="Purchase delivered.";}else Status="Saving purchase. Delivery will retry.";return;}
            }
            catch(Exception){Status="Purchase verification pending. Please retry later.";}
        }
        public bool Buy(SupplyKind kind)
        {
            if(!CanBuy(kind)){Status="Store unavailable. Please try later.";return false;}
            busy=true;Status="Opening store...";LabTelemetry.Event("purchase_started",ProductId(kind));try{store.PurchaseProduct(ProductId(kind));return true;}catch(Exception){busy=false;Status="Store unavailable.";return false;}
        }
        public bool Watch(SupplyKind kind)
        {
            if(!session||!session.Initialized||!session.persistence||!RewardReady||kind==SupplyKind.Slot&&AdAmount(kind)>session.maxCapacity-session.capacity-session.SlotCredits){Status="No video available or capacity is full.";return false;}
            rewardGranted=false;rewardKind=kind;rewardAmount=AdAmount(kind);rewardToken="ad:"+Guid.NewGuid().ToString("N");busy=true;Status="Watching reward video...";
            LabTelemetry.Event("reward_ad_started",kind.ToString());try{MaxSdk.ShowRewardedAd(settings.androidRewardedId);return true;}catch(Exception){busy=false;rewardToken=null;Status="Video unavailable.";return false;}
        }
        void OnAdsInitialized(MaxSdkBase.SdkConfiguration config){initializedAds=true;LoadAd();LoadInterstitial();}
        void LoadAd(){if(initializedAds)MaxSdk.LoadRewardedAd(settings.androidRewardedId);}
        void OnReward(string id,MaxSdkBase.Reward reward,MaxSdkBase.AdInfo info)
        {if(id!=settings.androidRewardedId||rewardToken==null||rewardGranted)return;rewardGranted=true;SaveAdReward();}
        void SaveAdReward(){if(session&&session.Initialized&&session.GrantSupply(rewardKind,rewardAmount,rewardToken)){Status="Reward added!";LabTelemetry.Event("reward_ad_completed",rewardKind.ToString(),rewardAmount);}else{Status="Reward saving. Please wait.";Invoke(nameof(SaveAdReward),2);}}
        void OnAdHidden(string id,MaxSdkBase.AdInfo info){if(id!=settings.androidRewardedId)return;Invoke(nameof(EndReward),2);LoadAd();}
        void EndReward(){busy=false;if(!rewardGranted)Status="Video closed. Reward requires completion.";if(!IsInvoking(nameof(SaveAdReward))){rewardToken=null;rewardGranted=false;}else{busy=true;Invoke(nameof(EndReward),2);}}
        void OnAdFailed(string id,MaxSdkBase.ErrorInfo error,MaxSdkBase.AdInfo info){if(id!=settings.androidRewardedId)return;busy=false;rewardToken=null;Status="Video could not play. No reward used.";Invoke(nameof(LoadAd),5);}
        void OnAdLoadFailed(string id,MaxSdkBase.ErrorInfo error){if(id==settings.androidRewardedId)Invoke(nameof(LoadAd),15);}
        void OnDestroy()
        {
            CancelInvoke();CleanupAds();if(subscribed){MaxSdkCallbacks.OnSdkInitializedEvent-=OnAdsInitialized;MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent-=OnReward;MaxSdkCallbacks.Rewarded.OnAdHiddenEvent-=OnAdHidden;MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent-=OnAdFailed;MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent-=OnAdLoadFailed;}
            // Persistent service owner releases subscriptions only when it is destroyed.
            if(store!=null){store.OnAuthAccountChanged-=AccountChanged;store.OnStoreConnected-=Connected;store.OnStoreDisconnected-=Disconnected;store.OnProductsFetched-=ProductsFetched;store.OnProductsFetchFailed-=ProductsFailed;store.OnPurchasePending-=OnPending;store.OnPurchaseFailed-=PurchaseFailed;store.OnPurchaseDeferred-=Deferred;store.OnPurchaseConfirmed-=Confirmed;store.OnPurchasesFetched-=PurchasesFetched;store.OnPurchasesFetchFailed-=PurchasesFailed;store.OnCheckEntitlement-=EntitlementChecked;}
        }
    }
}

