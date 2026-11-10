using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Profiling;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class StressPerformanceChecks
    {
        [Serializable] public class Sample {public int items;public string mode;public double frameMedianMs,mainThreadMedianMs,renderMedianMs,renderP95Ms,drawCalls,batches,setPass,prepareMedianMs;public int submittedGroups,visibleInstances;}
        [Serializable] public class Operations {public int items,saveBytes;public double searchMs,magnetMs,saveMs,loadMs;}
        [Serializable] public class Report {public string context="Unity Editor, explicit camera render each sample, fixed 1280x720 overhead view, 5 atlas UV meshes / 1 shared Unlit material, shadows off. renderMedianMs times synchronous Camera.Render CPU work including any driver waits, not GPU duration; whole-frame/Main Thread include physics, Editor and scheduling. 3500-item reverse-order repeat checks temporal noise. Not Android FPS. Global renderer counters may include Editor/UI work; -1 means unsupported counter.";public string device;public List<Sample> samples=new List<Sample>();public List<Operations> operations=new List<Operations>();}
        static Report report;
        static void Check(bool pass,string label){if(!pass)throw new Exception("STRESS CHECK FAILED: "+label);Debug.Log("STRESS PASS: "+label);}
        static double Median(List<double> values){values.Sort();return values[values.Count/2];}
        static int CaptureRenderedTexture(RenderTexture texture,string path)
        {
            var previous=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();int colored=0;
            foreach(var pixel in image.GetPixels32())if(Mathf.Max(pixel.r,Mathf.Max(pixel.g,pixel.b))-Mathf.Min(pixel.r,Mathf.Min(pixel.g,pixel.b))>40)colored++;
            File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;return colored;
        }
        static IEnumerator SampleFrames(int count,string mode,ItemBatchRenderer renderer,Camera camera)
        {
            for(int i=0;i<15;i++){camera.Render();yield return null;}
            using(var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
            using(var batches=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
            using(var passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count",1))
            using(var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))
            {
                var frames=new List<double>();var cpu=new List<double>();var renders=new List<double>();var preparation=new List<double>();double d=0,b=0,p=0;int submitted=0,visible=0;
                var clock=new System.Diagnostics.Stopwatch();
                for(int i=0;i<40;i++){clock.Restart();camera.Render();clock.Stop();renders.Add(clock.Elapsed.TotalMilliseconds);yield return null;frames.Add(Time.unscaledDeltaTime*1000);cpu.Add(main.Valid?main.LastValue/1000000.0:-1);preparation.Add(renderer.enabled?renderer.PrepareMilliseconds:0);d+=draws.Valid?draws.LastValue:-1;b+=batches.Valid?batches.LastValue:-1;p+=passes.Valid?passes.LastValue:-1;submitted=renderer.enabled?renderer.SubmittedBatches:0;visible=renderer.enabled?renderer.VisibleInstances:0;}
                double renderMedian=Median(renders);
                report.samples.Add(new Sample{items=count,mode=mode,frameMedianMs=Median(frames),mainThreadMedianMs=Median(cpu),renderMedianMs=renderMedian,renderP95Ms=renders[37],drawCalls=d/40,batches=b/40,setPass=p/40,prepareMedianMs=Median(preparation),submittedGroups=submitted,visibleInstances=visible});
                File.WriteAllText("Logs/StressPerformance.json",JsonUtility.ToJson(report,true));
            }
        }
        public static IEnumerator Run()
        {
            var catalog=UnityEngine.Object.FindFirstObjectByType<StressCatalog>();var s=catalog.session;var batch=s.GetComponent<ItemBatchRenderer>();report=new Report{device=SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType};
            bool persistence=s.persistence;s.persistence=false;s.saveName="sorting-stress-check-"+Guid.NewGuid().ToString("N")+".json";string path=s.SavePath;
            var go=new GameObject("BenchmarkCamera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=23;camera.transform.position=new Vector3(0,52,19);camera.transform.rotation=Quaternion.Euler(90,0,0);camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.08f,.1f);var texture=new RenderTexture(1280,720,24);camera.targetTexture=texture;bool viewEnabled=s.player.view.enabled;s.player.view.enabled=false;batch.targetCamera=camera;
            try
            {
                Check(SystemInfo.supportsInstancing,"Graphics device supports instancing");
                foreach(int count in new[]{500,1500,3500})
                {
                    yield return catalog.Populate(count);var initial=s.Capture();Check(s.items.Length==count&&s.items.Select(i=>i.id).Distinct().Count()==count,"Unique logical catalog for "+count);Check(s.player.character.SortingColliders.Count==count,"Fast collision exclusion catalog for "+count);
                    Check(s.items.All(item=>item.visual.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off&&!item.visual.receiveShadows),"All spawned items have shadows disabled at "+count);
                    batch.enabled=false;yield return SampleFrames(count,"ordinary renderers",batch,camera);
                    int baselinePixels=CaptureRenderedTexture(texture,"Logs/StressBaseline-"+count+".png");
                    batch.enabled=true;batch.Rebuild();yield return SampleFrames(count,"spatial instancing",batch,camera);
                    int optimizedPixels=CaptureRenderedTexture(texture,"Logs/StressBatched-"+count+".png");Check(baselinePixels>count*2&&optimizedPixels>=baselinePixels*.95&&optimizedPixels<=baselinePixels*1.05,"Rendered frames preserve colored item coverage at "+count+" [baseline="+baselinePixels+", batched="+optimizedPixels+"]");
                    Check(batch.BatchedItems==count&&batch.VisibleInstances==count,"All "+count+" items submitted without missing renderers");Check(batch.SubmittedBatches<count/5&&batch.SubmittedBatches<=250,"Bounded draw submissions for "+count);
                    if(count==3500)
                    {
                        float originalCell=batch.cellSize;
                        batch.cellSize=8;batch.MarkDirty();yield return SampleFrames(count,"spatial instancing legacy 8m",batch,camera);
                        int legacyPixels=CaptureRenderedTexture(texture,"Logs/StressBatched8-3500.png");
                        Check(legacyPixels==optimizedPixels,"Cell-size change preserves exact rendered coverage");
                        batch.enabled=false;yield return SampleFrames(count,"ordinary renderers repeat",batch,camera);
                        batch.cellSize=originalCell;batch.enabled=true;batch.Rebuild();yield return SampleFrames(count,"spatial instancing repeat",batch,camera);
                        var position=camera.transform.position;float size=camera.orthographicSize;
                        for(int view=0;view<3;view++)
                        {
                            camera.orthographicSize=8;camera.transform.position=new Vector3(-18+view*18,52,10+view*8);
                            batch.enabled=false;camera.Render();int ordinary=CaptureRenderedTexture(texture,"Logs/FrustumOrdinary-"+view+".png");
                            batch.enabled=true;batch.Rebuild();camera.Render();int instanced=CaptureRenderedTexture(texture,"Logs/FrustumBatched-"+view+".png");
                            Check(ordinary>0&&instanced>=ordinary*.99&&instanced<=ordinary*1.01,"Cropped frustum preserves edge items in view "+view);
                            Check(batch.VisibleInstances>0&&batch.VisibleInstances<count,"Offscreen groups culled in view "+view);
                        }
                        camera.transform.position=position;camera.orthographicSize=size;
                    }
                    s.items[count-1].gameObject.SetActive(false);camera.Render();Check(batch.BatchedItems==count-1,"Deactivation invalidates cached item visibility at "+count);s.items[count-1].gameObject.SetActive(true);camera.Render();Check(batch.BatchedItems==count,"Reactivation restores cached item visibility at "+count);
                    var op=new Operations{items=count};s.TryPick(s.items[0]);DG.Tweening.DOTween.CompleteAll();yield return null;camera.Render();Check(!s.Active.visual.forceRenderingOff,"Held item returns to normal renderer");
                    var clock=System.Diagnostics.Stopwatch.StartNew();Check(s.TrySearch(),"Unlimited-range search at "+count);op.searchMs=clock.Elapsed.TotalMilliseconds;
                    yield return SampleFrames(count,"instancing + search",batch,camera);Check(batch.SubmittedBatches<=250,"Search does not create hundreds of outline draws");
                    clock.Restart();Check(s.TryMagnet(),"Magnet succeeds at "+count);op.magnetMs=clock.Elapsed.TotalMilliseconds;Check(s.carried.Count==s.capacity,"Magnet collects up to capacity at "+count);int cash=s.Cash;Check(!s.TryMagnet()&&s.Cash==cash,"Full magnet does not charge at "+count);
                    s.persistence=true;clock.Restart();s.Save();op.saveMs=clock.Elapsed.TotalMilliseconds;op.saveBytes=(int)new FileInfo(path).Length;s.persistence=false;s.Restore(initial);clock.Restart();Check(s.Load()&&s.carried.Count==s.capacity&&s.Cash==cash,"Complete disk round trip at "+count);op.loadMs=clock.Elapsed.TotalMilliseconds;report.operations.Add(op);
                    s.Restore(initial);yield return null;batch.enabled=false;Check(s.items.All(i=>!i.visual.forceRenderingOff),"Disabling batching restores original visibility at "+count);batch.enabled=true;batch.Rebuild();yield return null;
                    if(count==3500){camera.Render();var old=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/Stress3500.png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=old;}
                    File.WriteAllText("Logs/StressPerformance.json",JsonUtility.ToJson(report,true));
                    if(count==3500)yield return AsyncSaveChecks.RunLarge(s,camera,Check);
                }
                Debug.Log("STRESS_PERFORMANCE_OK");
            }
            finally{s.persistence=persistence;batch.targetCamera=s.player.view;s.player.view.enabled=viewEnabled;camera.targetTexture=null;texture.Release();UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(go);foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
        }
    }
}



