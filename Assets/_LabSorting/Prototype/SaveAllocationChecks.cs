using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class SaveAllocationChecks
    {
        [Serializable] public class Report
        {
            public int items;public bool counterValid;
            public long calibrationBytes,legacyJsonBytes,streamJsonBytes,fullSnapshotBytes,unchangedRequestBytes;
            public string context="Isolated warm main-thread GC.Alloc recorder, excluding delegate creation and recorder setup. Streaming writes to TextWriter.Null to isolate serializer allocations; production also allocates bounded IO buffers. Full Capture is a reference baseline, not the previous incremental implementation. Unsupported counters are -1.";
        }
        static long Measure(Action action)
        {
            // Individual allocation events: a frame-summed sample can still be incomplete
            // when stopped before frame end. Sum bounded event samples explicitly instead.
            using(var recorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"GC.Alloc",65536,ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {action();recorder.Stop();if(!recorder.Valid||recorder.Count>=65536)return -1;long bytes=0;for(int i=0;i<recorder.Count;i++)bytes+=recorder.GetSample(i).Value;return bytes;}
        }
        public static void Run(SortingSession s,Action<bool,string> check)
        {
            var snapshot=s.Capture();var stream=new SaveJsonStream();stream.Write(TextWriter.Null,snapshot);GC.KeepAlive(JsonUtility.ToJson(snapshot));
            long calibration=Measure(()=>GC.KeepAlive(new byte[8192]));bool valid=calibration>=8192;
            var report=new Report{items=s.items.Length,counterValid=valid,calibrationBytes=calibration,legacyJsonBytes=Measure(()=>GC.KeepAlive(JsonUtility.ToJson(snapshot))),streamJsonBytes=Measure(()=>stream.Write(TextWriter.Null,snapshot)),fullSnapshotBytes=Measure(()=>GC.KeepAlive(s.Capture())),unchangedRequestBytes=Measure(()=>s.RequestSave())};
            if(valid){check(report.streamJsonBytes<report.legacyJsonBytes,"Streaming serializer allocates less than full JsonUtility string");check(report.unchangedRequestBytes<report.fullSnapshotBytes,"Unchanged incremental request allocates less than full snapshot");}
            else{report.legacyJsonBytes=report.streamJsonBytes=report.fullSnapshotBytes=report.unchangedRequestBytes=-1;Debug.Log("SAVE_ALLOCATION_COUNTER_UNAVAILABLE: calibration failed; no zero-allocation claim");}
            File.WriteAllText("Logs/SaveAllocationPerformance.json",JsonUtility.ToJson(report,true));
        }
    }
}
