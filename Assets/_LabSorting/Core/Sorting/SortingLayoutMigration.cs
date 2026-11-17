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
            // Only a complete pre-rat catalog is eligible. A partial modern save must
            // fail validation so Load can recover the durable backup instead.
            if(data!=null&&data.items!=null&&data.items.Count<items.Length)
            {
                var ids=new HashSet<string>();
                foreach(var entry in data.items)if(entry!=null)ids.Add(entry.id);
                int ratCount=0;bool legacy=true;
                foreach(var item in items)
                {
                    if(item.definition.isRat){ratCount++;if(ids.Contains(item.id))legacy=false;}
                    else if(!ids.Contains(item.id))legacy=false;
                }
                if(legacy&&ratCount>0&&data.items.Count==items.Length-ratCount)
                    foreach(var item in items)if(item.definition.isRat)
                        data.items.Add(new Entry{id=item.id,slot=-1,location=ItemLocation.Loose,position=item.transform.position,rotation=item.transform.rotation});
            }
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
