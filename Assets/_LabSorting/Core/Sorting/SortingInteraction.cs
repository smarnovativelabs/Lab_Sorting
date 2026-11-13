using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        [Header("Target assistance and hold")]
        [Range(.01f,.2f)] public float aimAssistRadius=.09f;
        public float holdDelay=.42f, holdInterval=.28f;
        readonly RaycastHit[] aimHits=new RaycastHit[96];
        SortItem pendingTarget;
        float pendingTargetSince;
        Vector3 lockedAim,lockedOrigin;
        ItemDefinition recentPickType;
        float recentPickUntil;
        [Range(0,.15f)] public float targetSwitchDelay=.075f;
        public bool TryTouchPick(Vector2 screen)
        {
            if(!Ready())return false;
            // Exact visible geometry wins; short pixel offsets assist tiny models in dense clutter.
            // Each candidate is a first ray hit: never pick through a wall or another item.
            for(int i=0;i<9;i++)
            {
                float angle=(i-1)*Mathf.PI*.25f;
                var p=screen+(i==0?Vector2.zero:new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Screen.height*.012f);
                var ray=player.view.ScreenPointToRay(p);
                if(!Physics.Raycast(ray,out var hit,reach,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                var item=hit.collider.GetComponentInParent<SortItem>();
                if(item&&!item.Moving&&item.location!=ItemLocation.Hand)return TryPick(item);
                if(i==0&&hit.distance<.4f)return false;
            }
            return false;
        }
        bool holding,holdPlace;
        float repeatAt;
        ItemDefinition holdType;
        SortRack holdRack;
        bool bufferedTap,bufferedPlace,bufferedRepeat;
        float bufferedUntil;
        SortItem bufferedItem;
        SortRack bufferedRack;
        ItemDefinition bufferedType;
        public bool BufferedTap=>bufferedTap;
        public enum TapBufferState { None, Waiting, Completed, Cancelled, Expired, TargetChanged, Rejected }
        public TapBufferState BufferedTapState {get;private set;}
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
            // The rack's whole silhouette is targetable, including gaps between its boards.
            // Real foreground geometry still wins, so this cannot select through a wall or loose item.
            if(!item||item.location==ItemLocation.Rack)
            {
                var ray=new Ray(origin,forward);float nearest=reach;
                float obstacle=direct.collider?direct.distance:reach;
                foreach(var candidate in racks)
                {
                    if(!candidate||!candidate.gameObject.activeInHierarchy)continue;
                    if(RackBounds(candidate).IntersectRay(ray,out float distance)&&distance<=nearest&&distance<=obstacle+.01f)
                    {rack=candidate;nearest=distance;}
                }
            }
            // A tiny edge wobble gets a short grace period, but deliberate look/movement or occlusion releases immediately.
            bool steady=TargetItem&&Vector3.Angle(lockedAim,forward)<1.25f&&(origin-lockedOrigin).sqrMagnitude<.0064f;
            if(item&&item!=TargetItem&&!rack&&steady&&Candidate(TargetItem,aimAssistRadius*.4f,out _))
            {
                if(pendingTarget!=item){pendingTarget=item;pendingTargetSince=Time.unscaledTime;}
                if(Time.unscaledTime-pendingTargetSince<targetSwitchDelay)item=TargetItem;
            }
            else {pendingTarget=null;pendingTargetSince=0;}
            if(!item&&!rack&&steady&&Candidate(TargetItem,aimAssistRadius*1.5f,out _)) item=TargetItem;
            if(!item&&!rack)
            {
                float best=float.MaxValue;
                int count=Physics.SphereCastNonAlloc(origin,aimAssistRadius,forward,aimHits,reach,~(1<<2),QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var candidate=aimHits[i].collider.GetComponentInParent<SortItem>();
                    if(!Candidate(candidate,aimAssistRadius,out float score))continue;
                    var preferred=holding&&!holdPlace?holdType:Time.unscaledTime<recentPickUntil?recentPickType:null;
                    if(preferred&&candidate.definition!=preferred)score+=aimAssistRadius*.3f;
                    if(score<best){best=score;item=candidate;}
                }
            }
            if(item&&item.location==ItemLocation.Rack)
            {
                if(rack&&rack!=item.rack)item=null; // Do not jump to a second rack through the first rack's gaps.
                else rack=item.rack;
            }
            if(item!=TargetItem){if(TargetItem)TargetItem.Highlight(false);TargetItem=item;lockedAim=forward;lockedOrigin=origin;pendingTarget=null;if(item)item.Highlight(true);}
            if(rack!=TargetRack){if(TargetRack)TargetRack.Preview(null);TargetRack=rack;}
            if(TargetRack)TargetRack.Preview(Active?Active.definition:null);
        }
        public void BeginHold(bool place)
        {
            CancelPendingDrop();CancelHold();if(player.blocked)return;RefreshTarget();
            if(!Ready()||(place&&Active&&Active.Moving))
            {
                if(place?Active&&TargetRack&&TargetRack.CanAccept(Active.definition):TargetItem&&!TargetItem.Moving&&carried.Count<capacity)
                {bufferedTap=true;BufferedTapState=TapBufferState.Waiting;bufferedPlace=place;bufferedRepeat=true;
                    // Arrival tweens can update after this component on a slow frame. Allow bounded grace after cooldown.
                    bufferedUntil=Mathf.Max(Time.unscaledTime,nextAction)+.5f;bufferedItem=TargetItem;bufferedRack=TargetRack;bufferedType=place?Active.definition:TargetItem.definition;}
                return;
            }
            holdPlace=place;holdType=place?(Active?Active.definition:null):(TargetItem?TargetItem.definition:null);holdRack=TargetRack;
            bool success=place?TryPlace(TargetRack):TryPick(TargetItem);
            holding=success;repeatAt=Time.unscaledTime+Mathf.Max(.3f,holdDelay);
        }
        public void EndHold(bool place){if(bufferedTap&&bufferedPlace==place)bufferedRepeat=false;if(holding&&holdPlace==place)CancelHold();}
        public int HoldGeneration {get;private set;}
        public void CancelHold(){HoldGeneration++;holding=false;holdType=null;holdRack=null;if(bufferedTap)BufferedTapState=TapBufferState.Cancelled;bufferedTap=false;bufferedItem=null;bufferedRack=null;bufferedType=null;}
        void TickBufferedTap()
        {
            if(!bufferedTap)return;
            bool valid=!player.blocked&&Time.unscaledTime<=bufferedUntil&&(bufferedPlace?Active&&Active.definition==bufferedType&&TargetRack==bufferedRack&&bufferedRack&&bufferedRack.CanAccept(bufferedType):TargetItem&&TargetItem==bufferedItem&&!TargetItem.Moving&&TargetItem.location!=ItemLocation.Hand&&carried.Count<capacity);
            if(!valid){bool expired=Time.unscaledTime>bufferedUntil;CancelHold();BufferedTapState=expired?TapBufferState.Expired:TapBufferState.TargetChanged;return;}
            if(!Ready()||(bufferedPlace&&Active.Moving))return;
            bool place=bufferedPlace,repeat=bufferedRepeat;var type=bufferedType;var rack=bufferedRack;bufferedTap=false;
            bool success=place?TryPlace(rack):TryPick(bufferedItem);
            BufferedTapState=success?TapBufferState.Completed:TapBufferState.Rejected;
            holding=success&&repeat;holdPlace=place;holdType=type;holdRack=rack;repeatAt=Time.unscaledTime+Mathf.Max(.3f,holdDelay);
            bufferedItem=null;bufferedRack=null;bufferedType=null;
        }
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
