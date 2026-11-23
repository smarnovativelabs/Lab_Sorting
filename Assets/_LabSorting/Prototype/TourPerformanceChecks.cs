#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using LabSorting.UI;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class TourPerformanceChecks
    {
        [Serializable] public class Phase
        {
            public string name;
            public int frames,over33ms,over50ms;
            public double medianFrameMs,p95FrameMs,maxFrameMs,medianTourCpuMs,p95TourCpuMs,medianBatchCpuMs,medianDrawCalls,medianGcBytes;
        }
        [Serializable] public class Report
        {
            public string context="Unity Editor, automatic camera rendering to a fixed 1280x720 target; not device FPS. Overlay UI retains the Editor Game View dimensions. No screenshots or synchronous Camera.Render during sampling. Tour marker includes pose sampling and Cinemachine.ManualUpdate. GC/draw counters include Editor work. -1 indicates unavailable counter. Unique player frames only; first two frames of each phase excluded; initial scene activation is not measured.";
            public string device,pipeline;public int width,height,items,batchedItems,fallbackItems;
            public List<Phase> phases=new List<Phase>();
        }
        static double Percentile(List<double> values,double p){if(values.Count==0)return -1;values.Sort();return values[Math.Min(values.Count-1,(int)Math.Ceiling(values.Count*p)-1)];}
        static IEnumerator Measure(FirstLaunchTour tour,string name,bool intro,Report report)
        {
            var frames=new List<double>(900);var cpu=new List<double>(900);var batch=new List<double>(900);var draws=new List<double>(900);var gc=new List<double>(900);
            var renderer=tour.session.GetComponent<ItemBatchRenderer>();
            using(var tick=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"LabSorting.FirstLaunchTour",1))
            using(var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
            using(var allocations=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
            {
                float deadline=Time.realtimeSinceStartup+(intro?40:6);int warmup=2,lastFrame=Time.frameCount;
                while(Time.realtimeSinceStartup<deadline&&(!intro||tour.IsRunning))
                {
                    yield return null;
                    // EditMode UnityTest enumerators can advance several times per player frame.
                    if(Time.frameCount==lastFrame)continue;
                    lastFrame=Time.frameCount;
                    if(warmup-->0)continue;
                    frames.Add(Time.unscaledDeltaTime*1000.0);cpu.Add(tick.Valid?tick.LastValue/1000000.0:-1);
                    batch.Add(renderer?renderer.PrepareMilliseconds:-1);draws.Add(draw.Valid?draw.LastValue:-1);gc.Add(allocations.Valid?allocations.LastValue:-1);
                }
            }
            if(intro&&tour.IsRunning)throw new Exception("Tour failed to finish within profiling deadline");
            if(frames.Count<30)throw new Exception("Insufficient rendered frames for "+name);
            var phase=new Phase{name=name,frames=frames.Count,medianFrameMs=Percentile(frames,.5),p95FrameMs=Percentile(frames,.95),maxFrameMs=Percentile(frames,1),medianTourCpuMs=Percentile(cpu,.5),p95TourCpuMs=Percentile(cpu,.95),medianBatchCpuMs=Percentile(batch,.5),medianDrawCalls=Percentile(draws,.5),medianGcBytes=Percentile(gc,.5)};
            foreach(var ms in frames){if(ms>33.333)phase.over33ms++;if(ms>50)phase.over50ms++;}
            report.phases.Add(phase);report.batchedItems=renderer?renderer.BatchedItems:0;report.fallbackItems=renderer?renderer.FallbackItems:report.items;
            File.WriteAllText("Logs/TourFrameProfile.json",JsonUtility.ToJson(report,true));
        }
        public static IEnumerator Run()
        {
            var tour=UnityEngine.Object.FindFirstObjectByType<FirstLaunchTour>();
            var originalKey=tour.completionKey;const string key="LabSorting.PerformanceAudit.Tour";
            tour.completionKey=key;PlayerPrefs.DeleteKey(key);tour.SendMessage("OnApplicationFocus",true);
            var camera=tour.session.player.view;var oldTarget=camera.targetTexture;
            var target=new RenderTexture(1280,720,24);camera.targetTexture=target;
            var report=new Report{device=SystemInfo.deviceModel+" / "+SystemInfo.graphicsDeviceName,width=target.width,height=target.height,items=tour.session.items.Length,pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name};
            try
            {
                if(tour.autoPlay||tour.session.player.blocked)throw new Exception("Profiling fixture must start with autoplay and gates disabled");
                if(!tour.BeginTour())throw new Exception("First profile tour did not start");
                yield return Measure(tour,"first tour",true,report);
                yield return Measure(tour,"gameplay idle",false,report);
                PlayerPrefs.DeleteKey(key);
                if(!tour.BeginTour())throw new Exception("Repeat profile tour did not start");
                yield return Measure(tour,"warm repeat tour",true,report);
                // Reproduce teardown ordering: the camera rig and overlay die before the HUD owner.
                PlayerPrefs.DeleteKey(key);
                if(!tour.BeginTour())throw new Exception("Teardown fixture did not start");
                UnityEngine.Object.DestroyImmediate(tour.tourCamera.gameObject);
                UnityEngine.Object.DestroyImmediate(tour.overlay);
                tour.enabled=false;
                if(tour.IsRunning||tour.session.player.blocked||!tour.session.player.look.enabled||tour.Completed)throw new Exception("Destroyed rig cleanup did not release control safely");
                yield return null;
                Debug.Log("TOUR_PROFILE_AND_DESTROYED_RIG_CLEANUP_PASSED");
            }
            finally{tour.enabled=false;tour.completionKey=originalKey;PlayerPrefs.DeleteKey(key);camera.targetTexture=oldTarget;target.Release();UnityEngine.Object.Destroy(target);}
        }
    }
}
#endif
