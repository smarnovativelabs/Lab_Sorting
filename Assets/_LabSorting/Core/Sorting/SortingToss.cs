using System;
using UnityEngine;
namespace LabSorting.Sorting
{
    // Trick shot: aim at a rack that is too far to place on but can take the held item, with a clear arc to it,
    // and THROW becomes TOSS. The item flies into the rack's next point. First placements pay a small bonus,
    // doubled from the third toss in a row. Placing normally or dropping ends the streak.
    public sealed partial class SortingSession
    {
        [Header("Toss into far racks")]
        [Min(0)] public float tossRange=9f;
        [Min(0)] public int tossBonus=2;
        [Min(1)] public int tossStreakForDouble=3;
        [Min(1)] public float tossStreakWindow=10f;
        public SortRack TossRack {get;private set;}
        public int TossStreak {get;private set;}
        public event Action<SortItem,bool> ItemLanded;   // item, tossed into a rack
        public event Action<int,int> TossScored;          // streak, bonus paid
        float lastToss=-99;
        readonly RaycastHit[] tossHits=new RaycastHit[16];
        readonly Collider[] tossOverlaps=new Collider[32];
        public Collider TossBlocker {get;private set;}
        public string TossBlockedStage {get;private set;}
        bool BlockToss(Collider collider,string stage){TossBlocker=collider;TossBlockedStage=stage;return false;}

