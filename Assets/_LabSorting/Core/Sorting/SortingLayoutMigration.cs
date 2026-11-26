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
                        data.items.Add(new Entry{id=item.id,slot=-1,location=ItemLocation.Loose,position=item.BasePosition(item.transform.position,item.transform.rotation),rotation=item.transform.rotation});
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
                foreach(var item in items)poses[item.id]=new Pose(item.BasePosition(item.transform.position,item.transform.rotation),item.transform.rotation);
                foreach(var entry in data.items)if(entry!=null&&entry.location==ItemLocation.Loose&&poses.TryGetValue(entry.id,out var pose)){entry.position=pose.position;entry.rotation=pose.rotation;}
                data.layoutRevision=layoutRevision;
            }
            int relocated=migrate?0:ApplyLayoutFixes(data);
            int merged=MergeSplitTypes(data);
            bool restored=Restore(data);
            // Anything an older build left hanging in the air is set down before the first frame shows it.
            int settled=restored?SettleFloatingLooseItems():0;
            LastLoadRelocated=restored?relocated:0;LastLoadSettled=settled;LastLoadMerged=restored?merged:0;
            if(restored&&merged>0){Message="Racks organized: matching items moved together";MessageIsError=false;Notify();}
            if(restored&&(added||migrate||relocated>0||settled>0||merged>0))saveAt=Time.unscaledTime+.4f;return restored;
        }
        public int LastLoadMerged {get;private set;}
        // Older builds let one type spread over several racks. Each type now has one home rack, and every
        // type's count fits one rack, so a split would leave another type with no rack. Move the items into
        // the fullest of their racks; items, rewards and progress are unchanged, only their shelf moves.
        int MergeSplitTypes(Snapshot data)
        {
            if(data==null||data.items==null)return 0;
            var itemLookup=new Dictionary<string,SortItem>(items.Length);foreach(var item in items)if(item&&item.id!=null)itemLookup[item.id]=item;
            var rackLookup=new Dictionary<string,SortRack>();foreach(var rack in racks)if(rack&&rack.id!=null)rackLookup[rack.id]=rack;
            var byType=new Dictionary<ItemDefinition,Dictionary<string,List<Entry>>>();
            foreach(var e in data.items)
            {
                if(e==null||e.location!=ItemLocation.Rack||e.rack==null||!itemLookup.TryGetValue(e.id,out var item)||!rackLookup.ContainsKey(e.rack))continue;
                if(!byType.TryGetValue(item.definition,out var perRack))byType[item.definition]=perRack=new Dictionary<string,List<Entry>>();
                if(!perRack.TryGetValue(e.rack,out var list))perRack[e.rack]=list=new List<Entry>();
                list.Add(e);
            }
            int moved=0;
            foreach(var pair in byType)
            {
                var perRack=pair.Value;if(perRack.Count<2)continue;
                var type=pair.Key;var order=new List<string>(perRack.Keys);
                order.Sort((a,b)=>{int c=perRack[b].Count.CompareTo(perRack[a].Count);return c!=0?c:string.CompareOrdinal(a,b);});
                var taken=new Dictionary<string,bool[]>();
                foreach(var id in order)
                {
                    var used=new bool[Mathf.Max(0,rackLookup[id].CapacityFor(type))];
                    foreach(var e in perRack[id])if(e.slot>=0&&e.slot<used.Length)used[e.slot]=true;
                    taken[id]=used;
                }
                // Empty the smallest racks first, into the fullest rack that still has room.
                for(int donor=order.Count-1;donor>0;donor--)
                {
                    var from=perRack[order[donor]];
                    for(int k=from.Count-1;k>=0;k--)
                    {
                        for(int home=0;home<donor;home++)
                        {
                            var used=taken[order[home]];int slot=System.Array.IndexOf(used,false);if(slot<0)continue;
                            var e=from[k];used[slot]=true;if(e.slot>=0&&e.slot<taken[order[donor]].Length)taken[order[donor]][e.slot]=false;
                            e.rack=order[home];e.slot=slot;from.RemoveAt(k);perRack[order[home]].Add(e);moved++;break;
                        }
                    }
                }
                // A rack this filled counts as completed, with the bonus the player would have earned placing it.
                if(data.completedRacks!=null)foreach(var id in order)
                {
                    int capacity=rackLookup[id].CapacityFor(type);
                    if(capacity>0&&perRack[id].Count==capacity&&!data.completedRacks.Contains(id)){data.completedRacks.Add(id);data.cash+=RackBonus;}
                }
            }
            return moved;
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
            foreach(var item in items)if(item&&item.id==id){pose=new Pose(item.BasePosition(item.transform.position,item.transform.rotation),item.transform.rotation);return true;}
            return false;
        }
    }
}
