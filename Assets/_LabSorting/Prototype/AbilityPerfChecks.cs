using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class AbilityPerfChecks
    {
        public static void Measure(SortingSession s,string path)
        {
            var initial=s.Capture();bool persistence=s.persistence;s.persistence=false;
            var report=new System.Text.StringBuilder("Editor main-thread microbenchmark; not device FPS. Items="+s.items.Length+"\n");
            try
            {
                var item=s.items.First(i=>i.location==ItemLocation.Loose);s.TryPick(item);DOTween.CompleteAll();s.TrySearch();
                MeasureOne(report,"nearest search",1000,()=>s.NearestSearchResult());
                MeasureOne(report,"drop planning",30,()=>s.TryThrowPosition(item,out _));
                s.Restore(initial);s.TryPick(item);DOTween.CompleteAll();
                MeasureOne(report,"magnet action",1,()=>s.TryMagnet());DOTween.CompleteAll();
                s.Restore(initial);s.TryPick(item);DOTween.CompleteAll();
                MeasureOne(report,"search action",1,()=>s.TrySearch());
                File.WriteAllText(path,report.ToString());
            }
            finally{DOTween.CompleteAll();s.Restore(initial);s.persistence=persistence;}
        }
        static void MeasureOne(System.Text.StringBuilder text,string name,int count,Action action)
        {
            if(count>1)action();long before=GC.GetAllocatedBytesForCurrentThread();var clock=Stopwatch.StartNew();
            for(int i=0;i<count;i++)action();clock.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            text.AppendLine(name+": mean_ms="+(clock.Elapsed.TotalMilliseconds/count).ToString("F4")+" managed_bytes_per_call="+(bytes/count));
        }
    }
}
