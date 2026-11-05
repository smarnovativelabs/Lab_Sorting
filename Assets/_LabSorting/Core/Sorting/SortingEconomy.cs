using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        [SerializeField] int cash=200;
        [Min(1)] public int searchCost=10,magnetCost=25,upgradeBaseCost=50,upgradeStepCost=25;
        [Min(0)] public int placementReward=5;
        [Range(10,30)] public int maxCapacity=15;
        [Min(1)] public float searchDuration=8;
        HashSet<string> rewardedItems=new HashSet<string>();
        ItemDefinition searchType;
        float searchUntil;
        public int Cash => cash;
        public int UpgradeCost => Mathf.Max(1,upgradeBaseCost+Mathf.Max(0,capacity-10)*upgradeStepCost);
        public ItemDefinition SearchType => searchType;
        public float SearchSeconds => searchType?Mathf.Max(0,searchUntil-Time.unscaledTime):0;
        public void UseSearch() { TrySearch(); }
        public void UseMagnet() { TryMagnet(); }
        public void UpgradeCapacity() { TryUpgradeCapacity(); }
        public bool TrySearch()
        {
            if(player.blocked) return false;
            if(!Active) return Fail("Select a carried item first");
            if(searchType) return Fail("Search already active — no charge");
            var type=Active.definition;int count=0;
            foreach(var item in items) if(item.location==ItemLocation.Loose && item.definition==type) count++;
            if(count==0) return Fail("No loose "+type.displayName+" remaining — no charge");
            int cost=Mathf.Max(1,searchCost);if(cash<cost) return Fail("Not enough cash for search");
            cash-=cost;searchType=type;searchUntil=Time.unscaledTime+Mathf.Max(1,searchDuration);
            foreach(var item in items) if(item.location==ItemLocation.Loose && item.definition==type) item.SetSearchHighlight(true);
            Commit("Search: "+count+" loose "+type.displayName+" found");SearchStarted?.Invoke(type,count);SaveTransaction();return true;
        }
        public bool TryMagnet()
        {
            if(player.blocked) return false;
            if(carried.Count>=capacity) return Fail("Hands full — magnet not charged");
            if(!Active) return Fail("Select a carried item first");
            if(Active.Moving) return Fail("Wait for pickup to finish — no charge");
            var type=Active.definition;int available=0;
            foreach(var item in items) if(item.location==ItemLocation.Loose && item.definition==type && !item.Moving) available++;
            if(available==0) return Fail("No loose "+type.displayName+" available — no charge");
            int cost=Mathf.Max(1,magnetCost);if(cash<cost) return Fail("Not enough cash for magnet");
            int collected=0;var origins=new List<Vector3>(capacity-carried.Count);
            // No distance/visibility filter. Only loose items, up to free capacity.
            foreach(var item in items)
            {
                if(carried.Count>=capacity) break;
                if(item.location==ItemLocation.Loose && item.definition==type && !item.Moving){var origin=item.transform.position;if(TryPick(item)){collected++;origins.Add(origin);}}
            }
            if(collected==0) return Fail("Nothing collected — no charge");
            cash-=cost;Commit("Magnet collected "+collected+" "+type.displayName);MagnetCollected?.Invoke(type,origins.ToArray());SaveTransaction();return true;
        }
        public bool TryUpgradeCapacity()
        {
            if(player.blocked) return false;
            if(capacity>=maxCapacity) return Fail("Maximum capacity reached — no charge");
            int cost=UpgradeCost;if(cash<cost) return Fail("Not enough cash for upgrade");
            cash-=cost;capacity++;Commit("Capacity upgraded to "+capacity);SaveTransaction();return true;
        }
        void UpdateSearch() { if(searchType && Time.unscaledTime>=searchUntil) {EndSearch();Notify();} }
        void EndSearch() { if(items!=null) foreach(var item in items) if(item) item.SetSearchHighlight(false);searchType=null;searchUntil=0; }
        public SortItem NearestSearchResult()
        {
            if(!searchType) return null;SortItem nearest=null;float best=float.PositiveInfinity;
            foreach(var item in items) if(item.location==ItemLocation.Loose && item.definition==searchType)
            {float d=(item.transform.position-player.transform.position).sqrMagnitude;if(d<best){best=d;nearest=item;}}
            return nearest;
        }
    }
}


