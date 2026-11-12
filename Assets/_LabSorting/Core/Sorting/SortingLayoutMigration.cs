using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        // Called only on disk load. A one-time authored layout update moves loose items,
        // preserving hand order, sorted racks, cash, upgrades and all reward history.
        public bool RestoreLoadedLayout(Snapshot data)
        {
            bool migrate=data!=null&&layoutRevision>data.layoutRevision&&layoutRevision>0;
            if(migrate&&data.items!=null)
            {
                var poses=new Dictionary<string,Pose>(items.Length);
                foreach(var item in items)poses[item.id]=new Pose(item.transform.position,item.transform.rotation);
                foreach(var entry in data.items)if(entry!=null&&entry.location==ItemLocation.Loose&&poses.TryGetValue(entry.id,out var pose)){entry.position=pose.position;entry.rotation=pose.rotation;}
                data.layoutRevision=layoutRevision;
            }
            bool restored=Restore(data);if(restored&&migrate)saveAt=Time.unscaledTime+.4f;return restored;
        }
    }
}
