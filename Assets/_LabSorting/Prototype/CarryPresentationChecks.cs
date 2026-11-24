#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using LabSorting.Player;
using LabSorting.Sorting;
using LabSorting.UI;

namespace LabSorting.Prototype
{
    // Run in the real Lab scene in Play Mode; restores the session and editor layout.
    public static class CarryPresentationChecks
    {
        const string Folder="Logs/CarryReview";
        static void Log(string text){File.AppendAllText(Folder+"/checks.txt",text+"\n");Debug.Log("CARRY "+text);}
        static void Check(bool ok,string text){Log((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text);}
        static void Unblock(SortingSession s){s.player.SetNetworkBlocked(false);s.player.SetTutorialBlocked(false);s.player.SetBlocked(false);s.player.SetPickTutorialLocked(false);}
        static IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForSecondsRealtime(.2f);}
        static IEnumerator Settle(SortItem item){float end=Time.realtimeSinceStartup+8;while(item.Moving&&Time.realtimeSinceStartup<end)yield return null;Check(!item.Moving,"Motion completed: "+item.definition.displayName);yield return new WaitForEndOfFrame();}
        static void PoseCheck(SortItem item,CarryPresentation carry,string label)
        {
            var t=item.transform;var pose=carry.ResolveHeldPose(item);
            Check(Vector3.Distance(t.localPosition,pose.position)<.0001f&&Quaternion.Angle(t.localRotation,pose.rotation)<.05f&&Vector3.Distance(t.localScale,pose.scale)<.0001f,label+" uses final held pose");
            var rect=carry.HeldScreenRect;var safe=carry.HeldSafeRect;
            Check(rect.width>5&&rect.height>5&&rect.xMin>=safe.xMin-1&&rect.xMax<=safe.xMax+1&&rect.yMin>=safe.yMin-1&&rect.yMax<=safe.yMax+1,label+" visible inside HUD clearance "+rect);
            var b=item.visual.localBounds;
            for(int n=0;n<8;n++)
            {
                var p=item.visual.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1)));
                Check(carry.view.WorldToScreenPoint(p).z>carry.view.nearClipPlane,label+" corner "+n+" clears near plane");
            }
            Check(carry.OverlayActive,label+" held overlay active");
        }
        static IEnumerator Resize(int w,int h)
        {
            typeof(ThumbControlsChecks).GetMethod("GameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
            for(int n=0;n<10;n++)yield return null;
            Canvas.ForceUpdateCanvases();Check(Screen.width==w&&Screen.height==h,"Resolution "+w+"x"+h);
        }
        public static IEnumerator Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Carry checks require Play Mode");
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/checks.txt","");
            var saved=s.Capture();bool persistence=s.persistence;string saveName=s.saveName;
            var carry=s.hand.GetComponent<CarryPresentation>();var sizes=UnityEngine.Object.FindFirstObjectByType<ControlSizes>();float controls=sizes.ActionScale;
            var safePanel=UnityEngine.Object.FindFirstObjectByType<SortingHUD>().GetComponentInChildren<SafeAreaPanel>();
            s.persistence=false;Unblock(s);Application.runInBackground=true;
            GameObject wall=null;
            try
            {
                foreach(var name in new[]{"Disinfectant Spray","Test Tube","Wide Radio","White Water Tablets","Microscope Big"})
                {
                    Unblock(s);var item=s.items.First(i=>i.definition.displayName==name&&i.location==ItemLocation.Loose);
                    Check(s.TryPick(item),"Floor pickup "+name);
                    yield return Settle(item);PoseCheck(item,carry,name);
                    var p=item.transform.localPosition;var r=item.transform.localRotation;var scale=item.transform.localScale;
                    yield return null;yield return new WaitForEndOfFrame();
                    Check(Vector3.Distance(p,item.transform.localPosition)<.0001f&&Quaternion.Angle(r,item.transform.localRotation)<.05f&&Vector3.Distance(scale,item.transform.localScale)<.0001f,"Stable arrival "+name);
                    yield return Capture("after-"+name.Replace(" ","-"));
                }
                Unblock(s);s.Select(0);yield return new WaitForSeconds(.25f);yield return new WaitForEndOfFrame();PoseCheck(s.Active,carry,"Selection");
                s.Cycle();yield return new WaitForSeconds(.25f);yield return new WaitForEndOfFrame();PoseCheck(s.Active,carry,"Cycle");
                var first=s.items.First(i=>i.location==ItemLocation.Loose&&!i.Moving&&!i.definition.isRat);
                var second=s.items.First(i=>i!=first&&i.location==ItemLocation.Loose&&!i.Moving&&!i.definition.isRat);
                Check(s.TryPick(first)&&s.TryPick(second),"Rapid successive pickups");
                s.Select(0);yield return new WaitForSeconds(.25f);yield return new WaitForEndOfFrame();
                Check(!first.Moving&&!second.Moving&&!first.gameObject.activeSelf&&!second.gameObject.activeSelf,"Selection during flight settles and hides other items");PoseCheck(s.Active,carry,"Mid-flight selection");
                var magnet=s.items.First(i=>i.location==ItemLocation.Loose&&!i.Moving&&!i.definition.isRat);
                Check(s.TryPick(magnet,true,.1f),"Staggered magnet arrival");
                float finish=Time.time+.6f;while(Time.time<finish){s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw+.8f,s.player.look.Pitch));yield return null;}
                yield return Settle(magnet);PoseCheck(magnet,carry,"Magnet while turning");yield return Capture("magnet-turned");
                // On-disk roundtrip uses a workspace-only save slot, never the player's save.
                s.saveName=Path.GetFullPath(Folder+"/verification-save.json");s.persistence=true;s.Save();s.persistence=false;
                string active=s.Active.id;Check(s.Load()&&s.Active.id==active,"Disk save/load preserves selected item");yield return null;yield return new WaitForEndOfFrame();PoseCheck(s.Active,carry,"Loaded item");
                foreach(var width in new[]{1280,1440,1560,1600,1680,1152,960})
                {
                    yield return Resize(width,720);
                    foreach(float control in new[]{.85f,1.15f})
                    {
                        sizes.Apply(sizes.JoystickScale,control);
                        for(int index=0;index<5;index++)
                        {
                            Unblock(s);s.Select(index);s.Active.CompleteMotion();yield return null;yield return new WaitForEndOfFrame();
                            PoseCheck(s.Active,carry,"Layout "+width+" controls="+control+" "+s.Active.definition.displayName);
                        }
                    }
                    yield return Capture("layout-"+width+"x720");
                }
                Check(safePanel.ApplySample(new Rect(60,25,Screen.width-120,Screen.height-25),new Vector2Int(Screen.width,Screen.height)),"Gameplay HUD accepts simulated notch inset");Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();s.Active.RefreshHeldPresentation(carry);PoseCheck(s.Active,carry,"Simulated notch UI");yield return Capture("safe-area-simulated");safePanel.Apply();
                yield return Resize(1280,720);sizes.Apply(sizes.JoystickScale,controls);yield return null;
                // A temporary collider exercises the real guard without modifying scene assets.
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Carry verification wall";
                wall.transform.SetPositionAndRotation(carry.view.transform.position+carry.view.transform.forward*.55f,carry.view.transform.rotation);wall.transform.localScale=new Vector3(3,3,.1f);Physics.SyncTransforms();
                yield return null;yield return new WaitForEndOfFrame();Check(carry.ClearanceScale<1,"Wall causes retreat");PoseCheck(s.Active,carry,"Near wall");yield return Capture("near-wall");
                UnityEngine.Object.Destroy(wall);wall=null;yield return new WaitForSeconds(.5f);
                // Clear the hand through actual placement, then exercise a last-item drop.
                while(s.Active)
                {
                    Unblock(s);s.Active.CompleteMotion();var rack=s.racks.FirstOrDefault(r=>r.CanAccept(s.Active.definition));
                    Check(rack,"Compatible rack available");var placed=s.Active;Check(s.TryPlace(rack),"Single placement");yield return Settle(placed);
                }
                yield return null;Check(!carry.OverlayActive,"Last placement clears overlay");
                var loose=s.items.First(i=>i.location==ItemLocation.Loose&&!i.Moving&&!i.definition.isRat);Unblock(s);Check(s.TryPick(loose),"Pick for immediate drop");s.ThrowActive();Check(s.Active==null&&loose.location==ItemLocation.Loose,"Drop during pickup releases last item");yield return Settle(loose);yield return null;Check(!carry.OverlayActive,"Last drop clears overlay");
                var shelf=s.items.First(i=>i.location==ItemLocation.Rack&&!i.PickLocked&&!i.Moving);Unblock(s);Check(s.TryPick(shelf),"Rack pickup");yield return Settle(shelf);PoseCheck(shelf,carry,"Rack arrival");
                var matching=s.items.First(i=>i!=shelf&&i.definition==shelf.definition&&i.location==ItemLocation.Loose&&!i.Moving);Check(s.TryPick(matching),"Matching batch pickup");yield return Settle(matching);
                var destination=s.racks.First(r=>r.CanAccept(shelf.definition)&&r.CapacityFor(shelf.definition)-r.Count>=2);Check(s.PlaceMatching(destination),"Batch placement");yield return Settle(shelf);yield return Settle(matching);Check(shelf.rack==destination&&matching.rack==destination,"Both batch items arrived");
                Log("DONE");
            }
            finally
            {
                if(wall)UnityEngine.Object.Destroy(wall);foreach(var item in s.items)if(item.Moving||item.Presenting)item.CompleteMotion();
                safePanel.Apply();sizes.Apply(sizes.JoystickScale,controls);s.persistence=false;s.Restore(saved);s.saveName=saveName;s.persistence=persistence;
                typeof(ThumbControlsChecks).GetMethod("RestoreGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            }
        }
    }
}
#endif
