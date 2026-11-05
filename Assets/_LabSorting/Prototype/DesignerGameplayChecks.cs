using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LabSorting.Sorting;
using UnityEngine;
using Unity.Profiling;

namespace LabSorting.Prototype
{
    public static class DesignerGameplayChecks
    {
        static int checks;
        static void Check(bool ok,string message){if(!ok)throw new Exception("DESIGNER CHECK FAILED: "+message);checks++;Debug.Log("DESIGNER PASS: "+message);}
        static IEnumerator Wait(float seconds)
        {
            float end=Time.time+seconds,deadline=Time.realtimeSinceStartup+30;
            while(Time.time<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Designer player loop stopped");yield return null;}
        }
        static IEnumerator Motion(SortItem item)
        {
            float deadline=Time.realtimeSinceStartup+10;
            do{yield return null;if(Time.realtimeSinceStartup>deadline)throw new Exception("Designer animation timeout");}while(item.Moving);
        }
        static IEnumerator Aim(SortingSession s,Vector3 feet,Vector3 target)
        {
            s.player.Clear();s.player.character.Motor.BaseVelocity=Vector3.zero;s.player.character.Motor.SetPositionAndRotation(feet,Quaternion.identity);
            yield return Wait(.12f);s.player.look.SnapToDirection(target-s.player.view.transform.position);yield return Wait(.12f);
        }
        static void Capture(Camera camera,string file)
        {
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(image);}
        }
        [Serializable] public class Metric {public bool instanced;public double renderMedianMs,batches;public int submitted,visible;}
        [Serializable] public class Report {public int checks,items=3500,categories=26;public string context="Actual designer meshes, four shared atlas materials; Editor camera-render CPU timing, not Android FPS. Render-only stress placement is disposable.";public List<Metric> samples=new List<Metric>();}
        static IEnumerator Measure(Camera camera,ItemBatchRenderer batch,bool enabled,Report report)
        {
            batch.enabled=enabled;if(enabled)batch.Rebuild();for(int i=0;i<15;i++){camera.Render();yield return null;}
            using(var counter=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
            {
                var times=new List<double>();double batches=0;var timer=new System.Diagnostics.Stopwatch();
                for(int i=0;i<40;i++){timer.Restart();camera.Render();timer.Stop();times.Add(timer.Elapsed.TotalMilliseconds);yield return null;batches+=counter.Valid?counter.LastValue:-1;}
                times.Sort();report.samples.Add(new Metric{instanced=enabled,renderMedianMs=times[20],batches=batches/40,submitted=enabled?batch.SubmittedBatches:0,visible=enabled?batch.VisibleInstances:0});
            }
        }
        public static IEnumerator Run()
        {
            checks=0;var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();s.persistence=false;
            s.player.SendMessage("OnApplicationFocus",true);yield return Wait(.15f);
            var initial=s.Capture();var originalItems=s.items;string oldName=s.saveName;s.saveName="designer-tests-"+Guid.NewGuid().ToString("N")+".json";string savePath=s.SavePath;
            var batch=s.GetComponent<ItemBatchRenderer>();var recovery=s.GetComponent<ItemRecovery>();bool recoveryEnabled=recovery.automatic;recovery.automatic=false;
            GameObject generated=null,cameraObject=null;RenderTexture target=null;
            try
            {
                Check(originalItems.Length==78&&s.racks.Length==26,"78 real items / 26 available category racks");
                Check(originalItems.Select(i=>i.definition).Distinct().Count()==26,"Every supplied model has an item definition");
                Check(originalItems.Select(i=>i.visual.sharedMaterial).Distinct().Count()==4,"Four shared atlas materials, no per-item material copies");
                Check(originalItems.All(i=>!i.visual.receiveShadows&&i.visual.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off),"All actual item shadows disabled");
                var floor=GameObject.Find("AlignedFloorCollision").GetComponent<BoxCollider>();Check(floor.bounds.size.y<.3f&&floor.bounds.size.z>17,"Designer floor collider is horizontal");
                Check(s.player.transform.position.y>=-.02f&&s.player.transform.position.y<.1f,"Player stands on actual designer floor");
                Capture(s.player.view,"Logs/DesignerGameplay-Spawn.png");
                for(int i=0;i<26;i++)
                {
                    var item=originalItems[i];var rack=s.racks[i];
                    yield return Aim(s,item.transform.position+new Vector3(0,.03f,-1.0f),item.hitbox.bounds.center);
                    Check(s.TargetItem==item,"Real collider target: "+item.definition.displayName);
                    Check(s.TryPick(item),"Pick "+item.definition.id);yield return Motion(item);
                    yield return Aim(s,rack.transform.position+new Vector3(0,.03f,-1.6f),rack.label.transform.position);
                    Check(s.TargetRack==rack,"Designer rack indicator can be targeted "+i);
                    Check(s.TryPlace(rack),"Place "+item.definition.id);yield return Motion(item);Physics.SyncTransforms();
                    Check(Mathf.Abs(item.hitbox.bounds.min.y-rack.slots[item.slot].position.y)<.003f,"Item base sits on shelf: "+item.definition.id);
                    int cash=s.Cash;
                    s.player.look.SnapToDirection(item.hitbox.bounds.center-s.player.view.transform.position);yield return Wait(.15f);
                    Check(s.TargetItem==item,"Placed item remains targetable "+i);
                    Check(s.TryPick(item),"Retrieve placed item "+i);yield return Motion(item);
                    Check(rack.Count==0&&rack.Type==null,"Empty rack releases type "+i);
                    Check(s.TryPlace(rack),"Replace retrieved item "+i);yield return Motion(item);Check(s.Cash==cash,"No duplicate placement cash "+i);
                }
                Capture(s.player.view,"Logs/DesignerGameplay-Rack.png");
                var thrown=originalItems[0];Check(s.TryPick(thrown),"Pick actual model for safe throw");yield return Motion(thrown);
                yield return Aim(s,new Vector3(-10,.03f,-7),new Vector3(-10,1.6f,-5));s.ThrowActive();yield return Motion(thrown);
                Check(thrown.location==ItemLocation.Loose&&s.carried.Count==0,"Safe throw releases one actual model");
                Check(s.Restore(initial),"Reset authored designer catalog");
                generated=new GameObject("Disposable Designer Stress Items");batch.enabled=false;s.player.SetBlocked(true);
                var templates=originalItems.Take(26).ToArray();foreach(var item in originalItems)item.gameObject.SetActive(false);
                var items=new SortItem[3500];
                for(int i=0;i<items.Length;i++)
                {
                    var item=UnityEngine.Object.Instantiate(templates[i%26],generated.transform);item.id="designer-stress-"+i;item.location=ItemLocation.Loose;item.rack=null;item.slot=-1;item.gameObject.SetActive(true);
                    item.Snap(new Vector3((i%70-34.5f)*.45f,.02f,-11+(i/70)*.45f),Quaternion.identity);items[i]=item;
                    if(i%64==63)yield return null;
                }
                s.items=items;s.RefreshSortingColliders();s.InitializeSaveCache();
                cameraObject=new GameObject("DesignerStressCamera");var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=18;camera.transform.SetPositionAndRotation(new Vector3(0,6.8f,0),Quaternion.Euler(90,0,0));camera.farClipPlane=60;
                target=new RenderTexture(1280,720,24);camera.targetTexture=target;s.player.view.enabled=false;batch.targetCamera=camera;
                var report=new Report();yield return Measure(camera,batch,false,report);yield return Measure(camera,batch,true,report);
                Check(batch.BatchedItems==3500&&batch.VisibleInstances==3500,"All 3500 actual models batched and submitted");
                Check(batch.SubmittedBatches<=250,"Actual designer item submissions stay bounded: "+batch.SubmittedBatches);
                Check(report.samples[1].batches>0&&report.samples[1].batches<report.samples[0].batches,"Actual-art batching reduces total batches");
                Capture(camera,"Logs/DesignerStress3500.png");
                s.player.SetBlocked(false);Check(s.TryPick(items[0]),"3500 real items: pick");yield return Motion(items[0]);Check(s.TrySearch(),"3500 real items: search");Check(s.TryMagnet(),"3500 real items: magnet");yield return Wait(.3f);
                int wallet=s.Cash;Check(!s.TryMagnet()&&s.Cash==wallet,"3500 real items: full magnet does not charge");
                s.persistence=true;s.Save();Check(s.FlushSaves()&&s.LastSaveError==null,"3500 real item checkpoint durable");s.persistence=false;
                Check(s.Load()&&s.Cash==wallet&&s.carried.Count==s.capacity&&s.items.Length==3500,"3500 real item inventory/cash reload");
                report.checks=checks;File.WriteAllText("Logs/DesignerGameplayTests.json",JsonUtility.ToJson(report,true));Debug.Log("DESIGNER_GAMEPLAY_OK checks="+checks);
            }
            finally
            {
                s.FlushSaves();s.persistence=false;s.player.SetBlocked(false);batch.enabled=false;s.player.view.enabled=true;batch.targetCamera=s.player.view;
                if(cameraObject){cameraObject.GetComponent<Camera>().targetTexture=null;UnityEngine.Object.Destroy(cameraObject);}if(target){target.Release();UnityEngine.Object.Destroy(target);}
                if(generated){generated.SetActive(false);UnityEngine.Object.Destroy(generated);}s.items=originalItems;foreach(var item in originalItems)item.gameObject.SetActive(true);s.RefreshSortingColliders();s.Restore(initial);s.InitializeSaveCache();batch.enabled=true;batch.Rebuild();recovery.automatic=recoveryEnabled;s.saveName=oldName;
                foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(savePath+suffix))File.Delete(savePath+suffix);
            }
        }
    }
}
