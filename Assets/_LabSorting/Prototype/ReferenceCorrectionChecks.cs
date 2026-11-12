using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class ReferenceCorrectionChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("REFERENCE FIX: "+message);Debug.Log("REFERENCE PASS: "+message);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var initial=s.Capture();var originals=s.items;var child=new GameObject("Extra item collider test");var unique=ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                int supported=0;var hits=new RaycastHit[32];
                foreach(var item in s.items)
                {
                    var b=item.hitbox.bounds;int n=Physics.RaycastNonAlloc(new Vector3(b.center.x,b.min.y+.02f,b.center.z),Vector3.down,hits,.09f,~(1<<2),QueryTriggerInteraction.Ignore);
                    if(hits.Take(n).Any(h=>!h.collider.GetComponentInParent<SortItem>()&&!h.collider.GetComponentInParent<SortRack>()&&h.normal.y>.95f))supported++;
                }
                Check(supported==4032,"All 4032 item bottoms are supported directly by floor");Check(s.layoutRevision==2,"New continuous layout revision active");
                s.player.SendMessage("OnApplicationFocus",true);s.player.Clear();
                // Let the authored spawn settle before measuring floor height.
                for(int frame=0;frame<60&&!s.player.character.Motor.GroundingStatus.IsStableOnGround;frame++)yield return new WaitForFixedUpdate();
                Check(s.player.character.Motor.GroundingStatus.IsStableOnGround,"Player settles on stable floor before movement; pos="+s.player.transform.position+", velocity="+s.player.character.Motor.Velocity);
                Check((s.player.character.Motor.CollidableLayers&(1<<LayerMask.NameToLayer("SortingItem")))==0,"Movement queries exclude dense item layer");
                float floorY=s.player.transform.position.y;s.player.SetMove(Vector2.up);yield return new WaitForSeconds(.7f);s.player.Clear();yield return new WaitForSeconds(.3f);var stopped=s.player.transform.position;yield return new WaitForSeconds(.2f);
                float drift=Vector3.Distance(stopped,s.player.transform.position),heightChange=Mathf.Abs(s.player.transform.position.y-floorY);
                Check(drift<.03f&&heightChange<.08f,"Walking through spread stays on floor and stops without sliding; drift="+drift+", height change="+heightChange);
                var eye=s.player.view.transform.position;
                foreach(var candidate in s.items.OrderBy(i=>(i.hitbox.bounds.center-eye).sqrMagnitude))
                {
                    var delta=candidate.hitbox.bounds.center-eye;if(delta.magnitude>s.reach)break;
                    if(Vector3.Angle(Vector3.ProjectOnPlane(delta,Vector3.up),delta)>55)continue; // Respect the player's look-pitch clamp.
                    if(Physics.Raycast(eye,delta.normalized,out var hit,delta.magnitude+.01f,~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<SortItem>())
                    {s.player.look.SnapToDirection(delta);break;}
                }
                yield return null;yield return null;s.RefreshTarget();yield return null;
                Check(s.TargetItem&&s.TargetItem.outline.IsHighlighted,"Real aimed item renders its cyan hover outline");ScreenCapture.CaptureScreenshot("Logs/ContinuousHover.png");yield return new WaitForSeconds(.1f);
                var item0=s.items[0];child.transform.SetParent(item0.transform,false);var collider=child.AddComponent<BoxCollider>();Check(!s.player.character.IsColliderValidForCollisions(collider),"Unregistered child item collider cannot affect locomotion");collider.enabled=false;
                item0.Highlight(true);Check(item0.outline.enabled&&item0.outline.OutlineWidth==4&&item0.outline.OutlineColor.g>.9f,"Hover has strong cyan rim");
                var scale=item0.transform.localScale;item0.SetBatchHighlight(false);item0.SetSearchHighlight(true);item0.Highlight(false);Check(item0.outline.OutlineColor.r>.9f&&item0.outline.OutlineColor.b<.3f&&item0.transform.localScale==scale,"Search gold is distinct without scaling colliders");item0.SetSearchHighlight(false);
                int cash=s.Cash;Check(!s.TryMagnet()&&s.MagnetSeconds>0&&s.Cash==cash,"Empty hand starts feedback timer without charge");
                Check(!s.TryMagnet()&&s.Cash==cash,"Timer prevents repeated empty scans/charges");yield return new WaitForSeconds(1.3f);
                Check(s.TryPick(item0),"Pick for magnet feedback");DOTween.CompleteAll();var definition=item0.definition;unique.id="empty-magnet-test";unique.displayName="Test type";item0.definition=unique;
                Check(!s.TryMagnet()&&s.MagnetStatus=="NO MATCH"&&s.MagnetSeconds>0&&s.Cash==cash,"No matching loose items gives timed no-charge feedback");item0.definition=definition;
                yield return new WaitForSeconds(1.3f);Check(s.TrySearch(),"Search still works across full layout");Check(s.TryMagnet()&&s.MagnetSeconds>0,"Successful magnet has visible cooldown");DOTween.CompleteAll();yield return new WaitForSeconds(.1f);
                var economy=UnityEngine.Object.FindFirstObjectByType<EconomyHUD>();Check((economy.compact?economy.magnetLabel.text.Contains("s"):economy.magnetLabel.text.Contains("COLLECTED")),"Magnet countdown/result reaches HUD");
                ScreenCapture.CaptureScreenshot("Logs/ContinuousSpreadHUD.png");yield return new WaitForSeconds(.15f);
                Debug.Log("REFERENCE_CORRECTIONS_OK");
            }
            finally{DOTween.CompleteAll();s.items=originals;s.Restore(initial);s.InitializeSaveCache();UnityEngine.Object.Destroy(child);UnityEngine.Object.Destroy(unique);}
        }
    }
}

