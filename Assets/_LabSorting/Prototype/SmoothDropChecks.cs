#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class SmoothDropChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("SMOOTH DROP: "+message);Debug.Log("SMOOTH DROP PASS: "+message);}
        public static IEnumerator Run(SortingSession s)
        {
            var saved=s.Capture();bool motor=s.player.character.Motor.enabled;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Drop test floor";floor.transform.position=new Vector3(1000,-.1f,1000);floor.transform.localScale=new Vector3(20,.2f,20);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.SetActive(false);
            var support=GameObject.CreatePrimitive(PrimitiveType.Cube);support.SetActive(false);
            try
            {
                s.player.Clear();s.player.SetBlocked(false);s.player.character.Motor.SetPosition(new Vector3(1000,.01f,1000));s.player.look.SetYawPitch(Vector2.zero);
                yield return new WaitForSecondsRealtime(.4f);s.player.character.Motor.enabled=false;
                var item=s.items.First(i=>i.location==ItemLocation.Loose&&i.definition.displayName.Contains("Yellow"));
                Check(s.TryPick(item),"Pick yellow item for drop checks");item.CompleteMotion();
                var path=s.PlanSmoothDrop(item,out var end);
                Check(path.Max(p=>p.y)>path[0].y+.15f,"Throw rises along a curved arc");
                Check(end.z>s.player.transform.position.z+.5f,"Open throw lands ahead");
                s.player.look.SetYawPitch(new Vector2(0,35));yield return null;
                s.PlanSmoothDrop(item,out var pitched);
                Check(Vector3.Distance(end,pitched)<.08f,"Looking down preserves throw heading and landing");
                s.player.look.SetYawPitch(Vector2.zero);yield return null;
                // An item-sized supported pile at the normal landing location exercises top-surface contact.
                support.transform.position=new Vector3(end.x,.2f,end.z);support.transform.localScale=new Vector3(2,.4f,2);support.SetActive(true);Physics.SyncTransforms();
                s.PlanSmoothDrop(item,out var stacked);
                Check(stacked.y>end.y+.3f,"Throw rests on an existing raised surface");
                s.ThrowActive();float until=Time.realtimeSinceStartup+35;while(item.Moving&&Time.realtimeSinceStartup<until)yield return null;
                Check(!item.Moving&&item.hitbox.enabled,"Stacked landing completes and restores pickup collider");
                Check(item.hitbox.bounds.min.y>=support.GetComponent<Collider>().bounds.max.y-.01f,"Landed item does not intersect support");
                Check(s.TryPick(item),"Stacked item can be picked again");item.CompleteMotion();support.SetActive(false);
                wall.transform.position=new Vector3(1000,2,1001);wall.transform.localScale=new Vector3(10,4,.15f);wall.SetActive(true);Physics.SyncTransforms();
                var blocked=s.PlanSmoothDrop(item,out var blockedEnd);
                Check(blocked.All(p=>p.z<wall.GetComponent<Collider>().bounds.min.z),"Wall stops the entire throw path");
                s.ThrowActive();until=Time.realtimeSinceStartup+35;while(item.Moving&&Time.realtimeSinceStartup<until)yield return null;
                Check(!item.Moving&&item.hitbox.enabled&&item.transform.position.z<1001,"Wall impact drops to the near side; moving="+item.Moving+", collider="+item.hitbox.enabled+", position="+item.transform.position+", planned="+blockedEnd+", samples="+blocked.Length);
                Check(!ItemSupport.Floating(item),"Wall impact rests on a supported surface");
                wall.SetActive(false);Check(s.TryPick(item),"Pick after wall impact");item.CompleteMotion();
                s.player.look.SetYawPitch(Vector2.zero);yield return new WaitForSecondsRealtime(.2f);
                s.PlanSmoothDrop(item,out var bare);
                var baseItem=s.items.Where(i=>i!=item&&i.location==ItemLocation.Loose&&!i.definition.isRat)
                    .OrderByDescending(i=>i.hitbox.bounds.size.x*i.hitbox.bounds.size.z).First();
                baseItem.Snap(Vector3.zero,Quaternion.identity);Physics.SyncTransforms();
                var box=baseItem.hitbox.bounds;
                baseItem.Snap(new Vector3(bare.x-box.center.x,-box.min.y+.003f,bare.z-box.center.z),Quaternion.identity);Physics.SyncTransforms();
                s.PlanSmoothDrop(item,out var pileEnd);
                s.ThrowActive();until=Time.realtimeSinceStartup+35;while(item.Moving&&Time.realtimeSinceStartup<until)yield return null;
                Check(!item.Moving&&item.hitbox.bounds.min.y>=baseItem.hitbox.bounds.max.y-.02f,"Real product lands on top of another product");
                Check(!Physics.ComputePenetration(item.hitbox,item.transform.position,item.transform.rotation,baseItem.hitbox,baseItem.transform.position,baseItem.transform.rotation,out _,out var penetration)||penetration<.005f,"Stacked products do not intersect");
                for(int pass=0;pass<3;pass++){s.player.look.SnapToDirection((item.hitbox.bounds.center+baseItem.hitbox.bounds.center)*.5f-s.player.view.transform.position);yield return new WaitForSecondsRealtime(.2f);}
                ScreenCapture.CaptureScreenshot("Logs/VisibilityReview/06-product-stacking.png");yield return new WaitForSecondsRealtime(.3f);
                var topBefore=item.transform.position;Check(s.TryPick(baseItem),"Pick the supporting product");baseItem.CompleteMotion();
                yield return new WaitForSecondsRealtime(2);
                Check(item.transform.position.y<topBefore.y-.02f,"Removing support lowers the item above");
                Debug.Log("SMOOTH_DROP_OK");
            }
            finally
            {
                DG.Tweening.DOTween.CompleteAll();floor.SetActive(false);wall.SetActive(false);support.SetActive(false);
                UnityEngine.Object.Destroy(floor);UnityEngine.Object.Destroy(wall);UnityEngine.Object.Destroy(support);
                s.player.character.Motor.enabled=motor;s.Restore(saved);
            }
        }
    }
}
#endif
