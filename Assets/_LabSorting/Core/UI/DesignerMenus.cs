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
        public void WatchCash(){var c=Commerce;if(storeRoot.activeSelf&&c){c.Watch(SupplyKind.Cash);RefreshStore();}}
        bool previousBlocked,previousCursorVisible;
        CursorLockMode previousCursor;
        float refreshAt;
        SupplyCommerce Commerce=>LabServiceRoot.Instance?LabServiceRoot.Instance.commerce:FindFirstObjectByType<SupplyCommerce>();
        public bool IsOpen=>pauseRoot.activeSelf||storeRoot.activeSelf;
        void Open(GameObject root)
        {
            if(IsOpen||session.player.blocked)return;
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
            if(cashAdButton){cashAdButton.interactable=c.RewardReady;cashAdLabel.text="WATCH AD  +"+c.AdAmount(SupplyKind.Cash)+" CASH";}
            string[] values={c.AdsRemoved?"ADS REMOVED":c.RemoveAdsPrice,c.Price(SupplyKind.Cash),c.LargeCashPrice(),c.Price(SupplyKind.Magnet),c.Price(SupplyKind.Search),c.Price(SupplyKind.Slot)};
            bool[] ready={!c.AdsRemoved&&!c.Busy&&c.RemoveAdsPrice!=null,c.CanBuy(SupplyKind.Cash),c.CanBuyLargeCash,c.CanBuy(SupplyKind.Magnet),c.CanBuy(SupplyKind.Search),c.CanBuy(SupplyKind.Slot)};
            for(int i=0;i<prices.Length;i++){prices[i].text=values[i]??"Unavailable";purchases[i].interactable=ready[i];}
            storeClose.interactable=!c.Busy||c.CanClosePurchaseUI;storeStatus.text=c.Status??"";
        }
        void OnDisable(){if(session&&pauseRoot&&storeRoot&&IsOpen){pauseRoot.SetActive(false);storeRoot.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=previousCursor;Cursor.visible=previousCursorVisible;}}
    }
}
