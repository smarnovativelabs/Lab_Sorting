using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using LabSorting.Player;

namespace LabSorting.Sorting
{
    public sealed partial class SortingSession : MonoBehaviour
    {
        public LabPlayerInput player;
        public Transform hand;
        public SortItem[] items;
        public SortRack[] racks;
        public int capacity=10;
        public bool persistence=true;
        public string saveName="sorting-test-v1.json";
        public readonly List<SortItem> carried=new List<SortItem>(16);
        public SortItem Active { get; private set; }
        public SortItem TargetItem { get; private set; }
        public SortRack TargetRack { get; private set; }
        public string Message { get; private set; }="Pick an item, then assign an empty rack.";
        public event Action Changed;
        public event Action<SortingCue> Feedback;
        public int Revision { get; private set; }
        public float reach=3.5f;
        float nextAction, saveAt=-1;
        public int SortedCount { get { int count=0; foreach(var item in items) if(item.location==ItemLocation.Rack) count++; return count; } }
        public string SavePath => Path.Combine(Application.persistentDataPath,saveName);
        void Awake()
        {
            foreach(var rack in racks) rack.Initialize();
            // Clutter remains raycastable, but does not snag the player's capsule.
            RefreshSortingColliders();
        }
        public void RefreshSortingColliders(){player.character.SortingColliders.Clear();foreach(var item in items)if(item&&item.hitbox)player.character.SortingColliders.Add(item.hitbox);}
        void Start() { if(persistence) Load(); InitializeSaveCache(); Notify(); }
        void Update()
        {
            PollSaveWriter();
            UpdateSearch();
            if(saveAt>=0 && Time.unscaledTime>=saveAt) { saveAt=-1; RequestSave(); }
            if(player.blocked) { CancelHold(); ClearTarget(); return; }
            RefreshTarget();
            var key=Keyboard.current;
            if(key!=null)
            {
                if(key.eKey.wasPressedThisFrame) BeginHold(false);
                if(key.fKey.wasPressedThisFrame) BeginHold(true);
                if(key.eKey.wasReleasedThisFrame) EndHold(false);
                if(key.fKey.wasReleasedThisFrame) EndHold(true);
                if(key.qKey.wasPressedThisFrame) ThrowActive();
                if(key.tabKey.wasPressedThisFrame) Cycle();
                if(key.rKey.wasPressedThisFrame) UseSearch();
                if(key.mKey.wasPressedThisFrame) UseMagnet();
                if(key.uKey.wasPressedThisFrame) UpgradeCapacity();
            }
            TickHold();
        }
        void ClearTarget() { if(TargetItem) TargetItem.Highlight(false); if(TargetRack) TargetRack.Preview(null); TargetItem=null;TargetRack=null; }
        bool Ready() => !player.blocked && Time.unscaledTime>=nextAction;
        public void PickTarget() { if(Ready()) TryPick(TargetItem); }
        public bool TryPick(SortItem item)
        {
            if(player.blocked) return false;
            if(!item || item.location==ItemLocation.Hand || item.Moving) return Fail("No available item in reach");
            if(carried.Count>=capacity) return Fail("Hands full — place or throw an item");
            if(item.rack) item.rack.Remove(item);
            item.SetSearchHighlight(false); item.location=ItemLocation.Hand; item.slot=-1; item.rack=null; item.Highlight(false);
            if(Active) Active.gameObject.SetActive(false);
            carried.Add(item); Active=item; item.gameObject.SetActive(true);
            item.DockAt(hand);
            Commit("Picked "+item.definition.displayName); Feedback?.Invoke(SortingCue.Pick);return true;
        }
        public void Select(int index)
        {
            if(player.blocked || index<0 || index>=carried.Count) return;
            CancelHold();
            if(Active) { Active.CompleteMotion(); Active.gameObject.SetActive(false); }
            Active=carried[index]; Active.CompleteMotion(); Active.gameObject.SetActive(true); Active.Snap(hand.position,hand.rotation,hand);
            Message="Selected "+Active.definition.displayName;MessageIsError=false;saveAt=Time.unscaledTime+.4f;Notify();
        }
        public void Cycle() { if(carried.Count>0) Select((carried.IndexOf(Active)+1)%carried.Count); }
        void RemoveActive()
        {
            var type=Active.definition;carried.Remove(Active); Active=null;
            for(int i=carried.Count-1;i>=0;i--) if(carried[i].definition==type) {Active=carried[i];break;}
            if(!Active && carried.Count>0) Active=carried[carried.Count-1];
            if(Active) { Active.gameObject.SetActive(true); Active.Snap(hand.position,hand.rotation,hand); }
        }
        public void PlaceTarget() { if(Ready()) TryPlace(TargetRack); }
        public bool TryPlace(SortRack rack)
        {
            if(player.blocked) return false;
            if(!Active || Active.Moving) return Fail("Pick an item first");
            if(!rack) return Fail("Aim at a rack");
            if(!rack.CanAccept(Active.definition)) return Fail(rack.Count>=rack.slots.Length?"Rack full":"This rack holds "+rack.Type.displayName);
            var item=Active; int index=rack.Add(item); RemoveActive(); item.transform.SetParent(null,true);
            item.Travel(rack.slots[index].position,rack.slots[index].rotation*Quaternion.Euler(item.definition.shelfEuler));
            if(rewardedItems.Add(item.id)) cash+=placementReward;
            CheckMilestones();Commit("Placed "+item.definition.displayName); Feedback?.Invoke(SortingCue.Place);return true;
        }
        public void ThrowActive()
        {
            CancelHold();
            if(!Ready()) return;
            if(!Active) {Fail("Pick an item first");return;}
            if(Active.Moving) {Fail("Wait for pickup to finish");return;}
            var item=Active;
            if(!TryThrowPosition(item,out var position)) {Fail("No safe throw space — step back or aim elsewhere");return;}
            RemoveActive(); item.location=ItemLocation.Loose;item.rack=null;item.slot=-1;item.transform.SetParent(null,true);
            item.transform.rotation=Quaternion.identity;item.transform.localScale=item.originalScale;
            item.Travel(position,Quaternion.identity,drop:true); Commit("Dropped "+item.definition.displayName);Feedback?.Invoke(SortingCue.Throw);
        }
        public bool MessageIsError {get;private set;}
        string lastFailure;float nextFailure;
        bool Fail(string reason) { if(reason==lastFailure&&Time.unscaledTime<nextFailure)return false;lastFailure=reason;nextFailure=Time.unscaledTime+.8f;Message=reason;MessageIsError=true; Notify(); Feedback?.Invoke(SortingCue.Reject);return false; }
        void Commit(string message) { lastFailure=null;nextAction=Time.unscaledTime+.24f; Message=message;MessageIsError=false; saveAt=Time.unscaledTime+.4f; Notify(); }
        void Notify() { Revision++; Changed?.Invoke(); }
        [Serializable] public class Snapshot { public int version=3; public string searchId; public float searchRemaining; public bool completionAcknowledged; public List<string> milestones=new List<string>(); public int cash; public List<string> rewarded=new List<string>(); public int capacity; public Vector3 playerPosition; public Vector2 look; public string active; public List<string> hand=new List<string>(); public List<Entry> items=new List<Entry>(); }
        [Serializable] public class Entry { public string id,rack; public int slot; public ItemLocation location; public Vector3 position; public Quaternion rotation; }
        public Snapshot Capture()
        {
            var data=new Snapshot {searchId=SearchType?SearchType.id:null,searchRemaining=SearchSeconds,completionAcknowledged=CompletionAcknowledged,milestones=new List<string>(claimedMilestones),cash=cash,rewarded=new List<string>(rewardedItems),capacity=capacity,playerPosition=player.transform.position,look=new Vector2(player.look.Yaw,player.look.Pitch),active=Active?Active.id:null};
            foreach(var item in carried) data.hand.Add(item.id);
            foreach(var item in items)
            {
                Vector3 pos=item.SavedPosition; Quaternion rot=item.SavedRotation;
                if(item.location==ItemLocation.Rack) {pos=item.rack.slots[item.slot].position;rot=item.rack.slots[item.slot].rotation*Quaternion.Euler(item.definition.shelfEuler);}
                data.items.Add(new Entry {id=item.id,rack=item.rack?item.rack.id:null,slot=item.slot,location=item.location,position=pos,rotation=rot});
            }
            return data;
        }
        public bool Restore(Snapshot data)
        {
            // Validate completely before mutating the live session.
            if(data==null || (data.version<1 || data.version>3) || data.items==null || data.hand==null || data.items.Count!=items.Length || data.capacity<10 || data.capacity>30 || data.hand.Count>data.capacity || (data.version>=2 && data.cash<0)) return false;
            if(float.IsNaN(data.searchRemaining)||float.IsInfinity(data.searchRemaining)||data.searchRemaining<0||data.searchRemaining>3600)return false;
            ItemDefinition savedSearch=null;if(!string.IsNullOrEmpty(data.searchId)){foreach(var candidate in items)if(candidate.definition.id==data.searchId){savedSearch=candidate.definition;break;}if(!savedSearch)return false;}
            var lookup=new Dictionary<string,SortItem>(); foreach(var item in items) lookup.Add(item.id,item);
            var rackLookup=new Dictionary<string,SortRack>(); foreach(var rack in racks) rackLookup.Add(rack.id,rack);
            var seen=new HashSet<string>(); var occupied=new HashSet<string>(); var types=new Dictionary<string,ItemDefinition>(); var held=new HashSet<string>(data.hand);
            if(held.Count!=data.hand.Count) return false;
            foreach(var e in data.items)
            {
                if(!lookup.TryGetValue(e.id,out var item)||!seen.Add(e.id)||!Enum.IsDefined(typeof(ItemLocation),e.location)) return false;
                if((e.location==ItemLocation.Hand)!=held.Contains(e.id)) return false;
                if(e.location==ItemLocation.Rack)
                {
                    if(e.rack==null || !rackLookup.TryGetValue(e.rack,out var rack)||e.slot<0||e.slot>=rack.slots.Length||!occupied.Add(e.rack+":"+e.slot)) return false;
                    if(types.TryGetValue(e.rack,out var type)&&type!=item.definition) return false;
                    types[e.rack]=item.definition;
                }
            }
            foreach(var id in held) if(!lookup.ContainsKey(id)) return false;
            var rewards=new HashSet<string>();
            if(data.version>=2) {if(data.rewarded==null) return false;foreach(var id in data.rewarded) if(id==null || !lookup.ContainsKey(id) || !rewards.Add(id)) return false;}
            else foreach(var e in data.items) if(e.location==ItemLocation.Rack) rewards.Add(e.id);
            if(held.Count>0&&!held.Contains(data.active??"")) return false;
            var milestones=new HashSet<string>();
            if(data.version>=3){if(data.milestones==null)return false;foreach(var id in data.milestones)if(id==null||Array.IndexOf(milestoneIds,id)<0||!milestones.Add(id))return false;}
            VisualsReset?.Invoke();CompletionAcknowledged=data.version>=3&&data.completionAcknowledged;claimedMilestones=milestones;
            lastFailure=null;MessageIsError=false;CancelHold();EndSearch();cash=data.version>=2?data.cash:200;rewardedItems=rewards;
            ClearTarget(); foreach(var rack in racks) rack.Initialize(); carried.Clear(); Active=null; capacity=data.capacity;
            foreach(var e in data.items)
            {
                var item=lookup[e.id];item.gameObject.SetActive(true);item.location=e.location;item.rack=null;item.slot=-1;
                if(e.location==ItemLocation.Rack) {var rack=rackLookup[e.rack]; rack.Restore(item,e.slot);item.Snap(rack.slots[e.slot].position,rack.slots[e.slot].rotation*Quaternion.Euler(item.definition.shelfEuler));}
                else item.Snap(e.position,e.rotation);
            }
            foreach(var id in data.hand) {var item=lookup[id];carried.Add(item);item.Snap(hand.position,hand.rotation,hand);item.gameObject.SetActive(false);}
            if(carried.Count>0) {Active=lookup[data.active];Active.gameObject.SetActive(true);}
            if(savedSearch&&data.searchRemaining>0){searchType=savedSearch;searchUntil=Time.unscaledTime+data.searchRemaining;foreach(var item in items)if(item.location==ItemLocation.Loose&&item.definition==savedSearch)item.SetSearchHighlight(true);}
            player.character.Motor.SetPosition(data.playerPosition);player.look.SetYawPitch(data.look);if(data.version<3)CheckMilestones(true);Notify();return true;
        }
        public bool Load()
        {
            if(!FlushSaves()){Debug.LogWarning("Cannot load while the latest save is not durable.");return false;}
            foreach(var path in new[]{SavePath,SavePath+".bak"})
            { try {if(File.Exists(path)&&Restore(JsonUtility.FromJson<Snapshot>(File.ReadAllText(path)))) {if(path.EndsWith(".bak",StringComparison.Ordinal)){if(saveWriter==null||writerPath!=SavePath){saveWriter=new DurableSaveWriter(SavePath);writerPath=SavePath;}saveWriter.ProtectBackup();}return true;}} catch(Exception e){Debug.LogWarning("Skipping invalid sorting save: "+e.Message);} }
            return false;
        }
        void OnDisable() { CancelHold(); ClearTarget(); FlushSaves(); }
        void OnApplicationFocus(bool value) { if(!value) {CancelHold();Save();} }
        void OnApplicationPause(bool value) { if(value) { CancelHold(); Save(); } }
        void OnApplicationQuit() { Save(); }
    }
}







