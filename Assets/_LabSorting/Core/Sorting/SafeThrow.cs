using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        Vector3 throwLaunchPosition;
        readonly Collider[] throwOverlaps=new Collider[256];
        readonly RaycastHit[] throwHits=new RaycastHit[256];
        // A crowded floor is a valid drop: find the top of the local pile. This fallback
        // releases directly above the supported footprint, without flying through clutter.
        Vector3 FindCrowdedDrop(SortItem item)
        {
            var bounds=ReleaseBounds(item);var half=bounds.extents+Vector3.one*.02f;
            var offset=bounds.center-item.transform.position;
            var camera=player.view.transform;var forward=Vector3.ProjectOnPlane(camera.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=player.transform.forward;
            var origin=camera.position;
            for(int attempt=0;attempt<81;attempt++)
            {
                int side=attempt%9;float angle=side==0?0:(side%2==1?1:-1)*Mathf.Ceil(side/2f)*40;
                var delta=Quaternion.Euler(0,angle,0)*forward*(.55f+attempt/9*.3f);
                // Stay on this side of walls, even when the hand is visually beyond them.
                int worldMask=~((1<<2)|(1<<LayerMask.NameToLayer("SortingItem")));
                if(Physics.Raycast(origin,delta.normalized,delta.magnitude+Mathf.Max(half.x,half.z),worldMask,QueryTriggerInteraction.Ignore))continue;
                var center=origin+delta;
                for(int lift=0;lift<24;lift++)
                {
                    if(ThrowClear(center,half,item,true))break;
                    center.y+=.15f;
                }
                if(!ThrowClear(center,half,item,true))continue;
                var hits=Physics.BoxCastAll(center,half,Vector3.down,Quaternion.identity,6,~0,QueryTriggerInteraction.Ignore);
                float distance=float.PositiveInfinity;
                foreach(var hit in hits)
                {
                    if(!ThrowObstacle(hit.collider,item,true)||hit.collider.transform.IsChildOf(player.transform)||hit.normal.y<.65f)continue;
                    distance=Mathf.Min(distance,hit.distance);
                }
                if(float.IsInfinity(distance))continue;
                var end=center+Vector3.down*Mathf.Max(0,distance-.025f);
                if(!ThrowClear(end,half,item,true)||DropLandingPool.Reserved(new Bounds(end,half*2),item))continue;
                throwLaunchPosition=end-offset+Vector3.up*.04f;
                return end-offset;
            }
            // Last resort for a completely enclosed/full footprint: lift the local column
            // until the entire released bounds are clear. Never leave the item in inventory.
            var fallback=origin+forward*.35f;
            for(int i=0;i<256;i++)
            {
                var overlaps=Physics.OverlapBox(fallback,half,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                float top=fallback.y;bool blocked=false;
                foreach(var other in overlaps)if(ThrowObstacle(other,item,true))
                {blocked=true;top=Mathf.Max(top,other.bounds.max.y+half.y+.03f);}
                if(!blocked&&!DropLandingPool.Reserved(new Bounds(fallback,half*2),item))break;
                fallback.y=Mathf.Max(top,fallback.y+.1f);
            }
            throwLaunchPosition=fallback-offset;
            // Release from that clear column, but land on whatever is below it: a mid-air landing target is where the
            // item stays when the landing physics times out or no landing body is free. With nothing below, keep the old point.
            var landing=fallback;float below=float.PositiveInfinity;
            foreach(var hit in Physics.BoxCastAll(fallback,half,Vector3.down,Quaternion.identity,6,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.distance<=0||!ThrowObstacle(hit.collider,item,true)||hit.collider.transform.IsChildOf(player.transform))continue;
                below=Mathf.Min(below,hit.distance);
            }
            if(!float.IsInfinity(below))
            {
                var grounded=fallback+Vector3.down*Mathf.Max(0,below-.025f);
                if(!DropLandingPool.Reserved(new Bounds(grounded,half*2),item))landing=grounded;
            }
            return landing-offset;
        }
        public Bounds ReleaseBounds(SortItem item)
        {
            var box=item.hitbox as BoxCollider;
            if(!box)throw new System.InvalidOperationException("Sorting drop requires a BoxCollider");
            var scale=item.GroundScale;var size=Vector3.Scale(box.size,scale);size=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
            var result=new Bounds(item.transform.position+Vector3.Scale(box.center,scale),size);
            if(item.visual)
            {
                // A SkinnedMeshRenderer's localBounds live in its root bone's space (rats), not the renderer's.
                var space=item.visual is SkinnedMeshRenderer skinned&&skinned.rootBone?skinned.rootBone:item.visual.transform;
                var local=item.visual.localBounds;var relative=item.transform.worldToLocalMatrix*space.localToWorldMatrix;
                for(int i=0;i<8;i++){var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));result.Encapsulate(item.transform.position+Vector3.Scale(relative.MultiplyPoint3x4(corner),scale));}
            }
            return result;
        }
        bool ThrowObstacle(Collider other,SortItem item,bool landing)
        {
            if(other.transform.IsChildOf(item.transform))return false;
            if(!landing&&other.transform.IsChildOf(player.transform))return false;
            // Both the flight corridor and the landing must stay clear of loose items.
            return true;
        }
        bool ThrowClear(Vector3 center,Vector3 extents,SortItem item,bool landing)
        {
            int count=Physics.OverlapBoxNonAlloc(center,extents,throwOverlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(count==throwOverlaps.Length)return false;
            for(int i=0;i<count;i++)if(ThrowObstacle(throwOverlaps[i],item,landing))return false;
            return true;
        }
        public bool TryThrowPosition(SortItem item,out Vector3 destination)
        {
            destination=default;if(!item||!item.hitbox||item.location!=ItemLocation.Hand)return false;
            // Measure the actual collider at its released scale/rotation, including offset pivots.
            if(!(item.hitbox is BoxCollider))return false;
            var t=item.transform;var bounds=ReleaseBounds(item);
            var extent=bounds.extents+Vector3.one*.012f;var offset=bounds.center-t.position;
            if(extent.sqrMagnitude<.0001f)return false;
            var forward=Vector3.ProjectOnPlane(player.view.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.001f)forward=player.transform.forward;forward.Normalize();
            // Retry from the player's side if a shelf traps the visual hand's outgoing path.
            for(int launch=0;launch<2;launch++)
            {
            var start=launch==0?t.position+offset:player.view.transform.position-Vector3.up*.25f;
            if(!ThrowClear(start,extent,item,false))continue;
            // Front cone only: try close gaps first, then a little further ahead.
            for(int attempt=0;attempt<243;attempt++)
            {
                float distance=.45f+(attempt/9)*.16f;
                int side=attempt%9;float angle=side==0?0:(side%2==1?1:-1)*Mathf.Ceil(side/2f)*5;
                var origin=player.view.transform.position+(Quaternion.Euler(0,angle,0)*forward)*distance;
                int hits=Physics.RaycastNonAlloc(origin,Vector3.down,throwHits,4,~0,QueryTriggerInteraction.Ignore);
                if(hits==throwHits.Length)continue;
                float nearest=float.PositiveInfinity;RaycastHit floor=default;
                for(int i=0;i<hits;i++)if(!throwHits[i].collider.GetComponentInParent<SortItem>()&&throwHits[i].distance<nearest){nearest=throwHits[i].distance;floor=throwHits[i];}
                if(!floor.collider||floor.normal.y<.95f||floor.collider.GetComponentInParent<SortItem>()||floor.collider.GetComponentInParent<SortRack>()||floor.collider.transform.IsChildOf(player.transform)||floor.collider.attachedRigidbody)continue;
                var end=floor.point+Vector3.up*(extent.y+.025f);
                // Only bare supported floor: never create a pile by stacking released items.
                if(!ThrowClear(end,extent,item,true))continue;
                if(DropLandingPool.Reserved(new Bounds(end,extent*2),item))continue;
                // A visual hand may already be beyond a wall: camera-side visibility still wins.
                var sight=end-player.view.transform.position;
                int sightCount=Physics.RaycastNonAlloc(player.view.transform.position,sight.normalized,throwHits,Mathf.Max(0,sight.magnitude-.02f),~0,QueryTriggerInteraction.Ignore);
                bool visible=sightCount<throwHits.Length;
                for(int i=0;i<sightCount&&visible;i++)
                {
                    var loose=throwHits[i].collider.GetComponentInParent<SortItem>();
                    // Loose items can sit below the arc; the swept corridor checks them below.
                    if(loose&&loose.location==ItemLocation.Loose)continue;
                    if(ThrowObstacle(throwHits[i].collider,item,false))visible=false;
                }
                if(!visible)continue;
                bool supported=true;
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                {
                    var corner=floor.point+new Vector3(x*extent.x,.08f,z*extent.z);
                    int count=Physics.RaycastNonAlloc(corner,Vector3.down,throwHits,.12f,~0,QueryTriggerInteraction.Ignore);bool cornerSupported=false;
                    for(int i=0;i<count;i++){var support=throwHits[i];if(support.normal.y>=.95f&&!support.collider.GetComponentInParent<SortItem>()&&!support.collider.GetComponentInParent<SortRack>())cornerSupported=true;}
                    if(!cornerSupported||count==throwHits.Length)supported=false;
                }
                if(!supported)continue;
                bool clear=true;var previous=start;
                // Same arc as SortItem.Travel; sweep full-size boxes between samples.
                for(int step=1;step<=16&&clear;step++)
                {
                    float fraction=step/16f;var point=SortItem.DropPoint(start,end,fraction);
                    var delta=point-previous;float length=delta.magnitude;
                    int count=Physics.BoxCastNonAlloc(previous,extent,delta.normalized,throwHits,Quaternion.identity,length,~0,QueryTriggerInteraction.Ignore);
                    clear=count<throwHits.Length&&ThrowClear(point,extent,item,false);
                    for(int i=0;i<count&&clear;i++)if(ThrowObstacle(throwHits[i].collider,item,false))clear=false;
                    previous=point;
                }
                var landing=end-offset;var planar=Vector3.ProjectOnPlane(landing-player.view.transform.position,Vector3.up);
                if(clear&&planar.sqrMagnitude>.01f&&Vector3.Angle(forward,planar)<=20.01f){destination=landing;throwLaunchPosition=start-offset;return true;}
            }
            }
            return false;
        }
    }
}
