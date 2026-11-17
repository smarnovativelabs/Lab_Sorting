#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.Player;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class RequestedPolishChecks
    {
        static void Check(bool value,string message){if(!value)throw new Exception("POLISH: "+message);Debug.Log("POLISH PASS: "+message);}
        public static IEnumerator HeldSizing()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            s.player.SendMessage("OnApplicationFocus",true);s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);
            var initial=s.Capture();var carry=s.hand.GetComponent<CarryPresentation>();
            string folder="Logs/HeldSizeReview/"+Screen.width+"x"+Screen.height;Directory.CreateDirectory(folder);
            var report=new StringBuilder("Item,Multiplier,Width,Height,InsideSafeRegion\n");
            var unique=s.items.GroupBy(i=>i.definition).Select(g=>g.First()).ToArray();
            var hud=UnityEngine.Object.FindFirstObjectByType<ContextActionHUD>();
            var controls=UnityEngine.Object.FindFirstObjectByType<ControlSizes>();
            var task=UnityEngine.Object.FindFirstObjectByType<TaskCardLayout>();
            var obstacles=UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(r=>r.name=="SelectedItemCard"||r==hud.pick.transform||r==hud.drop.transform||r==hud.place.transform||r==task.card||r==controls.joystick||controls.actions.Contains(r)).ToArray();
            try
            {
                foreach(var item in unique)
                {
                    item.CompleteMotion();Check(s.TryPick(item),"Pick "+item.definition.displayName);item.CompleteMotion();
                    yield return new WaitForSecondsRealtime(.2f);Canvas.ForceUpdateCanvases();yield return null;
                    var rect=carry.HeldScreenRect;var safe=carry.HeldSafeRect;
                    bool fits=rect.xMin>=safe.xMin-2&&rect.xMax<=safe.xMax+2&&rect.yMin>=safe.yMin-2&&rect.yMax<=safe.yMax+2;
                    report.AppendLine($"\"{item.definition.displayName}\",{item.definition.heldSizeMultiplier},{rect.width:F1},{rect.height:F1},{fits}");
                    File.WriteAllText(folder+"/results.csv",report.ToString());
                    Check(fits,"Safe region "+item.definition.displayName+" "+rect+" safe="+safe);
                    Check(Mathf.Max(rect.width,rect.height)>=Screen.height*.10f,"Readable size "+item.definition.displayName);
                    Check(rect.height<=Screen.height*.34f&&rect.width<=Screen.width*.31f,"Moderate size "+item.definition.displayName);
                    foreach(var obstacle in obstacles)
                    {
                        var corners=new Vector3[4];obstacle.GetWorldCorners(corners);var canvas=obstacle.GetComponentInParent<Canvas>();
                        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                        var points=corners.Select(c=>(Vector2)RectTransformUtility.WorldToScreenPoint(camera,c)).ToArray();
                        var bounds=Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
                        Check(!rect.Overlaps(bounds),"No overlap with "+obstacle.name+": "+item.definition.displayName);
                    }
                    ScreenCapture.CaptureScreenshot(folder+"/"+item.definition.id+".png");yield return new WaitForSecondsRealtime(.1f);
                    s.ThrowActive();item.CompleteMotion();
                    Check(Vector3.Distance(item.transform.localScale,item.originalScale)<.0001f,"World size restored "+item.definition.displayName);
                }
                Debug.Log("HELD_SIZE_CHECKS_OK unique="+unique.Length+" resolution="+Screen.width+"x"+Screen.height);
            }
            finally{DG.Tweening.DOTween.CompleteAll();s.Restore(initial);}
        }
        static IEnumerator FrozenBones(LabRat rat,string state)
        {
            yield return new WaitForSecondsRealtime(.2f);
            var bones=rat.animator.GetComponentsInChildren<Transform>();
            var rotations=bones.Select(b=>b.localRotation).ToArray();var positions=bones.Select(b=>b.localPosition).ToArray();
            yield return new WaitForSecondsRealtime(.6f);
            Check(rat.animator.speed==0&&!rat.Running,state+" animator frozen");
            Check(bones.Select((b,i)=>Quaternion.Angle(b.localRotation,rotations[i])<.01f&&(b.localPosition-positions[i]).sqrMagnitude<.00000001f).All(v=>v),state+" actual bone pose unchanged");
        }
        public static IEnumerator Corrections()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            s.player.SendMessage("OnApplicationFocus",true);s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);
            var saved=s.Capture();var guide=UnityEngine.Object.FindFirstObjectByType<GuidedTutorial>();var prefix=guide.preferencesPrefix;
            Directory.CreateDirectory("Logs/CorrectionReview");
            try
            {
                var rats=UnityEngine.Object.FindObjectsByType<LabRat>(FindObjectsSortMode.None);var glass=s.racks.First(r=>r.ratsOnly);
                foreach(var rat in rats)
                {
                    rat.Item.CompleteMotion();Check(s.TryPick(rat.Item),"Pick "+rat.name);rat.Item.CompleteMotion();
                    yield return FrozenBones(rat,"Held "+rat.name);
                    Check(s.TryPlace(glass),"Place rat in glass");rat.Item.CompleteMotion();yield return FrozenBones(rat,"Glass "+rat.name);
                }
                var released=rats[0];Check(s.TryPick(released.Item),"Retrieve glass rat");released.Item.CompleteMotion();s.ThrowActive();
                float deadline=Time.realtimeSinceStartup+10;
                while((released.Item.Moving||!released.Running)&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(released.Running&&released.animator.speed==1,"Dropped rat resumes animation while moving");
                var movingBones=released.animator.GetComponentsInChildren<Transform>();var start=movingBones.Select(b=>b.localRotation).ToArray();
                yield return new WaitForSecondsRealtime(.15f);
                Check(movingBones.Select((b,i)=>Quaternion.Angle(b.localRotation,start[i])>.1f).Any(v=>v),"Released rat bones animate again");
                var carry=s.hand.GetComponent<CarryPresentation>();
                foreach(var item in s.items.Where(i=>i.definition.displayName.Contains("Microscope")).GroupBy(i=>i.definition).Select(g=>g.First()))
                {
                    Check(s.TryPick(item),"Pick "+item.definition.displayName);item.CompleteMotion();yield return new WaitForSecondsRealtime(.25f);
                    var current=carry.HeldScreenRect;float multiplier=item.definition.heldSizeMultiplier;item.definition.heldSizeMultiplier=1;
                    yield return new WaitForSecondsRealtime(.25f);var baseline=carry.HeldScreenRect;item.definition.heldSizeMultiplier=multiplier;yield return new WaitForSecondsRealtime(.25f);
                    Check(Mathf.Max(current.width,current.height)>Mathf.Max(baseline.width,baseline.height)*1.08f,"Microscope visibly larger than previous size: current="+current+" baseline="+baseline+" multiplier="+multiplier);
                    var rect=carry.HeldScreenRect;var safe=carry.HeldSafeRect;
                    Check(rect.xMin>=safe.xMin-2&&rect.xMax<=safe.xMax+2&&rect.yMin>=safe.yMin-2&&rect.yMax<=safe.yMax+2,"Larger microscope avoids selected card and controls: "+rect+" safe="+safe);
                    ScreenCapture.CaptureScreenshot("Logs/CorrectionReview/"+item.definition.id+".png");yield return new WaitForSecondsRealtime(.2f);
                    s.ThrowActive();item.CompleteMotion();
                }
                guide.preferencesPrefix="LabSorting.CorrectionReview.";PlayerPrefs.SetInt(guide.preferencesPrefix+"GuidedStep",(int)GuidedTutorial.Step.Tools);PlayerPrefs.SetInt(guide.preferencesPrefix+"GuidedToolPage",0);guide.Begin(false);
                for(int page=0;page<2;page++)
                {
                    yield return new WaitForSecondsRealtime(.4f);
                    var next=guide.gotItButton.transform as RectTransform;var tip=next.InverseTransformPoint(guide.hand.position);
                    Check(guide.Mode==GuidedTutorial.HandMode.Tap&&tip.y<next.rect.yMin,"NEXT hand points from below on page "+page);
                    Check(Mathf.Abs(Mathf.DeltaAngle(guide.hand.localEulerAngles.z,90))<.1f,"NEXT hand points upward");
                    ScreenCapture.CaptureScreenshot("Logs/CorrectionReview/next-"+page+".png");yield return new WaitForSecondsRealtime(.2f);guide.GotIt();
                }
                Debug.Log("CORRECTION_CHECKS_OK rats="+rats.Length);
            }
            finally{guide.Skip();guide.preferencesPrefix=prefix;DG.Tweening.DOTween.CompleteAll();s.Restore(saved);}
        }
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();
            while(!s.Initialized)yield return null;
            s.player.SendMessage("OnApplicationFocus",true);s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);
            var initial=s.Capture();var presentation=s.hand.GetComponent<CarryPresentation>();
            var report=new StringBuilder("Item,Width,Height,Scale,InsideSafeRegion\n");
            string folder="Logs/HeldItemReview/"+Screen.width+"x"+Screen.height;Directory.CreateDirectory(folder);
            var unique=s.items.GroupBy(i=>i.definition).Select(g=>g.First()).ToArray();
            try
            {
                int n=0;
                foreach(var item in unique)
                {
                    item.CompleteMotion();Check(s.TryPick(item),"Pick "+item.definition.displayName);
                    item.CompleteMotion();yield return null;yield return null;Canvas.ForceUpdateCanvases();yield return null;
                    var rect=presentation.HeldScreenRect;var safe=presentation.HeldSafeRect;
                    bool fits=rect.xMin>=safe.xMin-2&&rect.xMax<=safe.xMax+2&&rect.yMin>=safe.yMin-2&&rect.yMax<=safe.yMax+2;
                    report.AppendLine($"\"{item.definition.displayName}\",{rect.width:F1},{rect.height:F1},{item.transform.localScale.x:F4},{fits}");
                    File.WriteAllText(folder+"/results.csv",report.ToString());
                    Check(fits,"UI clearance "+item.definition.displayName+" "+rect+" safe="+safe);
                    Check(Mathf.Max(rect.width,rect.height)>=Screen.height*.10f,"Readable held size "+item.definition.displayName);
                    var rat=item.GetComponent<LabRat>();if(rat)Check(!rat.Running&&!rat.animator.GetBool("Running"),"Held rat animation stopped");
                    ScreenCapture.CaptureScreenshot(folder+"/"+(n++).ToString("D3")+".png");yield return new WaitForSecondsRealtime(.15f);
                    int before=s.carried.Count;s.ThrowActive();
                    Check(item.location==ItemLocation.Loose&&s.carried.Count==before-1&&!s.DropPending,"Immediate crowded drop "+item.definition.displayName);
                    float end=Time.realtimeSinceStartup+4;while(item.Moving&&Time.realtimeSinceStartup<end)yield return null;
                    Check(!item.Moving&&item.hitbox.enabled,"Drop settles "+item.definition.displayName);
                }
                var quick=unique[0];Check(s.TryPick(quick),"Pickup before immediate drop");s.ThrowActive();Check(quick.location==ItemLocation.Loose&&!s.DropPending,"Drop during pickup animation");quick.CompleteMotion();
                Check(s.TryPick(quick),"Pickup search context");quick.CompleteMotion();
                Check(s.TrySearch(),"Search activates");yield return new WaitForSecondsRealtime(2.5f);
                Check(s.ItemsOfType(quick.definition).Where(i=>i.location==ItemLocation.Loose).All(i=>i.Searched),"All matching loose items highlighted");
                var locator=UnityEngine.Object.FindFirstObjectByType<ItemLocatorHUD>();
                Check(locator.beacons.Any(b=>b.gameObject.activeInHierarchy),"Search marker visible after camera focus");
                ScreenCapture.CaptureScreenshot(folder+"/search.png");yield return new WaitForSecondsRealtime(.2f);
                Debug.Log("REQUESTED_POLISH_CHECKS_OK unique="+unique.Length);
            }
            finally{DG.Tweening.DOTween.CompleteAll();s.Restore(initial);}
        }
    }
}
#endif
