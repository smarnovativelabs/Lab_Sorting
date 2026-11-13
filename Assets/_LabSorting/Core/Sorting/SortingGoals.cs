using System;
using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        public const int ShortGoalSize=25,ShortGoalBonus=2,RackBonus=1;
        HashSet<string> completedRacks=new HashSet<string>();
        public event Action<string,int> SmallGoalEarned;
        public event Action<SortRack> RackCompleted;
        public int CompletedRackCount=>completedRacks.Count;
        public bool ShortGoalsComplete=>items.Length>0&&rewardedItems.Count>=items.Length;
        public int ShortGoalOrdinal=>rewardedItems.Count/ShortGoalSize;
        public int ShortGoalTarget=>Mathf.Min(ShortGoalSize,Mathf.Max(0,items.Length-ShortGoalOrdinal*ShortGoalSize));
        public int ShortGoalProgress=>ShortGoalsComplete?ShortGoalTarget:rewardedItems.Count%ShortGoalSize;
        public float ShortGoalFraction=>ShortGoalsComplete?1:ShortGoalTarget>0?(float)ShortGoalProgress/ShortGoalTarget:0;
        public const int CleanupThreshold=15;
        // Evaluated by the HUD on revision changes, not every frame. These optional
        // focus suggestions use saved world state; cash remains tied to unique IDs.
        public string ShortGoalFocus()
        {
            int remaining=items.Length-SortedCount;
            if(remaining>0&&remaining<=CleanupThreshold)
                return "ALMOST DONE!  "+remaining+" left to sort";
            int mode=ShortGoalOrdinal%3;
            if(mode==0)return (ShortGoalTarget-ShortGoalProgress)+" new items to next reward";
            var progress=GetItemProgress();
            if(mode==1)
            {
                SortRack best=null;
                foreach(var rack in racks)
                {
                    if(!rack||!rack.Type||rack.Count==0||rack.RemainingSlots<=0)continue;
                    var row=progress.Find(p=>p.type==rack.Type);
                    if(row==null||row.Remaining<rack.RemainingSlots)continue;
                    if(best==null||rack.RemainingSlots<best.RemainingSlots)best=rack;
                }
                if(best)return "Focus: finish a rack  "+best.Count+" / "+best.Capacity;
            }
            ItemProgress closest=null;
            foreach(var row in progress)
            {
                if(row.Remaining<=0)continue;
                bool fits=false;foreach(var rack in racks)if(rack&&rack.CanAccept(row.type)){fits=true;break;}
                if(fits&&(closest==null||row.Remaining<closest.Remaining))closest=row;
            }
            return closest!=null?"Focus: "+closest.type.displayName+"  "+closest.sorted+" / "+closest.total:
                (ShortGoalTarget-ShortGoalProgress)+" new items to next reward";
        }
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
            if(rack.Capacity>0&&rack.Count==rack.Capacity&&completedRacks.Add(rack.id))
            {cash+=RackBonus;SmallGoalEarned?.Invoke("Rack complete!",RackBonus);RackCompleted?.Invoke(rack);}
        }
    }
}

