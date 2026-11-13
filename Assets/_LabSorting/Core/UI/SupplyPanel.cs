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
        public bool designerQuickUse;
        public bool twoOfferLayout;
        public UnityEngine.UI.Text cashOfferDetails,adOfferDetails;
        public UnityEngine.UI.Image cashRewardIcon,adRewardIcon;
        public Sprite searchSprite,magnetSprite,slotSprite;
        public RectTransform offerArtwork;
        SupplyKind kind;bool previousBlocked;CursorLockMode cursor;bool cursorVisible;
        public bool IsOpen=>root&&root.activeSelf;
        public void OpenSearch(){if(session.player.blocked)return;if(designerQuickUse&&session.SearchCredits>0)session.TrySearch();else Open(SupplyKind.Search);}
        public void OpenMagnet(){if(session.player.blocked)return;if(designerQuickUse&&session.MagnetCredits>0)session.TryMagnet();else Open(SupplyKind.Magnet);}
        public void SearchOffers()=>Open(SupplyKind.Search);
        public void MagnetOffers()=>Open(SupplyKind.Magnet);
        public void OpenSlots()=>Open(SupplyKind.Slot);
        public void OpenCash(){var menus=GetComponent<DesignerMenus>();if(twoOfferLayout&&menus)menus.OpenStore();else Open(SupplyKind.Cash);}
        public void Open(SupplyKind value)
        {
            if(IsOpen||session.player.blocked)return;
            if(offerArtwork)
            {
                var card=(RectTransform)title.transform.parent;bool left=value==SupplyKind.Slot;
                card.anchorMin=card.anchorMax=new Vector2(left?0:1,.5f);card.anchoredPosition=new Vector2(left?600:-410,-35);offerArtwork.localScale=new Vector3(left?-1:1,1,1);
                title.rectTransform.anchoredPosition=new Vector2(left?10:-10,title.rectTransform.anchoredPosition.y);
                ((RectTransform)cashOfferDetails.transform.parent).anchoredPosition=new Vector2(left?-106:-136,-35);
                ((RectTransform)adOfferDetails.transform.parent).anchoredPosition=new Vector2(left?136:106,-35);
            }
            kind=value;previousBlocked=session.player.blocked;cursor=Cursor.lockState;cursorVisible=Cursor.visible;session.CancelHold();session.player.SetBlocked(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;root.SetActive(true);root.transform.SetAsLastSibling();status.text="";Refresh();
        }
        public void Close(){if(!IsOpen||(commerce.Busy&&!commerce.CanClosePurchaseUI))return;root.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=cursor;Cursor.visible=cursorVisible;}
        public void BuyCash(){if(!IsOpen||commerce.Busy)return;if(kind==SupplyKind.Cash){commerce.BuyLargeCash();status.text=commerce.Status;}else if(twoOfferLayout?session.BuyOffer(kind):session.BuySupply(kind))status.text="Added to your supplies";else status.text="Not enough cash or capacity is full";Refresh();}
        public void Watch(){if(IsOpen){commerce.Watch(kind);status.text=commerce.Status;}}
        public void BuyStore(){if(IsOpen){commerce.Buy(kind);status.text=commerce.Status;}}
        public void Use(){if(!IsOpen||commerce.Busy)return;Close();if(kind==SupplyKind.Search)session.TrySearch();else if(kind==SupplyKind.Magnet)session.TryMagnet();else if(kind==SupplyKind.Slot)session.TryUpgradeCapacity();}
        float nextRefresh;
        void Update(){if(IsOpen&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.15f;Refresh();}}
        void Refresh()
        {
            if(twoOfferLayout&&kind!=SupplyKind.Cash){RefreshOffers();return;}
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
            useButton.interactable=!commerce.Busy&&!cap&&usable&&(session.Owned(kind)>0||session.Cash>=session.SupplyPrice(kind));closeButton.interactable=!commerce.Busy||commerce.CanClosePurchaseUI;
            if(commerce.Busy||!string.IsNullOrEmpty(commerce.Status))status.text=commerce.Status;
            else if(!commerce.RewardReady)status.text="Video and store offers are currently unavailable.";
        }
        void RefreshOffers()
        {
            string noun=kind==SupplyKind.Slot?"permanent slots":kind==SupplyKind.Search?"search uses":"magnet uses";
            title.text=kind==SupplyKind.Slot?"CARRY CAPACITY":kind==SupplyKind.Search?"SEARCH COUNT":"MAGNET COUNT";
            description.text=kind==SupplyKind.Slot?"Increase your permanent carry capacity.":"Choose how to get more "+noun+".";
            owned.text=kind==SupplyKind.Slot?"Capacity "+session.capacity+" / "+session.maxCapacity:"Uses owned: "+session.Owned(kind)+"   |   Cash "+session.Cash;
            cashOfferDetails.text="Cash Offer\n$"+session.OfferPrice(kind)+"  →  +"+session.OfferAmount(kind)+"\n"+noun;
            adOfferDetails.text="Ad Offer\n1 video  →  +"+commerce.AdAmount(kind)+"\n"+noun;
            var sprite=kind==SupplyKind.Search?searchSprite:kind==SupplyKind.Magnet?magnetSprite:slotSprite;cashRewardIcon.sprite=adRewardIcon.sprite=sprite;
            cashLabel.text="USE CASH";adLabel.text="WATCH AD";
            cashButton.gameObject.SetActive(true);cashButton.interactable=!commerce.Busy&&session.CanBuyOffer(kind);
            adButton.interactable=!commerce.Busy&&commerce.RewardReady&&(kind!=SupplyKind.Slot||session.capacity+session.SlotCredits+commerce.AdAmount(kind)<=session.maxCapacity);
            storeButton.gameObject.SetActive(false);useButton.gameObject.SetActive(false);closeButton.interactable=!commerce.Busy||commerce.CanClosePurchaseUI;
            if(commerce.Busy||!string.IsNullOrEmpty(commerce.Status))status.text=commerce.Status;
        }
        void OnDisable(){if(IsOpen){root.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=cursor;Cursor.visible=cursorVisible;}}
    }
}
