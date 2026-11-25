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
    public static class RackGuidanceChecks
    {
        const string Folder="Logs/RackPinReview";
        static void Check(bool ok,string text){File.AppendAllText(Folder+"/checks.txt",(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception("RACK PIN: "+text);}
        static void Count(SortRack rack,int count)=>typeof(SortRack).GetField("<Count>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(rack,count);
        static IEnumerator Wait(){yield return new WaitForSecondsRealtime(.55f);yield return new WaitForEndOfFrame();}
        static IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForSecondsRealtime(.15f);}
        static IEnumerator LookAway(SortingSession s)
        {
            s.player.look.SnapToDirection(Vector3.up+Vector3.forward*.1f);yield return Wait();s.RefreshTarget();
        }
        static IEnumerator Aim(SortingSession s,SortRack rack,float distance=2.4f)
        {
            var p=rack.transform.position-rack.transform.forward*distance;p.y=rack.transform.position.y+.03f;s.player.character.Motor.SetPosition(p);
            yield return new WaitForSecondsRealtime(.2f);
            for(int n=0;n<4;n++){s.player.look.SnapToDirection(rack.transform.position+Vector3.up*1.05f-s.player.view.transform.position);yield return null;}
            s.RefreshTarget();yield return Wait();
        }
        static void Presentation(MatchingRackGuide guide,SortingSession s,string label)
        {
            Check(!guide.label.gameObject.activeSelf&&guide.label.text.Length==0,label+": no caption");
            Check(guide.MarkerVisible,label+": marker visible");
            var rect=guide.MarkerScreenRect;
            Check(rect.width>0&&rect.height>0&&Screen.safeArea.Contains(rect.min)&&Screen.safeArea.Contains(rect.max),label+": marker inside screen");
            var carry=s.hand.GetComponent<LabSorting.Player.CarryPresentation>();
            Check(!rect.Overlaps(carry.HeldScreenRect),label+": held item unobstructed");
            if(guide.MarkerIsPin)
            {
                var pin=UnityEngine.Object.FindFirstObjectByType<RackLocationPin>();
                var mesh=pin&&pin.canvasRenderer?pin.canvasRenderer.GetMesh():null;
                Check(mesh&&mesh.vertexCount>0,label+": pin has rendered mesh");
                var screen=s.player.view.WorldToScreenPoint(guide.MarkerWorldPoint);
                Check(Vector2.Distance(new Vector2(rect.center.x,rect.yMin),screen)<2,label+": pin tip anchored to rack world point");
            }
        }
        public static IEnumerator Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/checks.txt","");
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var saved=s.Capture();var all=s.racks;bool persistence=s.persistence;s.persistence=false;
            var gates=UnityEngine.Object.FindObjectsByType<ServicePresentation>(FindObjectsSortMode.None);var gateStates=gates.Select(g=>g.enabled).ToArray();foreach(var gate in gates)gate.enabled=false;
            var sizes=UnityEngine.Object.FindFirstObjectByType<ControlSizes>();float size=sizes.ActionScale;
            var safe=UnityEngine.Object.FindFirstObjectByType<SortingHUD>().GetComponentInChildren<SafeAreaPanel>();bool safeEnabled=safe.enabled;
            GameObject obstacle=null,unreachable=null;
            try
            {
                s.player.SetNetworkBlocked(false);s.player.SetTutorialBlocked(false);s.player.SetBlocked(false);s.player.SetPickTutorialLocked(false);
                var held=s.items.First(i=>i.definition.displayName=="Microscope Big"&&i.location==ItemLocation.Loose);Check(s.TryPick(held),"Pickup");held.CompleteMotion();
                var routes=LabRackRoutes.For(s);Check(routes.Ready,"Navigation ready");yield return LookAway(s);
                var candidates=all.Where(r=>r.CanAccept(held.definition)&&Mathf.Abs(r.transform.position.y-s.player.transform.position.y)<1&&routes.Route(s.player.transform.position,r,out _,out _))
                    .OrderBy(r=>(r.transform.position-s.player.transform.position).sqrMagnitude).Take(3).ToArray();Check(candidates.Length==3,"Three reachable racks");
                var a=candidates[0];var b=candidates[1];var c=candidates[2];s.racks=candidates;
                foreach(var rack in new[]{a,b}){var item=s.items.First(i=>i!=held&&i.definition==held.definition&&i.location==ItemLocation.Loose);int slot=rack.Add(item);var pose=rack.SlotPose(slot);item.Snap(pose.position,pose.rotation);}
                var nearest=new[]{a,b}.OrderBy(r=>{routes.Route(s.player.transform.position,r,out float length,out _);return length;}).First();
                Check(MatchingRackGuide.FindRack(s,held.definition)==nearest,"Nearest matching rack preferred to empty rack");
                yield return Aim(s,c);Check(s.TargetRack==c,"Actually aiming at alternate empty rack");
                Check(MatchingRackGuide.FindRack(s,held.definition)==c,"Valid aim overrides assigned-rack preference");
                var guide=UnityEngine.Object.FindFirstObjectByType<MatchingRackGuide>();Check(guide.GuidedRack==c,"Live marker follows alternate aimed rack");
                var wrong=s.items.First(i=>i.location==ItemLocation.Loose&&i.definition!=held.definition&&!i.definition.isRat&&c.CanAccept(i.definition));c.Add(wrong);wrong.Snap(c.SlotPose(0).position,c.SlotPose(0).rotation);
                Check(MatchingRackGuide.FindRack(s,held.definition)!=c,"Incompatible aimed rack skipped");
                Count(a,a.Capacity);Check(MatchingRackGuide.FindRack(s,held.definition)==b,"Full rack skipped");Count(b,b.Capacity);
                Check(!MatchingRackGuide.FindRack(s,held.definition),"No eligible destination returns none");Count(a,1);Count(b,1);
                yield return Aim(s,b);yield return LookAway(s);
                Check(MatchingRackGuide.FindRack(s,held.definition,a)==b,"Walking much closer updates old recommendation");
                var stable=MatchingRackGuide.FindRack(s,held.definition);s.player.character.Motor.SetPosition(s.player.transform.position+Vector3.right*.03f);
                Check(MatchingRackGuide.FindRack(s,held.definition,stable)==stable,"Small movement keeps stable recommendation");
                unreachable=new GameObject("Unreachable rack check");unreachable.transform.position=new Vector3(1000,0,1000);var isolated=unreachable.AddComponent<SortRack>();isolated.slots=new[]{unreachable.transform};isolated.Initialize();
                s.racks=new[]{isolated};yield return LookAway(s);Check(!MatchingRackGuide.FindRack(s,held.definition),"Disconnected destination skipped");UnityEngine.Object.Destroy(unreachable);unreachable=null;
                s.racks=all;s.Restore(saved);Check(s.TryPick(held),"Reset presentation fixture");held.CompleteMotion();
                yield return Aim(s,a);Check(guide.GuidedRack==a&&guide.MarkerIsPin,"Visible aimed rack uses location pin");Presentation(guide,s,"Normal view");yield return Capture("01-visible-pin");
                yield return Aim(s,a,.95f);Check(guide.MarkerIsPin,"Nearby rack still uses attached pin");Presentation(guide,s,"Close view");yield return Capture("02-close-pin");
                yield return Aim(s,b);Check(guide.GuidedRack==b&&guide.MarkerIsPin,"Hovering another rack moves pin to that rack");yield return Capture("03-alternate-rack");
                var oldYaw=s.player.look.Yaw;s.player.look.SetYawPitch(new Vector2(oldYaw+180,-45));yield return Wait();s.RefreshTarget();yield return Wait();
                Check(guide.MarkerVisible&&!guide.MarkerIsPin,"Off-screen destination uses directional arrow");Presentation(guide,s,"Off-screen");yield return Capture("04-offscreen-arrow");
                yield return Aim(s,a);
                obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Rack pin occlusion check";var eye=s.player.view.transform;
                obstacle.transform.SetPositionAndRotation(eye.position+eye.forward*.6f,eye.rotation);obstacle.transform.localScale=new Vector3(8,8,.1f);Physics.SyncTransforms();yield return Wait();
                Check(!guide.MarkerVisible&&guide.MarkerOccluded,"Wall does not show a pin through geometry");yield return Capture("05-occluded");UnityEngine.Object.Destroy(obstacle);obstacle=null;yield return Wait();
                var upper=all.FirstOrDefault(r=>Mathf.Abs(r.transform.position.y-s.player.transform.position.y)>1.8f&&r.CanAccept(held.definition)&&routes.Route(s.player.transform.position,r,out _,out _));
                Check(upper,"Reachable upstairs rack exists");s.racks=new[]{upper};guide.enabled=false;guide.enabled=true;yield return LookAway(s);yield return Wait();Check(guide.GuidedRack==upper&&guide.MarkerVisible&&!guide.MarkerIsPin,"Other floor keeps route arrow rather than false visible pin");yield return Capture("06-upstairs-route");s.racks=all;
                foreach(int width in new[]{1280,1440,1560,1600,1680,1152,960})
                {
                    yield return (IEnumerator)typeof(ThumbControlsChecks).GetMethod("Resize",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{width,720});
                    sizes.Apply(sizes.JoystickScale,1.15f);yield return Aim(s,a);Presentation(guide,s,"Layout "+width);yield return Capture("layout-"+width);
                }
                safe.enabled=false;safe.ApplySample(new Rect(60,25,Screen.width-120,Screen.height-25),new Vector2Int(Screen.width,Screen.height));Canvas.ForceUpdateCanvases();yield return Wait();Presentation(guide,s,"Inset UI");yield return Capture("07-safe-area");
                s.player.SetBlocked(true);yield return Wait();Check(!guide.MarkerVisible,"Pause hides marker");s.player.SetBlocked(false);
                File.AppendAllText(Folder+"/checks.txt","DONE\n");
            }
            finally
            {
                if(obstacle)UnityEngine.Object.Destroy(obstacle);if(unreachable)UnityEngine.Object.Destroy(unreachable);
                safe.enabled=safeEnabled;safe.Apply();sizes.Apply(sizes.JoystickScale,size);s.racks=all;foreach(var item in s.items)if(item.Moving||item.Presenting)item.CompleteMotion();s.Restore(saved);s.persistence=persistence;
                for(int i=0;i<gates.Length;i++)gates[i].enabled=gateStates[i];
                typeof(ThumbControlsChecks).GetMethod("RestoreGameView",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
            }
        }
    }
}
#endif
