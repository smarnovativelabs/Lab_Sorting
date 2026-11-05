using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class SortingSoakChecks
    {
        const double Duration=900,Warmup=120;
        const long MiB=1024*1024;
        [Serializable] public class Sample
        {
            public double seconds;public int cycles,cash,sorted,held,materials,textures,gameObjects,meshes;
            public long managedUsed,managedReserved,unityUsed,unityReserved,saveBytes;
        }
        [Serializable] public class Report
        {
            public string status="running",context="15-minute real-time Editor soak, first 2 minutes warm-up. 3500 logical items, 75 pre-sorted rack items repeatedly picked/placed with real animations, paid search/upgrades, held and settled save/load and pause-save cycles. Synthetic starting cash 100000 to keep paid actions available; user saves never touched. Raw memory includes uncollected garbage and Editor allocations. Forced GC occurs ONLY at warm-up/end diagnostic boundaries, never during the measured gameplay interval. 8 MiB retained-managed and 64 MiB Unity-allocated growth tolerances allow Editor noise; not proof of indefinite leak freedom or Android memory usage.";
            public double elapsedSeconds;public int cycles,checks,saveRequests,roundTrips,searches,upgrades,pauseSaves,expectedCash,finalCash,finalSorted;
            public Sample baseline,final;public long retainedManagedGrowth,unityUsedGrowth;public List<Sample> samples=new List<Sample>(40);
        }
        static Report report;
        static readonly WaitForSecondsRealtime FrameDelay=new WaitForSecondsRealtime(1f/30f);
        static void Check(bool ok,string label){report.checks++;if(!ok)throw new Exception("SOAK CHECK FAILED: "+label);}
        static void WriteReport(){File.WriteAllText("Logs/SortingSoakReport.json",JsonUtility.ToJson(report,true));}
        static IEnumerator Frames(Camera camera,double seconds)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            do{camera.Render();yield return FrameDelay;}while(timer.Elapsed.TotalSeconds<seconds);
        }
        static IEnumerator Durable(SortingSession s,Camera camera)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while(s.SavePending&&s.LastSaveError==null&&timer.Elapsed.TotalSeconds<20){camera.Render();yield return FrameDelay;}
            Check(!s.SavePending&&s.LastSaveError==null&&s.FlushSaves(1),"Latest checkpoint durable without writer error");
        }
        static IEnumerator Motion(SortItem item,Camera camera)
        {
            // DOTween advances on scaled frame time; wall time can run ahead during an
            // Editor stall. Observe completion instead of assuming 0.3s elapsed is enough.
            var timer=System.Diagnostics.Stopwatch.StartNew();
            do{camera.Render();yield return FrameDelay;}while(item.Moving&&timer.Elapsed.TotalSeconds<10);
            Check(!item.Moving,"Real item animation completes within timeout");
        }
        static void Request(SortingSession s){Check(s.RequestSave(),"Save accepted");report.saveRequests++;}
        static IEnumerator RoundTrip(SortingSession s,Camera camera,int expectedCash)
        {
            var expected=s.Capture();Request(s);yield return Durable(s,camera);
            Check(s.Load(),"Saved checkpoint loads");var actual=s.Capture();
            Check(s.Cash==expectedCash&&actual.cash==expected.cash,"Cash equals independent transaction ledger after reload");
            Check(actual.searchId==expected.searchId&&Math.Abs(actual.searchRemaining-expected.searchRemaining)<1,"Purchased search timer preserved");
            actual.searchRemaining=expected.searchRemaining;
            Check(Vector3.Distance(actual.playerPosition,expected.playerPosition)<.001f&&Vector2.Distance(actual.look,expected.look)<.001f,"Player pose preserved within transform precision");actual.playerPosition=expected.playerPosition;actual.look=expected.look;
            Check(actual.items.Count==expected.items.Count,"Full catalog restored");
            for(int i=0;i<actual.items.Count;i++)
            {Check(Vector3.Distance(actual.items[i].position,expected.items[i].position)<.001f&&Quaternion.Angle(actual.items[i].rotation,expected.items[i].rotation)<.1f,"Item pose preserved within transform precision");actual.items[i].position=expected.items[i].position;actual.items[i].rotation=expected.items[i].rotation;}
            Check(JsonUtility.ToJson(actual)==JsonUtility.ToJson(expected),"Full item poses, slots, inventory order, selection, rewards and capacity round-trip exactly");
            report.roundTrips++;
        }
        static Sample Measure(SortingSession s,double seconds,bool collect)
        {
            // Resource counts are diagnostic snapshots; their temporary arrays are collected
            // before retained-memory readings at comparable quiescent boundaries.
            var sample=new Sample{seconds=seconds,cycles=report.cycles,cash=s.Cash,sorted=s.SortedCount,held=s.carried.Count,
                materials=Resources.FindObjectsOfTypeAll<Material>().Length,textures=Resources.FindObjectsOfTypeAll<Texture>().Length,
                gameObjects=Resources.FindObjectsOfTypeAll<GameObject>().Length,meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,
                saveBytes=File.Exists(s.SavePath)?new FileInfo(s.SavePath).Length:0};
            if(collect){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
            sample.managedUsed=Profiler.GetMonoUsedSizeLong();sample.managedReserved=Profiler.GetMonoHeapSizeLong();
            sample.unityUsed=Profiler.GetTotalAllocatedMemoryLong();sample.unityReserved=Profiler.GetTotalReservedMemoryLong();
            return sample;
        }
        public static IEnumerator Run()
        {
            report=new Report();var catalog=UnityEngine.Object.FindFirstObjectByType<StressCatalog>();var s=catalog.session;var batch=s.GetComponent<ItemBatchRenderer>();
            bool persistence=s.persistence,viewEnabled=s.player.view.enabled;string oldName=s.saveName;int oldRate=Application.targetFrameRate;
            s.persistence=false;s.saveName="sorting-soak-"+Guid.NewGuid().ToString("N")+".json";string path=s.SavePath;
            var cameraObject=new GameObject("SoakCamera");var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=23;camera.transform.SetPositionAndRotation(new Vector3(0,52,19),Quaternion.Euler(90,0,0));camera.farClipPlane=100;
            var texture=new RenderTexture(1280,720,24);camera.targetTexture=texture;s.player.view.enabled=false;batch.targetCamera=camera;Application.targetFrameRate=30;
            SortingSession.Snapshot clean=null;
            try
            {
                yield return catalog.Populate(3500);clean=s.Capture();var seed=s.Capture();seed.cash=100000;Check(s.Restore(seed),"Isolated seed wallet restored");s.player.SendMessage("OnApplicationFocus",true);
                int expected=100000;
                for(int i=0;i<75;i++){Check(s.TryPick(s.items[i]),"Seed pick");s.items[i].CompleteMotion();Check(s.TryPlace(s.racks[i%5]),"Seed place");s.items[i].CompleteMotion();}
                expected+=75*s.placementReward+s.firstTenBonus+3*SortingSession.ShortGoalBonus+5*SortingSession.RackBonus;
                Check(s.Cash==expected&&s.SortedCount==75&&s.CompletedRackCount==5,"Independent seed cash and progress totals");
                s.persistence=true;Request(s);yield return Durable(s,camera);
                var timer=System.Diagnostics.Stopwatch.StartNew();double nextSample=30;
                while(timer.Elapsed.TotalSeconds<Duration)
                {
                    int index=report.cycles%75;var item=s.items[index];var rack=s.racks[index%5];
                    Check(s.TryPick(item),"Repeated rack pickup");Request(s);yield return Motion(item,camera);
                    if(report.cycles%13==0)
                    {
                        int before=s.Cash;bool accepted=s.TrySearch();if(accepted){expected-=Mathf.Max(1,s.searchCost);report.searches++;}
                        Check(s.Cash==(accepted?before-Mathf.Max(1,s.searchCost):before),"Search charges once only on success");
                    }
                    if(report.cycles%100==0&&s.capacity<s.maxCapacity)
                    {int cost=s.UpgradeCost;Check(s.TryUpgradeCapacity(),"Paid capacity upgrade succeeds");expected-=cost;report.upgrades++;}
                    if(report.cycles%10==0)yield return RoundTrip(s,camera,expected);
                    Check(s.TryPlace(rack),"Repeated placement");yield return Motion(item,camera);Check(s.SortedCount==75&&s.carried.Count==0,"Settled inventory and progress remain consistent");
                    Check(s.Cash==expected,"Repeated sorting never farms cash");Request(s);
                    if(report.cycles%10==0)yield return RoundTrip(s,camera,expected);
                    if(report.cycles%60==0){s.SendMessage("OnApplicationPause",true);s.SendMessage("OnApplicationPause",false);report.pauseSaves++;yield return Durable(s,camera);Check(s.Cash==expected,"Pause-save preserves cash");}
                    report.cycles++;report.expectedCash=expected;report.elapsedSeconds=timer.Elapsed.TotalSeconds;
                    if(report.baseline==null&&timer.Elapsed.TotalSeconds>=Warmup)
                    {yield return Durable(s,camera);report.baseline=Measure(s,timer.Elapsed.TotalSeconds,true);Check(report.baseline.managedUsed>0&&report.baseline.unityUsed>0,"Memory counters available");}
                    if(timer.Elapsed.TotalSeconds>=nextSample)
                    {report.samples.Add(Measure(s,timer.Elapsed.TotalSeconds,false));WriteReport();Debug.Log("SOAK_PROGRESS seconds="+(int)timer.Elapsed.TotalSeconds+" cycles="+report.cycles+" reloads="+report.roundTrips+" cash="+s.Cash);nextSample=timer.Elapsed.TotalSeconds+30;}
                }
                yield return RoundTrip(s,camera,expected);yield return Frames(camera,.5);yield return Durable(s,camera);
                report.final=Measure(s,timer.Elapsed.TotalSeconds,true);report.elapsedSeconds=timer.Elapsed.TotalSeconds;report.finalCash=s.Cash;report.finalSorted=s.SortedCount;
                report.retainedManagedGrowth=report.final.managedUsed-report.baseline.managedUsed;report.unityUsedGrowth=report.final.unityUsed-report.baseline.unityUsed;
                Check(report.elapsedSeconds>=Duration&&report.cycles>=200&&report.roundTrips>=40,"Long session exercises sufficient actions and reloads");
                Check(report.retainedManagedGrowth<=8*MiB,"Retained managed memory stays within 8 MiB growth budget");
                Check(report.unityUsedGrowth<=64*MiB,"Unity allocated memory stays within 64 MiB growth budget");
                Check(report.final.materials<=report.baseline.materials+2&&report.final.textures<=report.baseline.textures+2&&report.final.meshes<=report.baseline.meshes+2&&report.final.gameObjects<=report.baseline.gameObjects+2,"No accumulating scene/material/texture/mesh instances");
                Check(report.final.saveBytes<=report.baseline.saveBytes+4096,"Fixed logical state has bounded save size");
                Check(s.items.Length==3500&&s.Cash==expected&&s.SortedCount==75&&s.carried.Count==0,"Final cash and progress match independent ledger");
                report.status="passed";WriteReport();Debug.Log("SORTING_SOAK_OK cycles="+report.cycles+" reloads="+report.roundTrips+" checks="+report.checks);
            }
            finally
            {
                if(report.status!="passed")report.status="incomplete-or-failed";WriteReport();s.FlushSaves();s.persistence=false;if(clean!=null)s.Restore(clean);
                batch.targetCamera=s.player.view;s.player.view.enabled=viewEnabled;camera.targetTexture=null;texture.Release();UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(cameraObject);
                foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);s.saveName=oldName;s.persistence=persistence;Application.targetFrameRate=oldRate;
            }
        }
    }
}
