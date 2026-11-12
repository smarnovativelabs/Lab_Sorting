using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        [SerializeField] int cash=200;
        [Min(1)] public int searchCost=10,magnetCost=25,upgradeBaseCost=50,upgradeStepCost=25;
        [Min(0)] public int placementReward=0; // Legacy serialized field; placement never awards cash.
        [Range(10,30)] public int maxCapacity=15;
        [Min(1)] public float searchDuration=8;
        HashSet<string> rewardedItems=new HashSet<string>();
        ItemDefinition searchType;
        float searchUntil;
        float magnetUntil;
        ItemDefinition magnetContext;
        public string MagnetStatus {get;private set;}="";
        public float MagnetSeconds=>magnetContext==(Active?Active.definition:null)?Mathf.Max(0,magnetUntil-Time.unscaledTime):0;
        bool MagnetEmpty(string message,string status){magnetContext=Active?Active.definition:null;MagnetStatus=status;magnetUntil=Time.unscaledTime+1.2f;return Fail(message);}
        void ResetMagnetFeedback(){magnetUntil=0;magnetContext=null;MagnetStatus="";}
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
            foreach(var item in ItemsOfType(type)) if(item.location==ItemLocation.Loose && item.definition==type) count++;
            if(count==0) return Fail("No loose "+type.displayName+" remaining — no charge");
            int cost=Mathf.Max(1,searchCost);if(SearchCredits<=0&&cash<cost) return Fail("Get search uses with cash, a rewarded ad or the store");
            if(SearchCredits>0)SearchCredits--;else cash-=cost;searchType=type;searchUntil=Time.unscaledTime+Mathf.Max(1,searchDuration);
            foreach(var item in ItemsOfType(type)) if(item.location==ItemLocation.Loose && item.definition==type) item.SetSearchHighlight(true);
            Commit("Search: "+count+" loose "+type.displayName+" found");SearchStarted?.Invoke(type,count);SaveTransaction();return true;
        }
        public bool TryMagnet()
        {
            if(player.blocked) return false;
            if(MagnetSeconds>0)return false;
            if(carried.Count>=capacity) return Fail("Hands full — magnet not charged");
            if(!Active) return MagnetEmpty("Pick or select an item first — magnet not charged","SELECT ITEM");
            if(Active.Moving) return Fail("Wait for pickup to finish — no charge");
            var type=Active.definition;int available=0;
            foreach(var item in ItemsOfType(type)) if(item.location==ItemLocation.Loose && item.definition==type && !item.Moving) available++;
            if(available==0) return MagnetEmpty("No loose "+type.displayName+" available — no charge","NO MATCH");
            int cost=Mathf.Max(1,magnetCost);if(MagnetCredits<=0&&cash<cost) return Fail("Get magnet uses with cash, a rewarded ad or the store");
            int collected=0;var origins=new List<Vector3>(capacity-carried.Count);
            // No distance/visibility filter. Only loose items, up to free capacity.
            foreach(var item in ItemsOfType(type))
            {
                if(carried.Count>=capacity) break;
                if(item.location==ItemLocation.Loose && item.definition==type && !item.Moving){var origin=item.transform.position;if(TryPick(item,true,collected*.055f)){collected++;origins.Add(origin);}}
            }
            if(collected==0) return Fail("Nothing collected — no charge");
            if(MagnetCredits>0)MagnetCredits--;else cash-=cost;magnetContext=type;MagnetStatus="COLLECTED "+collected;magnetUntil=Time.unscaledTime+Mathf.Max(.8f,.4f+collected*.055f);Commit("Magnet collected "+collected+" "+type.displayName);Feedback?.Invoke(SortingCue.Pick);MagnetCollected?.Invoke(type,origins.ToArray());SaveTransaction();return true;
        }
        public bool TryUpgradeCapacity()
        {
            if(player.blocked) return false;
            if(capacity>=maxCapacity) return Fail("Maximum capacity reached — no charge");
            int cost=UpgradeCost;if(SlotCredits<=0&&cash<cost) return Fail("Get a slot with cash, a rewarded ad or the store");
            if(SlotCredits>0)SlotCredits--;else cash-=cost;capacity++;Commit("Capacity upgraded to "+capacity);SaveTransaction();LabSorting.Services.LabTelemetry.Event("capacity_upgraded","slots",capacity);return true;
        }
        void UpdateSearch() { if(searchType && Time.unscaledTime>=searchUntil) {EndSearch();Notify();} }
        void EndSearch() { if(searchType) foreach(var item in ItemsOfType(searchType)) if(item) item.SetSearchHighlight(false);searchType=null;searchUntil=0; }
        public SortItem NearestSearchResult()
        {
            if(!searchType) return null;SortItem nearest=null;float best=float.PositiveInfinity;
            foreach(var item in ItemsOfType(searchType)) if(item.location==ItemLocation.Loose && item.definition==searchType)
            {float d=(item.transform.position-player.transform.position).sqrMagnitude;if(d<best){best=d;nearest=item;}}
            return nearest;
        }
    }
}


