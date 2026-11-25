#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class VisibilityFeedbackChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("VISIBILITY: "+message);Debug.Log("VISIBILITY PASS: "+message);}
        static IEnumerator Capture(string name){ShotGate.Capture("Logs/VisibilityReview/"+name+".png");yield return new WaitForSecondsRealtime(.3f);}
        static IEnumerator Aim(SortingSession s,SortItem item)
        {
            // Re-aim after the camera rig follows the rotated target, using its updated eye position.
            for(int pass=0;pass<3;pass++)
            {
                s.player.look.SnapToDirection(item.hitbox.bounds.center-s.player.view.transform.position);
                yield return new WaitForSecondsRealtime(.2f);
            }
            s.RefreshTarget();yield return null;
        }
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            s.player.SendMessage("OnApplicationFocus",true);s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);
            var saved=s.Capture();Directory.CreateDirectory("Logs/VisibilityReview");
            var locator=UnityEngine.Object.FindFirstObjectByType<ItemLocatorHUD>();
            var sizes=UnityEngine.Object.FindFirstObjectByType<ControlSizes>();float oldSize=sizes.ActionScale;
            try
            {
                sizes.Apply(sizes.JoystickScale,1);
                Cursor.lockState=CursorLockMode.None;
                yield return new WaitForSecondsRealtime(.3f);
                var eye=s.player.view.transform.position;
                var candidates=s.items.Where(i=>i.definition.displayName.Contains("Yellow")&&i.location==ItemLocation.Loose)
                    .OrderBy(i=>(i.hitbox.bounds.center-eye).sqrMagnitude).Where(i=>
                    {
                        var d=i.hitbox.bounds.center-eye;
                        return d.magnitude<s.reach&&Vector3.Angle(Vector3.ProjectOnPlane(d,Vector3.up),d)<55
                            &&Physics.Raycast(eye,d.normalized,out var hit,d.magnitude+.02f,~(1<<2),QueryTriggerInteraction.Ignore)
                            &&hit.collider.GetComponentInParent<SortItem>()==i;
                    }).ToArray();
                SortItem item=null;
                foreach(var candidate in candidates)
                {
                    yield return Aim(s,candidate);
                    if(s.TargetItem&&s.TargetItem.definition.displayName.Contains("Yellow")){item=s.TargetItem;break;}
                    Physics.Raycast(s.player.view.transform.position,s.player.view.transform.forward,out var hit,s.reach,~(1<<2),QueryTriggerInteraction.Ignore);
                    Debug.Log("VISIBILITY AIM: "+candidate.name+" direct="+(hit.collider?hit.collider.name:"none")+" angle="+Vector3.Angle(s.player.view.transform.forward,candidate.hitbox.bounds.center-s.player.view.transform.position)+" target="+(s.TargetItem?s.TargetItem.name:"none"));
                }
                Check(item,"Visible yellow item targetable from the normal spawn; candidates="+candidates.Length);
                yield return Capture("00-aim-review");
                Check(s.TargetItem==item&&item.Hovered,"Yellow object can be aimed and hovered; target="+(s.TargetItem?s.TargetItem.name:"none")+", blocked="+s.player.blocked+", moving="+item.Moving+", distance="+Vector3.Distance(item.hitbox.bounds.center,s.player.view.transform.position));
                Check(item.outline.IsHighlighted,"Hover renderer visible");
                Check(item.GetComponentsInChildren<Renderer>(true).Any(r=>r.name=="__QuickOutlineContrast"&&r.enabled),"Dark border rendered around yellow hover");
                yield return Capture("01-yellow-hover");
                var held=s.items.First(i=>i!=item&&i.definition==item.definition&&i.location==ItemLocation.Loose);
                var resting=item.transform.position;var restingBounds=item.hitbox.bounds;
                Check(s.TryPick(held),"Pick matching item");held.CompleteMotion();
                Check(s.TrySearch(),"Search starts normally");
                yield return new WaitForSecondsRealtime(1);
                Check(Vector3.Distance(item.transform.localScale,item.originalScale*1.2f)<.001f,"Search enlarges object exactly 20 percent");
                Check(Mathf.Abs(item.transform.position.y-resting.y-.125f)<.002f,"Search lifts object 12.5 cm");
                Check(Vector3.Distance(item.SavedPosition,resting)<.002f,"Save retains unlifted world position");
                Check(item.hitbox.bounds.size.magnitude>restingBounds.size.magnitude*1.19f,"Pickup collider follows enlarged presentation");
                yield return Aim(s,item);
                Check(item.Searched&&item.Hovered,"Hover preserves search state");
                Check(item.outline.OutlineColor==SortItem.HoverColor,"Hover color takes precedence over search");
                Check(locator.beacons.Any(b=>b.gameObject.activeInHierarchy&&b.transform.Find("SearchMarker")),"Magnifier beacon remains visible during hover");
                yield return Capture("02-search-and-hover");
                s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw+14,s.player.look.Pitch));
                yield return new WaitForSecondsRealtime(.3f);
                Check(item.Searched&&!item.Hovered,"Leaving hover keeps search active");
                Check(item.outline.IsHighlighted,"Search outline remains visible");
                var rim=item.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="__QuickOutlineURP");
                Check(rim.sharedMaterial.GetFloat("_PulseAmount")>0,"Search uses gentle shader pulse");
                yield return Capture("03-yellow-search");
                Check(s.TryPick(item),"Search result can still be collected");item.CompleteMotion();
                Check(!item.Searched&&!item.Hovered,"Collection clears both world highlight states");
                typeof(SortingSession).GetMethod("EndSearch",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(s,null);
                Check(s.items.All(i=>!i.Searched),"Search expiry clears all presentations");
                var path=s.PlanSmoothDrop(item,out var landing);
                s.ThrowActive();
                yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("04-drop-flight");
                float until=Time.realtimeSinceStartup+35;
                while(item.Moving&&Time.realtimeSinceStartup<until)yield return null;
                Check(!item.Moving&&item.hitbox.enabled,"Drop lands and is collectible");
                Check(!ItemSupport.Floating(item),"Gameplay drop has support below it");
                yield return Aim(s,item);
                yield return Capture("05-drop-landed");
                yield return CurrentGameplayChecks.NaturalDrop();
                Debug.Log("VISIBILITY_FEEDBACK_OK");
            }
            finally{DG.Tweening.DOTween.CompleteAll();sizes.Apply(sizes.JoystickScale,oldSize);s.Restore(saved);}
        }
    }
}
#endif
