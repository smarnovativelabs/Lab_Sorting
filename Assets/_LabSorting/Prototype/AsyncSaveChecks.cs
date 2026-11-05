using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class AsyncSaveChecks
    {
        static IEnumerator Until(Func<bool> ready){float end=Time.realtimeSinceStartup+30;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;}
        public static IEnumerator Run(SortingSession s,Action<bool,string> check)
        {
            SaveStreamChecks.Run(s,check);
            string path=Path.Combine(Application.persistentDataPath,"sorting-worker-test-"+Guid.NewGuid().ToString("N")+".json");
            var gate=new ManualResetEventSlim(false);var entered=new ManualResetEventSlim(false);int writes=0,writerThread=0;int mainThread=Thread.CurrentThread.ManagedThreadId;
            var writer=new DurableSaveWriter(path,(p,json,preserve)=>{writerThread=Thread.CurrentThread.ManagedThreadId;Interlocked.Increment(ref writes);entered.Set();if(!gate.Wait(10000))throw new IOException("Test worker gate timed out");DurableSaveWriter.AtomicWrite(p,json,preserve);});
            try
            {
                var first=s.Capture();first.cash=101;writer.Enqueue(first);yield return Until(()=>entered.IsSet);check(entered.IsSet&&writer.Pending,"Slow writer remains pending while main-thread frames advance");check(writerThread!=mainThread,"Serialization and file writer execute off the Unity main thread");
                for(int i=0;i<12;i++){var next=s.Capture();next.cash=200+i;writer.Enqueue(next);check(writer.QueuedSnapshots<=1,"Save queue retains at most one newest pending snapshot");}
                check(!writer.Flush(1),"Bounded flush reports incomplete slow storage instead of claiming saved");gate.Set();yield return Until(()=>!writer.Pending);check(writer.Flush()&&writer.CompletedSequence==13,"Ordered writer commits newest revision after slow earlier revision");
                var loaded=JsonUtility.FromJson<SortingSession.Snapshot>(File.ReadAllText(path));check(loaded.cash==211&&writes==2,"Rapid requests coalesce without older write overwriting latest cash");check(JsonUtility.FromJson<SortingSession.Snapshot>(File.ReadAllText(path+".bak")).cash==101,"Atomic rotation preserves previous complete backup");
                int failures=1;var failing=new DurableSaveWriter(path,(p,json,preserve)=>{if(Interlocked.Decrement(ref failures)>=0)throw new IOException("Injected storage failure");DurableSaveWriter.AtomicWrite(p,json,preserve);});var failed=s.Capture();failed.cash=777;failing.Enqueue(failed);yield return Until(()=>failing.Error!=null);
                check(!failing.Flush()&&failing.Pending&&JsonUtility.FromJson<SortingSession.Snapshot>(File.ReadAllText(path)).cash==211,"Failed write retains unsaved state and leaves existing primary intact");failing.Retry();yield return Until(()=>!failing.Pending);check(failing.Flush()&&failing.Error==null&&JsonUtility.FromJson<SortingSession.Snapshot>(File.ReadAllText(path)).cash==777,"Storage recovery retries retained snapshot successfully");
                string backup=File.ReadAllText(path+".bak");File.WriteAllText(path,"{truncated");var protectedWriter=new DurableSaveWriter(path);protectedWriter.ProtectBackup();var repaired=s.Capture();repaired.cash=888;protectedWriter.Enqueue(repaired);check(protectedWriter.Flush()&&File.ReadAllText(path+".bak")==backup,"Repair after backup recovery does not replace valid backup with corrupt primary");
                File.WriteAllText(path+".tmp","{partial");check(JsonUtility.FromJson<SortingSession.Snapshot>(File.ReadAllText(path)).cash==888,"Interrupted temp file cannot replace committed progress");
            }
            finally{gate.Set();writer.Flush();gate.Dispose();entered.Dispose();foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
        }
        [Serializable] public class Performance {public int items,requests,catalogCopies,gen0Collections;public bool allocationCounterValid;public double captureMedianMs,captureP95Ms,captureMaxMs,worstPathMs,worstSnapshotMs,worstQueueMs,workerSerializationMs,workerWriteMs;public long medianMainThreadAllocatedBytes;public string context="3500-item 120 warm requests during rendered gameplay; every fourth request dirties one item. Streaming JSON plus buffered writes run on one worker. Serialization time includes buffered IO; write time is final durable flush/replace. GC collection count covers the whole rendered test window, not saves alone. Allocation -1 means runtime counter failed calibration. Explicit pause/quit/load flush can still wait for storage. Editor measurements, not Android guarantees.";}
        public static IEnumerator RunLarge(SortingSession s,Camera camera,Action<bool,string> check)
        {
            var initial=s.Capture();bool persistence=s.persistence;s.persistence=true;var timings=new List<double>(120);var allocations=new List<long>(120);double maxObserved=-1,worstPath=0,worstSnapshot=0,worstQueue=0;
            try
            {
                s.InitializeSaveCache();s.RequestSave();yield return Until(()=>!s.SavePending);check(s.FlushSaves(),"3500-item initial checkpoint is durable");
                SaveAllocationChecks.Run(s,check);yield return Until(()=>!s.SavePending);
                long allocationStart=GC.GetAllocatedBytesForCurrentThread();var probe=new byte[8192];GC.KeepAlive(probe);bool allocationValid=GC.GetAllocatedBytesForCurrentThread()-allocationStart>=8192;int copies=s.SaveCatalogCopies,collections=GC.CollectionCount(0);
                for(int i=0;i<120;i++)
                {
                    if(i%4==0)s.items[s.items.Length-1].Snap(s.items[s.items.Length-1].transform.position, s.items[s.items.Length-1].transform.rotation);
                    long bytes=GC.GetAllocatedBytesForCurrentThread(),started=System.Diagnostics.Stopwatch.GetTimestamp();bool accepted=s.RequestSave();timings.Add((System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency);allocations.Add(GC.GetAllocatedBytesForCurrentThread()-bytes);if(timings[timings.Count-1]>maxObserved){maxObserved=timings[timings.Count-1];worstPath=s.LastSavePathMilliseconds;worstSnapshot=s.LastSaveSnapshotMilliseconds;worstQueue=s.LastSaveQueueMilliseconds;}check(accepted,"Large background save request accepted");camera.Render();yield return null;
                }
                int catalogCopies=s.SaveCatalogCopies-copies,gen0=GC.CollectionCount(0)-collections;check(catalogCopies>=30&&catalogCopies<=32,"Unchanged requests reuse immutable catalog; only dirty requests copy it");
                yield return Until(()=>!s.SavePending);check(s.FlushSaves()&&s.LastSaveError==null,"3500-item repeated saves finish without storage errors");
                s.TryPick(s.items[0]);s.RequestSave();DG.Tweening.DOTween.CompleteAll();s.TryPlace(s.racks[0]);int cash=s.Cash;s.RequestSave();yield return Until(()=>!s.SavePending);check(s.FlushSaves(),"Newest placement snapshot is durable after rapid pickup/place");s.Restore(initial);check(s.Load()&&s.Cash==cash&&s.SortedCount==1&&s.carried.Count==0,"Cash and sorted progress load from the same newest complete snapshot");
                // Capture destination without forcing the ongoing throw animation to complete.
                s.persistence=false;s.Restore(initial);s.TryPick(s.items[0]);DG.Tweening.DOTween.CompleteAll();float end=Time.unscaledTime+.3f;while(Time.unscaledTime<end)yield return null;s.ThrowActive();var destination=s.items[0].SavedPosition;s.persistence=true;s.RequestSave();check(s.items[0].Moving,"Async save preserves throw animation");yield return Until(()=>!s.SavePending);s.persistence=false;s.Restore(initial);check(s.Load()&&Vector3.Distance(s.items[0].transform.position,destination)<.001f,"Incremental cache saves final safe throw destination");
                timings.Sort();allocations.Sort();var result=new Performance{items=s.items.Length,requests=timings.Count,catalogCopies=catalogCopies,gen0Collections=gen0,allocationCounterValid=allocationValid,captureMedianMs=timings[timings.Count/2],captureP95Ms=timings[(int)(timings.Count*.95f)],captureMaxMs=timings[timings.Count-1],worstPathMs=worstPath,worstSnapshotMs=worstSnapshot,worstQueueMs=worstQueue,medianMainThreadAllocatedBytes=allocationValid?allocations[allocations.Count/2]:-1,workerSerializationMs=s.LastSaveSerializationMilliseconds,workerWriteMs=s.LastSaveWriteMilliseconds};File.WriteAllText("Logs/SaveHitchPerformance.json",JsonUtility.ToJson(result,true));
            }
            finally{s.FlushSaves();s.persistence=false;s.Restore(initial);s.persistence=persistence;}
        }
    }
}



