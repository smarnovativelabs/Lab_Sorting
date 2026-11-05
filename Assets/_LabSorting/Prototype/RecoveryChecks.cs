using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class RecoveryChecks
    {
        static IEnumerator Wait(float seconds){float end=Time.unscaledTime+seconds,deadline=Time.realtimeSinceStartup+120;while(Time.unscaledTime<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Recovery test frame clock stopped");yield return null;}}
        public static IEnumerator Run(SortingSession s,Action<bool,string> check)
        {
            var recovery=s.GetComponent<ItemRecovery>();var initial=s.Capture();var a=s.items[0];var b=s.items[1];var points=recovery.safePoints;bool auto=recovery.automatic,persist=s.persistence;float grace=recovery.graceSeconds,interval=recovery.scanInterval;int budget=recovery.itemsPerScan;
            GameObject blocker=null,voidPoint=null;
            try
            {
                s.persistence=false;recovery.automatic=false;check(recovery.Configured,"Recovery bounds and points wired");int cash=s.Cash,sorted=s.SortedCount,milestones=s.MilestoneCount;var ids=s.items.Length;
                a.Snap(new Vector3(40,.2f,0),Quaternion.identity);b.Snap(new Vector3(0,-2,0),Quaternion.identity);check(recovery.RecoverLostItems()==2,"Outside and below-floor loose items recover");
                check(recovery.Contains(a.transform.position)&&recovery.Contains(b.transform.position)&&a.hitbox.enabled&&b.hitbox.enabled,"Recovered items are inside bounds with active colliders");check(Vector3.Distance(a.transform.position,b.transform.position)>.2f,"Multiple recovered items use separate unoccupied points");check(s.Cash==cash&&s.SortedCount==sorted&&s.MilestoneCount==milestones&&s.items.Length==ids,"Recovery preserves cash, progress, milestones and item totals");check(recovery.RecoverLostItems()==0,"Valid recovered items do not repeatedly recover");
                check(s.TryPick(a),"Recovered item remains pickable");DOTween.CompleteAll();a.transform.position=new Vector3(100,-100,0);check(recovery.RecoverLostItems()==0&&a.location==ItemLocation.Hand&&s.Active==a,"Recovery never teleports carried items");s.Restore(initial);
                s.TryPick(a);DOTween.CompleteAll();s.TryPlace(s.racks[0]);DOTween.CompleteAll();a.transform.position=new Vector3(100,-100,0);check(recovery.RecoverLostItems()==0&&a.location==ItemLocation.Rack&&s.racks[0].Count==1,"Recovery never changes rack ownership");s.Restore(initial);
                a.Travel(new Vector3(40,.2f,0),Quaternion.identity);check(recovery.RecoverLostItems()==0,"In-flight items are excluded from recovery");DOTween.CompleteAll();check(recovery.RecoverLostItems()==1,"Invalid item recovers once its animation finishes");s.Restore(initial);
                blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Recovery wall fixture";blocker.transform.position=a.transform.position;blocker.transform.localScale=Vector3.one;Physics.SyncTransforms();check(recovery.NeedsRecovery(a),"Deep static geometry penetration is detected");check(recovery.RecoverLostItems()>=1&&!recovery.NeedsRecovery(a),"Embedded item returns to a safe point");blocker.SetActive(false);s.Restore(initial);
                a.Snap(new Vector3(40,.2f,0),Quaternion.identity);recovery.graceSeconds=.2f;check(recovery.RecoverLostItems(true)==0,"Transient invalid position waits for grace period");yield return Wait(.3f);check(recovery.RecoverLostItems(true)==1,"Persistent invalid position recovers after grace");s.Restore(initial);
                voidPoint=new GameObject("Recovery void fixture");voidPoint.transform.position=new Vector3(100,1,100);recovery.safePoints=new[]{voidPoint.transform};a.Snap(new Vector3(40,.2f,0),Quaternion.identity);var lost=a.transform.position;check(recovery.RecoverLostItems()==0&&a.transform.position==lost,"No safe floor means no unsafe teleport");recovery.safePoints=points;check(recovery.RecoverLostItems()==1,"Recovery retries successfully when safe points become available");s.Restore(initial);
                recovery.safePoints=new[]{points[0]};blocker.transform.position=new Vector3(points[0].position.x,.2f,points[0].position.z);blocker.transform.localScale=new Vector3(.5f,.4f,.5f);blocker.AddComponent<Rigidbody>().isKinematic=true;blocker.SetActive(true);Physics.SyncTransforms();a.Snap(new Vector3(40,.2f,0),Quaternion.identity);check(recovery.RecoverLostItems()==0,"Occupied recovery point is never used");blocker.SetActive(false);check(recovery.RecoverLostItems()==1,"Clearing occupied point allows retry");recovery.safePoints=points;s.Restore(initial);
                a.Snap(new Vector3(40,.2f,0),Quaternion.identity);recovery.playableBounds.enabled=false;check(recovery.RecoverLostItems()==0&&a.transform.position.x==40,"Invalid recovery configuration leaves items untouched");recovery.playableBounds.enabled=true;recovery.RecoverLostItems();
                var recovered=a.transform.position;s.persistence=true;s.Save();s.Restore(initial);check(s.Load()&&Vector3.Distance(a.transform.position,recovered)<.001f&&s.Cash==cash,"Recovered position persists through disk save/load without cash change");s.persistence=false;s.Restore(initial);
                recovery.scanInterval=.1f;recovery.graceSeconds=.1f;recovery.itemsPerScan=s.items.Length;recovery.automatic=true;a.Snap(new Vector3(40,.2f,0),Quaternion.identity);yield return Wait(.6f);check(recovery.Contains(a.transform.position)&&a.location==ItemLocation.Loose,"Automatic periodic recovery repairs an invalid loose item");
            }
            finally{recovery.automatic=auto;recovery.graceSeconds=grace;recovery.scanInterval=interval;recovery.itemsPerScan=budget;recovery.safePoints=points;recovery.playableBounds.enabled=true;if(blocker)UnityEngine.Object.Destroy(blocker);if(voidPoint)UnityEngine.Object.Destroy(voidPoint);s.Restore(initial);s.persistence=persist;}
        }
    }
}



