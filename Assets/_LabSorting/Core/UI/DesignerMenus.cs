using UnityEngine;
using UnityEngine.InputSystem;
using LabSorting.Sorting;
using LabSorting.Services;

namespace LabSorting.UI
{
    // Designer artwork only changes presentation; all deliveries still go through the existing commerce owner.
    public sealed class DesignerMenus : MonoBehaviour
    {
        public SortingSession session;
        public SettingsTutorialPanel settings;
        public GameObject pauseRoot,storeRoot;
        public UnityEngine.UI.Text saveStatus,storeStatus;
        public UnityEngine.UI.Text[] prices;
        public UnityEngine.UI.Button[] purchases;
        public UnityEngine.UI.Button storeClose;
        public UnityEngine.UI.Button cashAdButton;
        public UnityEngine.UI.Text cashAdLabel;
        public StoreScrollGuard storeScrollGuard;
        [Header("Product amounts (big value + small unit), filled from the service settings")]
        public UnityEngine.UI.Text[] amountValues,amountUnits;
        // The tapped product / the ad button shows progress itself; results go to the toast.
        int busyProduct=-1;bool busyAd;
        public void WatchCash(){var c=Commerce;if(storeRoot.activeSelf&&c){busyAd=true;busyProduct=-1;c.Watch(SupplyKind.Cash);RefreshStore();}}
        bool previousBlocked,previousCursorVisible;
        CursorLockMode previousCursor;
        float refreshAt;
        SupplyCommerce Commerce=>LabServiceRoot.Instance?LabServiceRoot.Instance.commerce:FindFirstObjectByType<SupplyCommerce>();
        public bool IsOpen=>pauseRoot.activeSelf||storeRoot.activeSelf;
        void Open(GameObject root)
        {
            if(IsOpen||session.player.blocked||session.player.PickTutorialLocked)return;
            previousBlocked=session.player.blocked;previousCursor=Cursor.lockState;previousCursorVisible=Cursor.visible;
            session.CancelHold();session.player.SetBlocked(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            root.SetActive(true);root.transform.SetAsLastSibling();
        }
        public void OpenPause(){Open(pauseRoot);saveStatus.text="";}
        public void OpenStore(){Open(storeRoot);RefreshStore();}
        public void Close()
        {
            if(!IsOpen||(storeRoot.activeSelf&&Commerce&&Commerce.Busy&&!Commerce.CanClosePurchaseUI))return;
            pauseRoot.SetActive(false);storeRoot.SetActive(false);session.player.SetBlocked(previousBlocked);
            Cursor.lockState=previousCursor;Cursor.visible=previousCursorVisible;
        }
        public void Settings(){Close();settings.OpenSettings();}
        public void StoreFromPause(){Close();OpenStore();}
        public void Save(){session.Save();saveStatus.text="Progress saved";}
        public void Purchase(int index)
        {
            var commerce=Commerce;if(!storeRoot.activeSelf||!commerce||(storeScrollGuard&&storeScrollGuard.Blocked))return;
            busyProduct=index;busyAd=false;
            switch(index){case 0:commerce.BuyRemoveAds();break;case 1:commerce.Buy(SupplyKind.Cash);break;case 2:commerce.BuyLargeCash();break;case 3:commerce.Buy(SupplyKind.Magnet);break;case 4:commerce.Buy(SupplyKind.Search);break;case 5:commerce.Buy(SupplyKind.Slot);break;}
            RefreshStore();
        }
        void Update()
        {
            if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Close();
            if(storeRoot.activeSelf&&Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+.25f;RefreshStore();}
        }
        void RefreshStore()
        {
            var c=Commerce;if(!c)return;
            if(!c.Busy){busyProduct=-1;busyAd=false;}
            if(cashAdButton){cashAdButton.interactable=c.RewardReady;cashAdLabel.text=busyAd&&c.Busy&&!string.IsNullOrEmpty(c.Status)?c.Status.ToUpperInvariant():"WATCH AD  +"+UIFormat.Count(c.AdAmount(SupplyKind.Cash))+" CASH";}
            string[] values={c.AdsRemoved?"ADS REMOVED":c.RemoveAdsPrice,c.Price(SupplyKind.Cash),c.LargeCashPrice(),c.Price(SupplyKind.Magnet),c.Price(SupplyKind.Search),c.Price(SupplyKind.Slot)};
            bool[] ready={!c.AdsRemoved&&!c.Busy&&c.RemoveAdsPrice!=null,c.CanBuy(SupplyKind.Cash),c.CanBuyLargeCash,c.CanBuy(SupplyKind.Magnet),c.CanBuy(SupplyKind.Search),c.CanBuy(SupplyKind.Slot)};
            for(int i=0;i<prices.Length;i++){prices[i].text=c.Busy&&i==busyProduct?"...":values[i]??"Unavailable";purchases[i].interactable=ready[i];}
            if(amountValues!=null&&amountUnits!=null&&c.settings)
            {
                var s=c.settings;int[] amounts={0,s.smallCashAmount,s.largeCashAmount,s.magnetPackAmount,s.searchPackAmount,s.slotPackAmount};
                string[] units={"No forced ads","CASH","CASH","MAGNET USES","SEARCH USES","CARRY SLOTS"};
                for(int i=0;i<amountValues.Length&&i<amountUnits.Length&&i<amounts.Length;i++){if(amountValues[i])amountValues[i].text=amounts[i]>0?"+"+UIFormat.Count(amounts[i]):"";if(amountUnits[i])amountUnits[i].text=units[i];}
            }
            storeClose.interactable=!c.Busy||c.CanClosePurchaseUI;
            // Results reach the toast; a line here only repeated them and never cleared.
            if(storeStatus)storeStatus.text="";
        }
        void OnDisable(){if(session&&pauseRoot&&storeRoot&&IsOpen){pauseRoot.SetActive(false);storeRoot.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=previousCursor;Cursor.visible=previousCursorVisible;}}
    }
}