        // DROP never changes into an automatic rack toss.
        void RefreshToss() { SetTossRack(null); }
        readonly System.Collections.Generic.Dictionary<SortRack,Bounds> rackBounds=new System.Collections.Generic.Dictionary<SortRack,Bounds>();
        Bounds RackBounds(SortRack rack)
        {
            if(rackBounds.TryGetValue(rack,out var bounds))return bounds;
            bounds=new Bounds(rack.transform.position,Vector3.zero);
            foreach(var c in rack.GetComponentsInChildren<Collider>())if(!c.GetComponentInParent<SortItem>())bounds.Encapsulate(c.bounds);
            rackBounds[rack]=bounds;return bounds;
        }
        void SetTossRack(SortRack rack)
        {
            if(rack==TossRack)return;
            if(TossRack&&TossRack!=TargetRack)TossRack.Preview(null);
            TossRack=rack;if(rack)rack.Preview(Active?Active.definition:null);
        }
        // Where the item would land and whether the flight path is clear of walls, racks and ceilings.
        public bool TossPlan(SortItem item,SortRack rack,out int slot,out Pose pose,out Vector3 entry)
        {
            TossBlocker=null;TossBlockedStage=null;
            pose=default;entry=default;slot=-1;if(!item||!item.definition||!rack)return false;
            slot=rack.NextSlot(item.definition);if(slot<0)return false;
            pose=rack.SlotPoseFor(item.definition,slot);
            var local=rack.transform.InverseTransformPoint(pose.position);
            // Edge slots sit behind the vertical front posts. Enter through the middle opening,
            // then move sideways over the board; a straight path to an edge slot clips the post.
            float entryX=local.x;
            if(rack.SizeBased)
            {
                var bounds=SortRack.RotatedBounds(item.definition,Quaternion.Inverse(rack.transform.rotation)*pose.rotation);
                var width=rack.shelves[0].x;float margin=bounds.extents.x+.13f;
                if(width.x+margin>width.y-margin)return false;
                entryX=Mathf.Clamp(local.x,width.x+margin,width.y-margin);
            }
            entry=rack.transform.TransformPoint(new Vector3(entryX,local.y+.08f,-.62f));
            var from=item.transform.position;float apex=SortItem.TossApex(from,entry);
            // The item tumbles around its pivot: enclose every corner at any rotation, including its centre offset.
            var size=item.definition.size;
            if(size.x<=0||size.y<=0||size.z<=0)return false;
            float radius=(item.definition.center.magnitude+size.magnitude*.5f)*Mathf.Max(1,item.heldScale)+.003f;
            if(radius<=0||float.IsNaN(radius)||float.IsInfinity(radius))return false;
            var previous=from;
            for(int step=1;step<=20;step++)
            {
                var point=SortItem.ArcPoint(from,entry,apex,step/20f);var delta=point-previous;float length=delta.magnitude;
                int overlaps=Physics.OverlapSphereNonAlloc(previous,radius,tossOverlaps,~(1<<2),QueryTriggerInteraction.Ignore);
                if(overlaps==tossOverlaps.Length)return false;
                for(int i=0;i<overlaps;i++)if(TossObstacle(tossOverlaps[i],item))return BlockToss(tossOverlaps[i],"arc overlap");
                if(length>.0001f)
                {
                    int count=Physics.SphereCastNonAlloc(previous,radius,delta/length,tossHits,length,~(1<<2),QueryTriggerInteraction.Ignore);
                    if(count==tossHits.Length)return false;
                    for(int i=0;i<count;i++)
                    {
                        var other=tossHits[i].collider;
                        if(TossObstacle(other,item))return BlockToss(other,"arc sweep");
                    }
                }
                previous=point;
            }
            // The slide after the arc is part of the flight too. Check its full oriented footprint.
            var offset=pose.rotation*item.definition.center;
            var half=size*.5f;var above=SortItem.TossSettlePoint(entry,pose.position);
            for(int endpoint=0;endpoint<3;endpoint++)
            {
                var end=endpoint==0?entry:endpoint==1?above:pose.position;
                int count=Physics.OverlapBoxNonAlloc(end+offset,half,tossOverlaps,pose.rotation,~(1<<2),QueryTriggerInteraction.Ignore);
                if(count==tossOverlaps.Length)return false;
                for(int i=0;i<count;i++)if(TossObstacle(tossOverlaps[i],item))return BlockToss(tossOverlaps[i],"slide overlap");
            }
            for(int segment=0;segment<2;segment++)
            {
                var start=segment==0?entry:above;var end=segment==0?above:pose.position;var slide=end-start;
                if(slide.sqrMagnitude<=.000001f)continue;
                int count=Physics.BoxCastNonAlloc(start+offset,half,slide.normalized,tossHits,pose.rotation,slide.magnitude,~(1<<2),QueryTriggerInteraction.Ignore);
                if(count==tossHits.Length)return false;
                for(int i=0;i<count;i++)if(TossObstacle(tossHits[i].collider,item))return BlockToss(tossHits[i].collider,"slide sweep");
            }
            return true;
        }
        bool TossObstacle(Collider other,SortItem item)=>other&&!other.transform.IsChildOf(item.transform)&&(!player||!other.transform.IsChildOf(player.transform));
        bool TryToss(SortRack rack)
        {
            var item=Active;
            if(!rack||!rack.CanAccept(item.definition)||!TossPlan(item,rack,out int slot,out var pose,out var entry))return false;
            int index=rack.Add(item);if(index!=slot){rack.Remove(item);item.location=ItemLocation.Hand;return false;}
            RemoveActive();item.transform.SetParent(null,true);
            bool firstPlacement=rewardedItems.Add(item.id);
            TossStreak=Time.unscaledTime-lastToss<=tossStreakWindow?TossStreak+1:1;lastToss=Time.unscaledTime;
            int bonus=0;
            item.Toss(entry,pose.position,pose.rotation,()=>ItemLanded?.Invoke(item,true));
            SetTossRack(null);
            CheckSmallGoals(rack,firstPlacement);CheckMilestones();
            Commit("Nice shot! "+item.definition.displayName);Feedback?.Invoke(SortingCue.Toss);TossScored?.Invoke(TossStreak,bonus);
            return true;
        }
        void EndTossStreak(){TossStreak=0;lastToss=-99;}
        void NotifyLanded(SortItem item){ItemLanded?.Invoke(item,false);}
    }
}
