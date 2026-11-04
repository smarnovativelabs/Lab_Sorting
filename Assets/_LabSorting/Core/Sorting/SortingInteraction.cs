using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        [Header("Target assistance and hold")]
        [Range(.01f,.2f)] public float aimAssistRadius=.09f;
        public float holdDelay=.42f, holdInterval=.28f;
        readonly RaycastHit[] aimHits=new RaycastHit[32];
        bool holding,holdPlace;
        float repeatAt;
        ItemDefinition holdType;
        SortRack holdRack;
        public bool Holding => holding;
        bool Candidate(SortItem item,float radius,out float score)
        {
            score=float.MaxValue;
            if(!item||!item.gameObject.activeInHierarchy||item.Moving||item.location==ItemLocation.Hand||!item.hitbox||!item.hitbox.enabled) return false;
            var origin=player.view.transform.position;var forward=player.view.transform.forward;
            float depth=Vector3.Dot(item.hitbox.bounds.center-origin,forward);
            if(depth<=0||depth>reach) return false;
            var point=item.hitbox.ClosestPoint(origin+forward*depth);
            var delta=point-origin;score=Vector3.Cross(delta,forward).magnitude;
            if(score>radius||delta.magnitude>reach) return false;
            // Assistance never bypasses a wall, shelf or another item's collider.
            return Physics.Raycast(origin,delta.normalized,out var hit,delta.magnitude+.02f,~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<SortItem>()==item;
        }
        public void RefreshTarget()
        {
            if(player.blocked){ClearTarget();return;}
            var origin=player.view.transform.position;var forward=player.view.transform.forward;
            SortItem item=null;SortRack rack=null;
            if(Physics.Raycast(origin,forward,out var direct,reach,~(1<<2),QueryTriggerInteraction.Ignore))
            {item=direct.collider.GetComponentInParent<SortItem>();rack=direct.collider.GetComponentInParent<SortRack>();}
            if(item&&!Candidate(item,aimAssistRadius,out _))item=null;
            // A direct item hit always wins. Retention applies only to slight misses.
            if(!item&&Candidate(TargetItem,aimAssistRadius*1.5f,out _)) item=TargetItem;
            if(!item)
            {
                float best=float.MaxValue;
                int count=Physics.SphereCastNonAlloc(origin,aimAssistRadius,forward,aimHits,reach,~(1<<2),QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var candidate=aimHits[i].collider.GetComponentInParent<SortItem>();
                    if(Candidate(candidate,aimAssistRadius,out float score)&&score<best){best=score;item=candidate;}
                }
            }
            if(item&&item.location==ItemLocation.Rack)rack=item.rack;
            if(item!=TargetItem){if(TargetItem)TargetItem.Highlight(false);TargetItem=item;if(item)item.Highlight(true);}
            if(rack!=TargetRack){if(TargetRack)TargetRack.Preview(null);TargetRack=rack;}
            if(TargetRack)TargetRack.Preview(Active?Active.definition:null);
        }
        public void BeginHold(bool place)
        {
            CancelHold();if(!Ready())return;RefreshTarget();
            holdPlace=place;holdType=place?(Active?Active.definition:null):(TargetItem?TargetItem.definition:null);holdRack=TargetRack;
            bool success=place?TryPlace(TargetRack):TryPick(TargetItem);
            holding=success;repeatAt=Time.unscaledTime+Mathf.Max(.3f,holdDelay);
        }
        public void EndHold(bool place){if(holding&&holdPlace==place)CancelHold();}
        public void CancelHold(){holding=false;holdType=null;holdRack=null;}
        void TickHold()
        {
            if(!holding)return;
            if(player.blocked){CancelHold();return;}
            if(Time.unscaledTime<repeatAt||!Ready())return;
            // A slow frame can leave scaled-time animation behind the unscaled repeat timer.
            // Wait for the held item's motion instead of treating that transient state as invalid.
            if(holdPlace&&Active&&Active.Moving)return;
            bool valid=holdPlace?Active&&!Active.Moving&&Active.definition==holdType&&TargetRack==holdRack&&holdRack&&holdRack.CanAccept(holdType):TargetItem&&!TargetItem.Moving&&TargetItem.definition==holdType&&carried.Count<capacity;
            if(!valid){CancelHold();return;}
            bool success=holdPlace?TryPlace(TargetRack):TryPick(TargetItem);
            if(!success)CancelHold();else repeatAt=Time.unscaledTime+Mathf.Max(.25f,holdInterval);
        }
    }
}
