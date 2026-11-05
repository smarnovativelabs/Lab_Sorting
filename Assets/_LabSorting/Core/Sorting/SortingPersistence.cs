using System;
using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        DurableSaveWriter saveWriter;
        string writerPath,reportedSaveError;
        float retrySaveAt;
        SortItem[] saveCatalog;
        Entry[] cachedEntries;
        readonly Dictionary<SortItem,int> saveIndices=new Dictionary<SortItem,int>();
        readonly HashSet<SortItem> dirtySaveItems=new HashSet<SortItem>();
        readonly List<SortItem> dynamicSaveItems=new List<SortItem>();
        public bool SavePending=>saveWriter!=null&&saveWriter.Pending;
        public string LastSaveError=>saveWriter==null?null:saveWriter.Error;
        public double LastSaveCaptureMilliseconds {get;private set;}
        public double LastSaveSerializationMilliseconds=>saveWriter==null?0:saveWriter.SerializationMilliseconds;
        public double LastSaveWriteMilliseconds=>saveWriter==null?0:saveWriter.WriteMilliseconds;
        void DirtySaveItem(SortItem item){dirtySaveItems.Add(item);}
        void ClearSaveCache(){if(saveCatalog!=null)foreach(var item in saveCatalog)if(item)item.PersistenceChanged-=DirtySaveItem;saveCatalog=null;cachedEntries=null;saveIndices.Clear();dirtySaveItems.Clear();dynamicSaveItems.Clear();}
        public void InitializeSaveCache()
        {
            ClearSaveCache();saveCatalog=items;cachedEntries=new Entry[items.Length];
            for(int i=0;i<items.Length;i++){saveIndices.Add(items[i],i);cachedEntries[i]=CaptureEntry(items[i]);items[i].PersistenceChanged+=DirtySaveItem;if(items[i].hitbox&&items[i].hitbox.attachedRigidbody&&!items[i].hitbox.attachedRigidbody.isKinematic)dynamicSaveItems.Add(items[i]);}
        }
        Entry CaptureEntry(SortItem item)
        {
            Vector3 position=item.SavedPosition;Quaternion rotation=item.SavedRotation;
            if(item.location==ItemLocation.Rack){position=item.rack.slots[item.slot].position;rotation=item.rack.slots[item.slot].rotation*Quaternion.Euler(item.definition.shelfEuler);}
            return new Entry{id=item.id,rack=item.rack?item.rack.id:null,slot=item.slot,location=item.location,position=position,rotation=rotation};
        }
        Snapshot CaptureSave(bool refreshAll)
        {
            if(saveCatalog!=items)InitializeSaveCache();
            foreach(var item in dynamicSaveItems)if(item)dirtySaveItems.Add(item);
            if(refreshAll){for(int i=0;i<items.Length;i++)cachedEntries[i]=CaptureEntry(items[i]);}
            else foreach(var item in dirtySaveItems)if(item&&saveIndices.TryGetValue(item,out int index))cachedEntries[index]=CaptureEntry(item);
            dirtySaveItems.Clear();
            // Entries are immutable once published. Updating an item replaces its entry.
            var snapshot=new Snapshot{items=new List<Entry>(cachedEntries),searchId=SearchType?SearchType.id:null,searchRemaining=SearchSeconds,completionAcknowledged=CompletionAcknowledged,milestones=new List<string>(claimedMilestones),cash=cash,rewarded=new List<string>(rewardedItems),capacity=capacity,playerPosition=player.transform.position,look=new Vector2(player.look.Yaw,player.look.Pitch),active=Active?Active.id:null};
            foreach(var item in carried)snapshot.hand.Add(item.id);
            return snapshot;
        }
        public bool RequestSave(bool refreshAll=false)
        {
            if(!persistence)return false;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();string path=SavePath;
            if(saveWriter==null||writerPath!=path)
            {
                if(saveWriter!=null&&!saveWriter.Flush()){Debug.LogWarning("Previous save is still pending; cannot change save slot.");return false;}
                saveWriter=new DurableSaveWriter(path);writerPath=path;
            }
            saveWriter.Enqueue(CaptureSave(refreshAll));saveAt=-1;
            LastSaveCaptureMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            return true;
        }
        // Explicit durability boundary: used for pause, quit, explicit saves and load ordering.
        public bool FlushSaves(int timeoutMilliseconds=5000)=>saveWriter==null||saveWriter.Flush(timeoutMilliseconds);
        public void Save(){if(!persistence)return;if(RequestSave(true)&&!FlushSaves())Debug.LogWarning("Sorting save has not completed: "+(LastSaveError??"writer timeout"));}
        void SaveTransaction(){RequestSave();}
        void PollSaveWriter()
        {
            if(saveWriter==null)return;
            if(saveWriter.Error==null){if(reportedSaveError!=null&&!saveWriter.Pending){reportedSaveError=null;Message="Progress saved";MessageIsError=false;Notify();}return;}
            if(reportedSaveError!=saveWriter.Error){reportedSaveError=saveWriter.Error;Message="Save delayed — retrying. Keep the game open.";MessageIsError=true;Notify();Debug.LogWarning("Sorting save failed; retaining latest snapshot: "+reportedSaveError);}
            if(Time.unscaledTime>=retrySaveAt){retrySaveAt=Time.unscaledTime+2;saveWriter.Retry();}
        }
        void OnDestroy(){FlushSaves();ClearSaveCache();}
    }
}
