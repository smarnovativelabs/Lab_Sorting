using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        readonly Collider[] throwOverlaps=new Collider[64];
        readonly RaycastHit[] throwHits=new RaycastHit[64];
        bool ThrowObstacle(Collider other,SortItem item,bool landing)
        {
            if(other.transform.IsChildOf(item.transform))return false;
            if(!landing&&other.transform.IsChildOf(player.transform))return false;
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
            var t=item.transform;var rotation=t.rotation;var scale=t.localScale;bool enabled=item.hitbox.enabled;
            Bounds bounds;
            try {t.rotation=Quaternion.identity;t.localScale=item.originalScale;item.hitbox.enabled=true;Physics.SyncTransforms();bounds=item.hitbox.bounds;}
            finally {t.rotation=rotation;t.localScale=scale;item.hitbox.enabled=enabled;Physics.SyncTransforms();}
            var extent=bounds.extents+Vector3.one*.012f;var offset=bounds.center-t.position;
            if(extent.sqrMagnitude<.0001f)return false;
            var forward=Vector3.ProjectOnPlane(player.view.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.001f)forward=player.transform.forward;forward.Normalize();
            var start=t.position+offset;
            if(!ThrowClear(start,extent,item,false))return false;
            for(int attempt=0;attempt<4;attempt++)
            {
                float distance=1.5f-attempt*.25f;
                var origin=player.view.transform.position+forward*distance;
                int hits=Physics.RaycastNonAlloc(origin,Vector3.down,throwHits,4,~0,QueryTriggerInteraction.Ignore);
                if(hits==throwHits.Length)continue;
                float nearest=float.PositiveInfinity;RaycastHit floor=default;
                for(int i=0;i<hits;i++)if(throwHits[i].distance<nearest){nearest=throwHits[i].distance;floor=throwHits[i];}
                if(!floor.collider||floor.normal.y<.95f||floor.collider.GetComponentInParent<SortItem>()||floor.collider.GetComponentInParent<SortRack>()||floor.collider.transform.IsChildOf(player.transform)||floor.collider.attachedRigidbody)continue;
                var end=floor.point+Vector3.up*(extent.y+.025f);
                if(!ThrowClear(end,extent,item,true))continue;
                bool supported=true;
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                {
                    var corner=floor.point+new Vector3(x*extent.x,.08f,z*extent.z);
                    if(!Physics.Raycast(corner,Vector3.down,out var support,.12f,~0,QueryTriggerInteraction.Ignore)||support.normal.y<.95f||support.collider.GetComponentInParent<SortItem>()||support.collider.GetComponentInParent<SortRack>())supported=false;
                }
                if(!supported)continue;
                bool clear=true;var previous=start;
                // Same arc as SortItem.Travel; sweep full-size boxes between samples.
                for(int step=1;step<=16&&clear;step++)
                {
                    float fraction=step/16f;var point=Vector3.Lerp(start,end,fraction)+Vector3.up*(Mathf.Sin(fraction*Mathf.PI)*.12f);
                    var delta=point-previous;float length=delta.magnitude;
                    int count=Physics.BoxCastNonAlloc(previous,extent,delta.normalized,throwHits,Quaternion.identity,length,~0,QueryTriggerInteraction.Ignore);
                    clear=count<throwHits.Length&&ThrowClear(point,extent,item,false);
                    for(int i=0;i<count&&clear;i++)if(ThrowObstacle(throwHits[i].collider,item,false))clear=false;
                    previous=point;
                }
                if(clear){destination=end-offset;return true;}
            }
            return false;
        }
    }
}
