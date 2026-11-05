using System;
using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        public const int ShortGoalSize=25,ShortGoalBonus=10,RackBonus=10;
        HashSet<string> completedRacks=new HashSet<string>();
        public event Action<string,int> SmallGoalEarned;
        public event Action<SortRack> RackCompleted;
        public int CompletedRackCount=>completedRacks.Count;
        public string ShortGoalText
        {
            get
            {
                int done=rewardedItems.Count;
                if(done>=items.Length)return "SHORT GOALS COMPLETE";
                int start=done/ShortGoalSize*ShortGoalSize,target=Mathf.Min(start+ShortGoalSize,items.Length);
                return "SHORT GOAL  Sort new items  "+(done-start)+" / "+(target-start)+"   +$"+ShortGoalBonus;
            }
        }
        void CheckSmallGoals(SortRack rack,bool firstPlacement)
        {
            int done=rewardedItems.Count;
            if(firstPlacement&&(done%ShortGoalSize==0||done==items.Length))
            {cash+=ShortGoalBonus;SmallGoalEarned?.Invoke("Sorting goal complete!",ShortGoalBonus);}
            if(rack.slots.Length>0&&rack.Count==rack.slots.Length&&completedRacks.Add(rack.id))
            {cash+=RackBonus;SmallGoalEarned?.Invoke("Rack complete!",RackBonus);RackCompleted?.Invoke(rack);}
        }
    }
}
