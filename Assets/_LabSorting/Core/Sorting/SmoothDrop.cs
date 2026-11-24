using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        // Fixed heading and launch speed: camera pitch never changes the throw.
        // Sweep the released footprint, so even large products stop at walls and shelves.
        public Vector3[] PlanSmoothDrop(SortItem item,out Vector3 destination)
        {
            Physics.SyncTransforms();
            var bounds=ReleaseBounds(item);var half=bounds.extents+Vector3.one*.008f;
            var offset=bounds.center-item.transform.position;
            var eye=player.view.transform.position;
            var forward=Vector3.ProjectOnPlane(player.view.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.01f)forward=player.transform.forward;
            var position=eye-Vector3.up*.15f;
            // Keep the launch on the player's side of nearby geometry.
            if(!SmoothDropClear(position,half,item))
            {
                for(int i=1;i<=12;i++)
                {
                    var probe=eye-forward*(i*.05f);
                    if(SmoothDropClear(probe,half,item)){position=probe;break;}
                }
            }
            var path=new List<Vector3>(100){position-offset};
            var velocity=forward*4.2f+Vector3.up*3.4f;
            const float dt=1f/30f;
            bool landed=false;
            for(int step=0;step<90;step++)
            {
                velocity.y-=14f*dt;
                var delta=velocity*dt;float distance=delta.magnitude;
                int count=Physics.BoxCastNonAlloc(position,half,delta.normalized,throwHits,Quaternion.identity,distance,~(1<<2),QueryTriggerInteraction.Ignore);
                float nearest=float.PositiveInfinity;RaycastHit contact=default;
                for(int i=0;i<count;i++)
                {
                    var hit=throwHits[i];
                    if(!SmoothDropObstacle(hit.collider,item)||hit.distance<0)continue;
                    if(hit.distance<nearest){nearest=hit.distance;contact=hit;}
                }
                if(contact.collider)
                {
                    position+=delta.normalized*Mathf.Max(0,nearest-.004f);
                    path.Add(position-offset);
                    var rack=contact.collider.GetComponentInParent<SortRack>();
                    if(contact.normal.y>=.65f&&!rack&&StackDepth(position,item)<3&&SmoothDropClear(position,half,item)&&!DropLandingPool.Reserved(new Bounds(position,half*2),item))
                    {landed=true;break;}
                    var away=Vector3.ProjectOnPlane(contact.normal,Vector3.up);
                    if(!rack&&contact.normal.y>.65f)away=Vector3.Cross(forward,Vector3.up);
                    if(rack&&away.sqrMagnitude<.01f)away=-forward;
                    velocity=away.normalized*1.1f+Vector3.up*Mathf.Min(velocity.y,0);
                    if(contact.normal.y<-.5f)velocity.y=-Mathf.Abs(velocity.y);
                    position+=contact.normal*.006f;
                }
                else {position+=delta;path.Add(position-offset);}
                if(position.y<player.transform.position.y-3)break;
            }
            if(!landed)
            {
                // Find a supported nearby footprint; item tops count, rack shelves do not.
                var preferred=position;bool found=false;
                float spacing=Mathf.Max(.12f,Mathf.Max(half.x,half.z)*1.1f);
                for(int probe=0;probe<65&&!found;probe++)
                {
                    int ring=(probe+7)/8;float angle=probe*137.5f*Mathf.Deg2Rad;
                    var origin=preferred+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*spacing;
                    origin.y=Mathf.Max(eye.y,preferred.y)+half.y+.2f;
                    if(!SmoothDropClear(origin,half,item))continue;
                    int count=Physics.BoxCastNonAlloc(origin,half,Vector3.down,throwHits,Quaternion.identity,6,~(1<<2),QueryTriggerInteraction.Ignore);
                    float nearest=float.PositiveInfinity;RaycastHit contact=default;
                    for(int i=0;i<count;i++)if(SmoothDropObstacle(throwHits[i].collider,item)&&throwHits[i].distance<nearest){nearest=throwHits[i].distance;contact=throwHits[i];}
                    if(!contact.collider||contact.normal.y<.65f||contact.collider.GetComponentInParent<SortRack>())continue;
                    var candidate=origin+Vector3.down*Mathf.Max(0,nearest-.006f);
                    if(StackDepth(candidate,item)>=3||!SmoothDropClear(candidate,half,item)||DropLandingPool.Reserved(new Bounds(candidate,half*2),item))continue;
                    // Recovery is part of the visible throw too; never slide through a neighbouring product.
                    var recovery=candidate-position;
                    bool clear=true;
                    if(recovery.sqrMagnitude>.000001f)
                    {
                        int crossed=Physics.BoxCastNonAlloc(position,half,recovery.normalized,throwHits,Quaternion.identity,recovery.magnitude,~(1<<2),QueryTriggerInteraction.Ignore);
                        if(crossed==throwHits.Length)clear=false;
                        for(int h=0;h<crossed;h++)if(SmoothDropObstacle(throwHits[h].collider,item))clear=false;
                    }
                    if(!clear)continue;
                    position=candidate;found=true;
                }
                if(!found)position=FindCrowdedDrop(item)+offset;
                // A blocked throw settles along a short recovery rather than snapping on completion.
                var start=path[path.Count-1];var end=position-offset;
                int steps=Mathf.Max(3,Mathf.CeilToInt(Vector3.Distance(start,end)/.12f));
                for(int i=1;i<=steps;i++)path.Add(Vector3.Lerp(start,end,i/(float)steps));
            }
            destination=position-offset;
            return path.ToArray();
        }
        bool SmoothDropObstacle(Collider collider,SortItem item)=>collider&&!collider.transform.IsChildOf(item.transform)&&!collider.transform.IsChildOf(player.transform);
        int StackDepth(Vector3 center,SortItem item)
        {
            var seen=new HashSet<SortItem>();
            foreach(var hit in Physics.RaycastAll(center,Vector3.down,3,~(1<<2),QueryTriggerInteraction.Ignore))
            {var support=hit.collider.GetComponentInParent<SortItem>();if(support&&support!=item)seen.Add(support);}
            return seen.Count;
        }
        bool SmoothDropClear(Vector3 center,Vector3 half,SortItem item)
        {
            int count=Physics.OverlapBoxNonAlloc(center,half,throwOverlaps,Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==throwOverlaps.Length)return false;
            for(int i=0;i<count;i++)if(SmoothDropObstacle(throwOverlaps[i],item))return false;
            return true;
        }
    }
    public sealed partial class SortItem
    {
        public void FollowDropArc(Vector3[] path,Vector3 destination,Action done)
        {
            SetSearchHighlight(false);selectionPresentation?.Kill();ClearShine();motion?.Kill();
            DropLandingPool.Stop(this,false);physicsLanding=false;ImpactFeedbackPlayed=false;
            motionDestination=destination;motionRotation=Quaternion.identity;hitbox.enabled=false;Highlight(false);
            var scale=transform.localScale;var rotation=transform.rotation;var handPosition=transform.position;
            NaturalItemSettling.Suspend(this);
            // Fit nearby supports before the last settling phase; distant clutter retains its inexpensive box collider.
            foreach(var support in Physics.OverlapSphere(destination,.8f,~(1<<2),QueryTriggerInteraction.Ignore))
            {var other=support.GetComponentInParent<SortItem>();if(other&&other!=this&&other.location==ItemLocation.Loose&&!other.Moving)NaturalItemSettling.Fit(other);}
            var box=hitbox as BoxCollider;
            var footprint=new Bounds(destination+Vector3.Scale(box.center,originalScale),Vector3.Scale(box.size,originalScale));
            DropLandingPool.Reserve(this,footprint);
            int segments=path.Length-1;float duration=Mathf.Max(.1f,segments/30f);
            motion=DOTween.To(()=>0f,t=>
            {
                float index=t*segments;int segment=Mathf.Min((int)index,segments-1);
                var flightPosition=Vector3.Lerp(path[segment],path[segment+1],index-segment);
                transform.position=Vector3.Lerp(handPosition,flightPosition,Mathf.SmoothStep(0,1,Mathf.Clamp01(t*duration/.16f)));
                float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(t*duration/.15f));
                transform.localScale=Vector3.Lerp(scale,originalScale,blend);
                transform.rotation=Quaternion.Slerp(rotation,Quaternion.identity,blend);
            },1f,duration).SetEase(Ease.Linear).OnComplete(()=>
            {
                var natural=NaturalItemSettling.Fit(this);
                if(natural)natural.Begin(done);else FinishPhysicsLanding(destination,Quaternion.identity,done);
            });
            PoseChanged();
        }
    }
}
