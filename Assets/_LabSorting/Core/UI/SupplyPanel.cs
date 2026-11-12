using UnityEngine;
using LabSorting.Sorting;
using LabSorting.Services;
namespace LabSorting.UI
{
    public sealed class SupplyPanel : MonoBehaviour
    {
        public SortingSession session;
        public SupplyCommerce commerce;
        public GameObject root;
        public UnityEngine.UI.Text title,description,owned,cashLabel,adLabel,storeLabel,useLabel,status;
        public UnityEngine.UI.Button cashButton,adButton,storeButton,useButton,closeButton;
        SupplyKind kind;bool previousBlocked;CursorLockMode cursor;bool cursorVisible;
        public bool IsOpen=>root&&root.activeSelf;
        public void OpenSearch()=>Open(SupplyKind.Search);
        public void OpenMagnet()=>Open(SupplyKind.Magnet);
        public void OpenSlots()=>Open(SupplyKind.Slot);
        public void OpenCash()=>Open(SupplyKind.Cash);
        public void Open(SupplyKind value)
        {
            if(IsOpen||session.player.blocked)return;
            kind=value;previousBlocked=session.player.blocked;cursor=Cursor.lockState;cursorVisible=Cursor.visible;session.CancelHold();session.player.SetBlocked(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;root.SetActive(true);root.transform.SetAsLastSibling();status.text="";Refresh();
        }
        public void Close(){if(!IsOpen||commerce.Busy)return;root.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=cursor;Cursor.visible=cursorVisible;}
        public void BuyCash(){if(!IsOpen)return;if(kind==SupplyKind.Cash){commerce.BuyLargeCash();status.text=commerce.Status;}else if(session.BuySupply(kind))status.text="Added to your supplies";else status.text="Not enough cash or capacity is full";Refresh();}
        public void Watch(){if(IsOpen){commerce.Watch(kind);status.text=commerce.Status;}}
        public void BuyStore(){if(IsOpen){commerce.Buy(kind);status.text=commerce.Status;}}
        public void Use(){if(!IsOpen||commerce.Busy)return;Close();if(kind==SupplyKind.Search)session.TrySearch();else if(kind==SupplyKind.Magnet)session.TryMagnet();else if(kind==SupplyKind.Slot)session.TryUpgradeCapacity();}
        float nextRefresh;
        void Update(){if(IsOpen&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.15f;Refresh();}}
        void Refresh()
        {
            title.text=kind==SupplyKind.Slot?"CARRY CAPACITY":kind.ToString().ToUpperInvariant();
            description.text=kind==SupplyKind.Search?"Highlight the selected item type anywhere in the lab.":kind==SupplyKind.Magnet?"Bring matching loose items into your hands.":kind==SupplyKind.Slot?"Add one permanent carry slot.":"Use cash for search, magnet and extra carry slots.";
            owned.text=kind==SupplyKind.Slot?"Capacity "+session.capacity+" / "+session.maxCapacity+"   |   Slot tokens "+session.SlotCredits:kind==SupplyKind.Cash?"Cash "+session.Cash:"Uses owned: "+session.Owned(kind)+"   |   Cash "+session.Cash;
            bool cap=kind==SupplyKind.Slot&&session.capacity>=session.maxCapacity;
            cashButton.gameObject.SetActive(true);cashLabel.text="BUY 1\n$"+session.SupplyPrice(kind);cashButton.interactable=!commerce.Busy&&!cap&&session.Cash>=session.SupplyPrice(kind);
            if(kind==SupplyKind.Cash){cashLabel.text="BIG PACK +"+(commerce.settings?commerce.settings.largeCashAmount:0)+"\n"+(commerce.LargeCashPrice()??"Unavailable");cashButton.interactable=commerce.CanBuyLargeCash;}
            adLabel.text="WATCH AD\n+"+commerce.AdAmount(kind)+" "+kind;adButton.interactable=!commerce.Busy&&!cap&&commerce.RewardReady&&(kind!=SupplyKind.Slot||session.capacity+session.SlotCredits+commerce.AdAmount(kind)<=session.maxCapacity);
            storeLabel.text="PACK +"+commerce.PackAmount(kind)+"\n"+(commerce.Price(kind)??"Unavailable");storeButton.interactable=!cap&&commerce.CanBuy(kind);
            useButton.gameObject.SetActive(kind!=SupplyKind.Cash);useLabel.text=cap?"MAX CAPACITY":session.Owned(kind)>0?"USE 1":"USE FOR $"+session.SupplyPrice(kind);
            bool usable=kind==SupplyKind.Slot||session.Active&&!session.Active.Moving;
            if(kind==SupplyKind.Magnet)usable&=session.carried.Count<session.capacity&&session.MagnetSeconds<=0;
            if(kind==SupplyKind.Search)usable&=!session.SearchType;
            useButton.interactable=!commerce.Busy&&!cap&&usable&&(session.Owned(kind)>0||session.Cash>=session.SupplyPrice(kind));closeButton.interactable=!commerce.Busy;
            if(commerce.Busy||!string.IsNullOrEmpty(commerce.Status))status.text=commerce.Status;
            else if(!commerce.RewardReady)status.text="Video and store offers are currently unavailable.";
        }
        void OnDisable(){if(IsOpen){root.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=cursor;Cursor.visible=cursorVisible;}}
    }
}
