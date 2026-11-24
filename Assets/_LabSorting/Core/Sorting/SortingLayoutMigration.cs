using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        [System.Serializable] public struct LayoutFix {public string id;public Vector3 previous;}
        // Single authored items moved without a full layout revision (e.g. after a level collider is refitted).
        // On load, a loose item still at its previous authored position takes its current scene pose; an item
        // the player has moved is left where the player put it.
        [Tooltip("Written by editor upgrades. Each entry moves one untouched loose item of an existing save to its current scene pose.")]
        public LayoutFix[] layoutFixes=new LayoutFix[0];
        [System.Serializable] public struct ContentAddition {public string id;public int introducedInVersion;}
        [Tooltip("For each new item, keep its stable ID and the contentVersion that introduced it. Never remove older entries. Existing items are version 1.")]
        public ContentAddition[] contentAdditions=new ContentAddition[0];
        public int LastLoadRelocated {get;private set;}
        public int LastLoadSettled {get;private set;}
        // Called only on disk load. A one-time authored layout update moves loose items,
        // preserving hand order, sorted racks, cash, upgrades and all reward history.
        public bool RestoreLoadedLayout(Snapshot data)
        {
            if(data==null||data.savedContentVersion<0||data.savedContentVersion>contentVersion||(!string.IsNullOrEmpty(data.savedAreaId)&&data.savedAreaId!=areaId))return false;
            // Work on a copy: failed validation must not alter the caller's save or the live run.
            data=JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(data));
            bool added=false;
            if(!AppendContent(data,out added))return false;
            // Only a complete pre-rat catalog is eligible. A partial modern save must
            // fail validation so Load can recover the durable backup instead.
            if(data.savedContentVersion==0&&data.items!=null&&data.items.Count<items.Length)
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
                {
                    foreach(var item in items)if(item.definition.isRat)
                        data.items.Add(new Entry{id=item.id,slot=-1,location=ItemLocation.Loose,position=item.transform.position,rotation=item.transform.rotation});
                    added=true;
                }
            }
            if(added&&data.milestones!=null&&data.milestones.Contains("complete"))
            {
                // Retain the previous achievement, but the extended lab is no longer fully sorted.
                if(!data.runCompletionRecorded){data.completedArea=areaId;data.completionCount++;data.completedContentVersion=Mathf.Max(1,data.savedContentVersion);data.runCompletionRecorded=true;}
                data.milestones.Remove("complete");data.completionAcknowledged=false;
            }
            bool migrate=data!=null&&layoutRevision>data.layoutRevision&&layoutRevision>0;
            if(migrate&&data.items!=null)
            {
                var poses=new Dictionary<string,Pose>(items.Length);
                foreach(var item in items)poses[item.id]=new Pose(item.transform.position,item.transform.rotation);
                foreach(var entry in data.items)if(entry!=null&&entry.location==ItemLocation.Loose&&poses.TryGetValue(entry.id,out var pose)){entry.position=pose.position;entry.rotation=pose.rotation;}
                data.layoutRevision=layoutRevision;
            }
            int relocated=migrate?0:ApplyLayoutFixes(data);
            bool restored=Restore(data);
            // Anything an older build left hanging in the air is set down before the first frame shows it.
            int settled=restored?SettleFloatingLooseItems():0;
            LastLoadRelocated=restored?relocated:0;LastLoadSettled=settled;
            if(restored&&(added||migrate||relocated>0||settled>0))saveAt=Time.unscaledTime+.4f;return restored;
        }
        bool AppendContent(Snapshot data,out bool added)
        {
            added=false;if(data.items==null)return false;
            var savedIds=new HashSet<string>();
            foreach(var entry in data.items)if(entry==null||string.IsNullOrEmpty(entry.id)||!savedIds.Add(entry.id))return false;
            var introductions=new Dictionary<string,int>();
            foreach(var addition in contentAdditions)
            {
                if(string.IsNullOrEmpty(addition.id)||addition.introducedInVersion<2||addition.introducedInVersion>contentVersion||introductions.ContainsKey(addition.id))return false;
                introductions.Add(addition.id,addition.introducedInVersion);
            }
            int previousVersion=Mathf.Max(1,data.savedContentVersion);
            foreach(var item in items)
            {
                if(savedIds.Contains(item.id)||!introductions.TryGetValue(item.id,out int introduced)||introduced<=previousVersion)continue;
                if(!AuthoredPose(item.id,out var pose))return false;
                data.items.Add(new Entry{id=item.id,slot=-1,location=ItemLocation.Loose,position=pose.position,rotation=pose.rotation});added=true;
            }
            return true;
        }
        int ApplyLayoutFixes(Snapshot data)
        {
            if(layoutFixes==null||layoutFixes.Length==0||data==null||data.items==null)return 0;
            var entries=new Dictionary<string,Entry>(data.items.Count);
            foreach(var entry in data.items)if(entry!=null&&entry.id!=null&&!entries.ContainsKey(entry.id))entries.Add(entry.id,entry);
            int count=0;
            foreach(var fix in layoutFixes)
            {
                if(fix.id==null||!entries.TryGetValue(fix.id,out var entry)||entry.location!=ItemLocation.Loose)continue;
                if((entry.position-fix.previous).sqrMagnitude>.0004f||!AuthoredPose(fix.id,out var pose))continue;
                entry.position=pose.position;entry.rotation=pose.rotation;count++;
            }
            return count;
        }
        // The scene layout captured at Start, before any save was applied.
        bool AuthoredPose(string id,out Pose pose)
        {
            pose=default;
            if(replayLayout!=null&&replayLayout.items!=null)foreach(var entry in replayLayout.items)if(entry!=null&&entry.id==id){if(entry.location!=ItemLocation.Loose)return false;pose=new Pose(entry.position,entry.rotation);return true;}
            foreach(var item in items)if(item&&item.id==id){pose=new Pose(item.transform.position,item.transform.rotation);return true;}
            return false;
        }
    }
}
