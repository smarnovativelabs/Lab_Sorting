using UnityEngine;
using DG.Tweening;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        public Vector3 DropVelocity()
        {
            var forward=Vector3.ProjectOnPlane(player.view.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.01f)forward=player.transform.forward;
            return forward*3f+Vector3.up*1.4f;
        }
        // Only choose a clear release close to the hand, never an artificial landing target.
        public bool TryPhysicsRelease(SortItem item,out Vector3 position)
        {
            position=default;
            if(!item||!(item.hitbox is BoxCollider box))return false;
            Physics.SyncTransforms();
            var rotation=item.transform.rotation;
            var scale=item.GroundScale;
            var offset=rotation*Vector3.Scale(box.center,scale);
            var size=Vector3.Scale(box.size,scale);
            var half=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z))*.5f+Vector3.one*.015f;
            var eye=player.view.transform.position;
            var forward=Vector3.ProjectOnPlane(player.view.transform.forward,Vector3.up).normalized;
            for(int attempt=0;attempt<9;attempt++)
            {
                var center=attempt==0?item.transform.position+offset:
                    eye+Vector3.up*((attempt-1)/3*.15f-.15f)-forward*((attempt-1)%3*.15f);
                // A hand presentation can be beyond a thin shelf or wall: keep release on the camera side.
                var delta=center-eye;bool clear=true;
                foreach(var hit in Physics.RaycastAll(eye,delta.normalized,delta.magnitude,~(1<<2),QueryTriggerInteraction.Ignore))
                    if(SmoothDropObstacle(hit.collider,item)){clear=false;break;}
                if(!clear)continue;
                int count=Physics.OverlapBoxNonAlloc(center,half,throwOverlaps,rotation,~(1<<2),QueryTriggerInteraction.Ignore);
                if(count==throwOverlaps.Length)continue;
                for(int i=0;i<count;i++)if(SmoothDropObstacle(throwOverlaps[i],item)){clear=false;break;}
                if(!clear)continue;
                position=center-offset;return true;
            }
            return false;
        }
    }
    public sealed partial class SortItem
    {
        internal void PhysicsWoke()=>PoseChanged();
        public void ReleaseNaturally(Vector3 position,Vector3 velocity,System.Action done)
        {
            SetSearchHighlight(false);selectionPresentation?.Kill();ClearShine();motion?.Kill();
            DropLandingPool.Stop(this,false);DropLandingPool.Unreserve(this);physicsLanding=false;
            ImpactFeedbackPlayed=false;Highlight(false);SetHeldPresentation(false);
            transform.localScale=GroundScale;transform.position=position;hitbox.enabled=true;
            var natural=NaturalItemSettling.Fit(this);
            natural.Begin(done,velocity);
            PoseChanged();
        }
    }
}
