using System;
using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public enum SupplyKind { Search, Magnet, Slot, Cash }
    public sealed partial class SortingSession
    {
        public int SearchCredits {get;private set;}
        public int MagnetCredits {get;private set;}
        public int SlotCredits {get;private set;}
        HashSet<string> supplyReceipts=new HashSet<string>();
        public bool bundleEconomy;
        public event Action<SupplyKind,int> SupplyRewardPresented;
        public int OfferAmount(SupplyKind kind)=>kind==SupplyKind.Search?5:kind==SupplyKind.Magnet?3:kind==SupplyKind.Slot?4:0;
        public int OfferPrice(SupplyKind kind)=>kind==SupplyKind.Search?30:kind==SupplyKind.Magnet?90:kind==SupplyKind.Slot?150:0;
        public bool CanBuyOffer(SupplyKind kind)=>OfferAmount(kind)>0&&Cash>=OfferPrice(kind)&&(kind!=SupplyKind.Slot||capacity+SlotCredits+OfferAmount(kind)<=maxCapacity);
        public bool BuyOffer(SupplyKind kind)
        {
            if(!CanBuyOffer(kind))return false;
            LabSorting.Services.LabTelemetry.Event("supply_cash_purchase",kind.ToString(),OfferAmount(kind));cash-=OfferPrice(kind);AddSupply(kind,OfferAmount(kind));Commit("Added "+OfferAmount(kind)+" "+kind);SaveTransaction();SupplyRewardPresented?.Invoke(kind,OfferAmount(kind));return true;
        }
        public int Owned(SupplyKind kind)=>kind==SupplyKind.Search?SearchCredits:kind==SupplyKind.Magnet?MagnetCredits:kind==SupplyKind.Slot?SlotCredits:Cash;
        public int SupplyPrice(SupplyKind kind)=>kind==SupplyKind.Search?searchCost:kind==SupplyKind.Magnet?magnetCost:kind==SupplyKind.Slot?UpgradeCost:0;
        public bool BuySupply(SupplyKind kind)
        {
            if(kind==SupplyKind.Cash||kind==SupplyKind.Slot&&capacity+SlotCredits>=maxCapacity)return false;
            int price=Mathf.Max(1,SupplyPrice(kind));if(cash<price)return Fail("Not enough cash");
            cash-=price;AddSupply(kind,1);Commit("Added 1 "+kind);SaveTransaction();return true;
        }
        void AddSupply(SupplyKind kind,int amount)
        {
            if(kind==SupplyKind.Search)SearchCredits+=amount;
            else if(kind==SupplyKind.Magnet)MagnetCredits+=amount;
            else if(kind==SupplyKind.Slot){if(bundleEconomy)capacity+=amount;else SlotCredits+=amount;}
            else cash+=amount;
        }
        // Call only from a verified store/reward callback. Save the receipt and reward together.
        public bool GrantSupply(SupplyKind kind,int amount,string transaction)
        {
            if(kind==SupplyKind.Slot&&!supplyReceipts.Contains(transaction)&&capacity+SlotCredits+amount>maxCapacity)return false;
            if(string.IsNullOrWhiteSpace(transaction)||amount<=0||amount>100000||!Enum.IsDefined(typeof(SupplyKind),kind)||Owned(kind)>int.MaxValue-amount)return false;
            bool fresh=supplyReceipts.Add(transaction);
            if(fresh){LabSorting.Services.LabTelemetry.Event("supply_delivered",kind.ToString(),amount);AddSupply(kind,amount);Commit("Received "+amount+" "+kind);}
            bool saved=SaveRedundantTransaction();
            if(fresh&&saved)SupplyRewardPresented?.Invoke(kind,amount);
            return saved;
        }
        void CaptureSupplies(Snapshot data){CaptureCompletionHistory(data);data.searchCredits=SearchCredits;data.magnetCredits=MagnetCredits;data.slotCredits=SlotCredits;data.supplyReceipts=new List<string>(supplyReceipts);}
        bool ValidSupplies(Snapshot data)=>data.searchCredits>=0&&data.magnetCredits>=0&&data.slotCredits>=0&&(data.supplyReceipts==null||data.supplyReceipts.TrueForAll(x=>!string.IsNullOrWhiteSpace(x)));
        void RestoreSupplies(Snapshot data){SearchCredits=data.searchCredits;MagnetCredits=data.magnetCredits;SlotCredits=data.slotCredits;supplyReceipts=new HashSet<string>(data.supplyReceipts??new List<string>());}
    }
}
