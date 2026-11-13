using System;
using System.Collections;
using System.Linq;
using System.IO;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class DevicePerformanceChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("DEVICE PERF: "+message);Debug.Log("DEVICE PERF PASS: "+message);}
        static IEnumerator Wait(float seconds){float end=Time.time+seconds,limit=Time.realtimeSinceStartup+30;while(Time.time<end){if(Time.realtimeSinceStartup>limit)throw new Exception("Clock stalled");yield return null;}}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var initial=s.Capture();s.player.SendMessage("OnApplicationFocus",true);yield return Wait(.3f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1200,-.1f,1200);floor.transform.localScale=new Vector3(20,.2f,20);
            try
            {
                s.RebuildTypeIndex();Check(s.items.GroupBy(i=>i.definition).All(g=>s.ItemsOfType(g.Key).Count==g.Count()),"Type index exactly matches all 4032 live items");
                foreach(var item in s.items.GroupBy(i=>i.definition).Select(g=>g.First()))
                {
                    var measured=s.ReleaseBounds(item);var rotation=item.transform.rotation;var scale=item.transform.localScale;bool enabled=item.hitbox.enabled;
                    item.transform.rotation=Quaternion.identity;item.transform.localScale=item.originalScale;item.hitbox.enabled=true;Physics.SyncTransforms();var reference=item.hitbox.bounds;reference.Encapsulate(item.visual.bounds);
                    item.transform.rotation=rotation;item.transform.localScale=scale;item.hitbox.enabled=enabled;Physics.SyncTransforms();
                    Check(Vector3.Distance(measured.center,reference.center)<.002f&&Vector3.Distance(measured.size,reference.size)<.002f,"Pure-math drop bounds match physics: "+item.definition.id);
                }
                AbilityPerfChecks.Measure(s,"Logs/AbilitiesOptimized.txt");
                var chosen=s.items.Take(4).ToArray();int completed=0;
                for(int i=0;i<4;i++){var item=chosen[i];var bounds=s.ReleaseBounds(item);var end=new Vector3(1197+i*2,bounds.extents.y+.035f-(bounds.center.y-item.transform.position.y),1200);item.Snap(end+Vector3.up*.3f,Quaternion.identity);item.location=ItemLocation.Loose;item.Travel(end,Quaternion.identity,()=>completed++,true);}
                DOTween.CompleteAll();Check(DropLandingPool.ActiveCount==3,"Physics landing cap is three; oldest completes when full");
                var save=s.Capture();Check(chosen.All(i=>save.items.First(e=>e.id==i.id).position==i.SavedPosition),"Saving during physics uses safe committed destinations");
                var settling=chosen.First(i=>i.Moving);bool wasMoving=settling.Moving;bool picked=s.TryPick(settling);Check(!picked&&settling.location==ItemLocation.Loose,"Pickup during short settle cannot duplicate or lose item; moving="+wasMoving+", picked="+picked+", bodies="+DropLandingPool.ActiveCount+", completed="+completed);
                yield return Wait(.35f);Check(DropLandingPool.ActiveCount==0&&completed==4&&chosen.All(i=>!i.Moving&&i.hitbox.enabled),"All temporary bodies settle and callbacks occur exactly once");
                Check(chosen.All(i=>i.GetComponent<Rigidbody>()==null),"No persistent Rigidbody added to scene items");
                var pool=UnityEngine.Object.FindFirstObjectByType<DropLandingPool>();Check(pool.GetComponentsInChildren<Rigidbody>().Length==3,"Only three reusable physics bodies exist");Check(pool.ContactCount>0,"Temporary bodies produce real floor collision contacts; steps="+pool.PhysicsSteps+", reason="+pool.LastFinishReason+", position="+pool.LastFinishPosition+", target="+pool.LastTarget);
                chosen[0].Travel(chosen[0].transform.position,Quaternion.identity,null,true);DOTween.CompleteAll();s.Restore(initial);Check(DropLandingPool.ActiveCount==0,"Restore cancels physics without orphan bodies");
                chosen[0].Travel(chosen[0].transform.position,Quaternion.identity,null,true);DOTween.CompleteAll();pool.SendMessage("OnApplicationPause",true);Check(DropLandingPool.ActiveCount==0&&!chosen[0].Moving,"Pause completes active landing safely");
                s.Restore(initial);yield return Wait(.2f);Check(s.hand.localPosition.x>=.479f,"Held socket is shifted toward screen-right");
                yield return Ratios(s);
            }
            finally{DOTween.CompleteAll();s.Restore(initial);floor.SetActive(false);UnityEngine.Object.Destroy(floor);}
        }
        static IEnumerator Ratios(SortingSession s)
        {
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var canvas=hud.GetComponent<Canvas>();var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();var safe=hud.GetComponentInChildren<SafeAreaPanel>();var camera=s.player.view;
            var mode=canvas.renderMode;var world=canvas.worldCamera;var target=camera.targetTexture;float scale=canvas.scaleFactor,plane=canvas.planeDistance;bool scalerEnabled=scaler.enabled,safeEnabled=safe.enabled;
            var report=new System.Text.StringBuilder();var corners=new Vector3[4];
            try
            {
                scaler.enabled=false;safe.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.01f;
                foreach(int width in new[]{1280,1440,1560,1600})
                {
                    var rt=new RenderTexture(width,720,24);camera.targetTexture=rt;
                    canvas.scaleFactor=Mathf.Pow(2,Mathf.Lerp(Mathf.Log(width/scaler.referenceResolution.x,2),Mathf.Log(720/scaler.referenceResolution.y,2),scaler.matchWidthOrHeight));
                    safe.ApplySample(new Rect(48,24,width-96,696),new Vector2Int(width,720));Canvas.ForceUpdateCanvases();yield return null;
                    // Screen-space-camera geometry is finalized during rendering after a
                    // render-target/aspect change. Measure the rendered frame, not stale bounds.
                    camera.Render();Canvas.ForceUpdateCanvases();
                    var economy=hud.GetComponent<EconomyHUD>();var actions=hud.GetComponent<ContextActionHUD>();
                    var controls=new[]{(RectTransform)economy.searchButton.transform,(RectTransform)economy.magnetButton.transform,(RectTransform)economy.upgradeButton.transform,(RectTransform)actions.pick.transform,(RectTransform)actions.place.transform,(RectTransform)actions.drop.transform};
                    foreach(var control in controls)
                    {
                        control.GetWorldCorners(corners);var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                        Check(min.x>=47&&max.x<=width-47&&min.y>=23&&max.y<=721,"Safe-area control at "+width+"x720: "+control.name);
                        Check(Mathf.Min(max.x-min.x,max.y-min.y)>=42,"Control touch target remains at least 42px: "+control.name+" size="+(max-min)+" localScale="+control.localScale);
                        if(control!=(RectTransform)economy.upgradeButton.transform){var center=(min+max)*.5f;Check(center.x>width*.75f&&center.y<720*.65f,"Action remains in right-thumb region: "+control.name);}
                    }
                    var joystick=hud.GetComponentsInChildren<LabSorting.Player.LabTouchArea>().First(t=>t.movement);var joy=RectTransformUtility.WorldToScreenPoint(camera,joystick.transform.position);Check(joy.x<width*.25f&&joy.y<720*.35f,"Joystick remains in lower-left thumb region");
                    var texture=new Texture2D(width,720,TextureFormat.RGB24,false);var previous=RenderTexture.active;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,720),0,0);texture.Apply();File.WriteAllBytes("Logs/MobileRatio"+width+".png",texture.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=target;UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(rt);report.AppendLine(width+"x720: inset safe-area and action target bounds passed");
                }
                File.WriteAllText("Logs/MobileRatioChecks.txt",report.ToString());
            }
            finally{camera.targetTexture=target;canvas.renderMode=mode;canvas.worldCamera=world;canvas.planeDistance=plane;canvas.scaleFactor=scale;scaler.enabled=scalerEnabled;safe.enabled=safeEnabled;safe.Apply();}
        }
    }
}
