using System;
using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        [Min(0)] public int firstTenBonus=25,categoryBonus=40,halfBonus=60,completeBonus=100;
        HashSet<string> claimedMilestones=new HashSet<string>();
        public event Action<string,int> MilestoneEarned;
        public event Action<ItemDefinition,int> SearchStarted;
        public event Action<ItemDefinition,Vector3[]> MagnetCollected;
        public event Action VisualsReset;
        static readonly string[] milestoneIds={"first-ten","category","half","complete"};
        public bool HasMilestone(string id)=>claimedMilestones.Contains(id);
        public int MilestoneCount=>claimedMilestones.Count;
        bool GoalReached(string id)
        {
            if(items.Length==0)return false;
            if(id=="first-ten")return rewardedItems.Count>=Mathf.Min(10,items.Length);
            if(id=="half")return SortedCount>=Mathf.CeilToInt(items.Length*.5f);
            if(id=="complete")return SortedCount==items.Length;
            foreach(var row in GetItemProgress())if(row.total>0&&row.sorted==row.total)return true;
            return false;
        }
        int Bonus(string id)=>Mathf.Max(0,id=="first-ten"?firstTenBonus:id=="category"?categoryBonus:id=="half"?halfBonus:completeBonus);
        string GoalName(string id)=>id=="first-ten"?"First "+Mathf.Min(10,items.Length)+" items":id=="category"?"First category complete":id=="half"?"Lab 50% sorted":"Lab fully sorted";
        void CheckMilestones(bool migrate=false)
        {
            foreach(var id in milestoneIds)
                if(!claimedMilestones.Contains(id)&&GoalReached(id))
                {claimedMilestones.Add(id);if(!migrate){int reward=Bonus(id);cash+=reward;MilestoneEarned?.Invoke(GoalName(id),reward);}}
        }
        public string NextMilestoneText
        {
            get
            {
                foreach(var id in milestoneIds)
                {
                    if(HasMilestone(id))continue;
                    string progress=id=="first-ten"?Mathf.Min(rewardedItems.Count,Mathf.Min(10,items.Length))+"/"+Mathf.Min(10,items.Length):id=="category"?"Complete any item type":SortedCount+"/"+(id=="half"?Mathf.CeilToInt(items.Length*.5f):items.Length);
                    return GoalName(id)+"   "+progress+"   +$"+Bonus(id);
                }
                return "ALL MILESTONES COMPLETE";
            }
        }
    }
}
