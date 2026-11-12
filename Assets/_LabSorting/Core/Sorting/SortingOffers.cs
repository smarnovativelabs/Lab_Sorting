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
            else if(kind==SupplyKind.Slot)SlotCredits+=amount;
            else cash+=amount;
        }
        // Call only from a verified store/reward callback. Save the receipt and reward together.
        public bool GrantSupply(SupplyKind kind,int amount,string transaction)
        {
            if(string.IsNullOrWhiteSpace(transaction)||amount<=0||amount>100000||!Enum.IsDefined(typeof(SupplyKind),kind)||Owned(kind)>int.MaxValue-amount)return false;
            if(supplyReceipts.Add(transaction)){AddSupply(kind,amount);Commit("Received "+amount+" "+kind);}
            if(!persistence)return true;
            return RequestSave()&&FlushSaves();
        }
        void CaptureSupplies(Snapshot data){data.searchCredits=SearchCredits;data.magnetCredits=MagnetCredits;data.slotCredits=SlotCredits;data.supplyReceipts=new List<string>(supplyReceipts);}
        bool ValidSupplies(Snapshot data)=>data.searchCredits>=0&&data.magnetCredits>=0&&data.slotCredits>=0&&(data.supplyReceipts==null||data.supplyReceipts.TrueForAll(x=>!string.IsNullOrWhiteSpace(x)));
        void RestoreSupplies(Snapshot data){SearchCredits=data.searchCredits;MagnetCredits=data.magnetCredits;SlotCredits=data.slotCredits;supplyReceipts=new HashSet<string>(data.supplyReceipts??new List<string>());}
    }
}
