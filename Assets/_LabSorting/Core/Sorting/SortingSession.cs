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
        public int layoutRevision;
        public bool persistence=true;
        public string saveName="sorting-test-v1.json";
        public readonly List<SortItem> carried=new List<SortItem>(16);
        public SortItem Active { get; private set; }
        public SortItem TargetItem { get; private set; }
        public SortRack TargetRack { get; private set; }
        public string Message { get; private set; }="Pick an item, then assign an empty rack.";
        public event Action Changed;
        public event Action Backgrounded;
        public event Action<SortingCue> Feedback;
        public event Action<SortItem> ItemPlaced;
        public event Action<SortItem> ItemPlacementArrived;
        public event Action<SortItem,Vector3> ItemPicked;
        [Tooltip("Place all carried items of the selected type, with staggered flights to reserved slots.")]
        public bool batchPlacement=true;
        bool settlingPresentation;
        public int Revision { get; private set; }
        public float reach=3.5f;
        float nextAction, saveAt=-1;
        public int SortedCount { get { int count=0; foreach(var item in items) if(item.location==ItemLocation.Rack) count++; return count; } }
        public string SavePath => Path.Combine(Application.persistentDataPath,saveName);
        void Awake()
        {
            foreach(var rack in racks) {rack.group=racks; rack.Initialize();}
            // Clutter remains raycastable, but does not snag the player's capsule.
            RefreshSortingColliders();
        }
        public void RefreshSortingColliders(){player.character.SortingColliders.Clear();foreach(var item in items)if(item&&item.hitbox){item.ApplyItemLayer();player.character.SortingColliders.Add(item.hitbox);}int layer=LayerMask.NameToLayer("SortingItem");if(layer>=0)player.character.Motor.CollidableLayers&=~(1<<layer);}
        public bool Initialized {get;private set;}
        public bool LoadedAtStartup {get;private set;}
        void Start() { foreach(var item in items)if(item)item.ApplyGroundSize(); RebuildTypeIndex();RefreshSortingColliders(); replayLayout=Capture(); bool loaded=persistence&&Load(); LoadedAtStartup=loaded; if(bundleEconomy&&!loaded){cash=150;capacity=10;SearchCredits=5;MagnetCredits=2;} InitializeSaveCache();Initialized=true; Notify(); if(bundleEconomy&&persistence&&!loaded)SaveTransaction(); }
        void Update()
        {
            PollSaveWriter();
            TickSupportChecks();
            UpdateSearch();
            UpdateRackHints();
            if(saveAt>=0 && Time.unscaledTime>=saveAt) { saveAt=-1; RequestSave(); }
            if(player.blocked) { CancelPendingDrop();CancelHold(); ClearTarget(); return; }
            if(player.PickTutorialLocked){CancelHold();CancelPendingDrop();}
            RefreshTarget();RefreshToss();
            var key=Keyboard.current;
            if(key!=null&&!player.PickTutorialLocked)
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
            TickBufferedTap();TickHold();
            TickPendingDrop();
        }
        void ClearTarget() { if(TargetItem) TargetItem.Highlight(false); if(TargetRack) TargetRack.Preview(null); TargetItem=null;TargetRack=null; SetTossRack(null); }
        bool Ready() => !player.blocked && Time.unscaledTime>=nextAction;
        public void PickTarget() { if(Ready()) TryPick(TargetItem); }
        public bool TryPick(SortItem item,bool magnet=false,float delay=0)
        {
            CancelPendingDrop();
            if(player.blocked) return false;
            if(!magnet)foreach(var held in carried)if(held.MagnetFlying)return Fail("Wait for magnet collection to finish");
            if(!item || item.location==ItemLocation.Hand || item.Moving) return Fail("No available item in reach");
            if(item.PickLocked)return Fail("Rack complete! Items are locked");
            if(carried.Count>=capacity) return FullInventoryNotice();
            // Measured before docking disables the collider: whatever rested on this item must not stay in the air.
            item.SetSearchHighlight(false);Physics.SyncTransforms();
            bool fromFloor=item.location==ItemLocation.Loose&&item.hitbox&&item.hitbox.enabled;var vacated=fromFloor?item.hitbox.bounds:default;
            if(item.rack) item.rack.Remove(item);
            item.SetSearchHighlight(false); item.location=ItemLocation.Hand; item.slot=-1; item.rack=null; item.Highlight(false);
            item.GetComponent<LabRat>()?.StopRunning();
            if(Active&&!Active.Moving)Active.gameObject.SetActive(false);
            carried.Add(item); Active=item; item.gameObject.SetActive(true);
            recentPickType=item.definition;recentPickUntil=Time.unscaledTime+.6f;
            if(item.definition.isRat)item.GetComponent<LabRat>()?.colony.PlayInteraction(item);
            var pickupPosition=item.visual?item.visual.bounds.center:item.transform.position;
            item.DockAt(hand,magnet,delay,()=>{if(item&&item.location==ItemLocation.Hand&&Active!=item)item.gameObject.SetActive(false);});
            if(fromFloor)QueueSupportCheck(vacated);
            if(!magnet){Commit("Picked "+item.definition.displayName);ItemPicked?.Invoke(item,pickupPosition); Feedback?.Invoke(SortingCue.Pick);}return true;
        }
        public void Select(int index)
        {
            CancelPendingDrop();
            if(player.blocked || index<0 || index>=carried.Count) return;
            // Selecting used to complete every flight, teleporting the magnet batch.
            foreach(var held in carried)if(held.MagnetFlying)return;
            CancelHold();
            if(Active==carried[index])return;
            foreach(var held in carried){held.CompleteMotion();held.gameObject.SetActive(false);}
            Active=carried[index]; Active.CompleteMotion(); Active.gameObject.SetActive(true); Active.Snap(hand.position,hand.rotation,hand);
            Active.PresentHeld();
            Message="Selected "+Active.definition.displayName;MessageIsError=false;saveAt=Time.unscaledTime+.4f;Notify();
        }
        public void Cycle() { if(carried.Count>0) Select((carried.IndexOf(Active)+1)%carried.Count); }
        void RemoveActive()
        {
            var type=Active.definition;carried.Remove(Active); Active=null;
            for(int i=carried.Count-1;i>=0;i--) if(carried[i].definition==type) {Active=carried[i];break;}
            if(!Active && carried.Count>0) Active=carried[carried.Count-1];
            if(Active) { Active.gameObject.SetActive(true); Active.Snap(hand.position,hand.rotation,hand);Active.PresentHeld(); }
        }
        public void PlaceTarget() { if(Ready()) PlaceMatching(TargetRack); }
        // TryPlace remains a single-item primitive for tutorial, replay and scripted actions.
        public bool TryPlace(SortRack rack) => PlaceOne(rack,0);
        [Tooltip("Smart Place: PLACE on a rack that already holds a carried type selects that type first, so the player skips the inventory.")]
        public bool smartPlace=true;
        // The carried type this rack takes: the selected type first, otherwise a carried type the rack already holds.
        // An empty rack is never claimed for a type the player has not selected.
        public ItemDefinition PlaceableType(SortRack rack)
        {
            if(!rack||!Active)return null;
            if(rack.CanAccept(Active.definition))return Active.definition;
            if(!smartPlace||!rack.Type||!rack.CanAccept(rack.Type))return null;
            ItemDefinition found=null;
            // Select() refuses while a magnet batch is flying, so the rack must not promise a place yet.
            foreach(var held in carried){if(held.MagnetFlying)return null;if(held.definition==rack.Type)found=rack.Type;}
            return found;
        }
        // The carried item of this type that selecting the type shows (the latest, as RemoveActive does).
        public SortItem CarriedOf(ItemDefinition type){for(int i=carried.Count-1;i>=0;i--)if(carried[i].definition==type)return carried[i];return null;}
        void SelectForRack(SortRack rack)
        {
            if(player.blocked)return;
            var type=PlaceableType(rack);if(!type||type==Active.definition)return;
            var item=CarriedOf(type);if(item)Select(carried.IndexOf(item));
        }
        public bool PlaceMatching(SortRack rack)
        {
            SelectForRack(rack);
            if(!batchPlacement)return TryPlace(rack);
            if(player.blocked||!Active||Active.Moving||!rack||!rack.CanAccept(Active.definition))return TryPlace(rack);
            var type=Active.definition;int count=0;
            foreach(var item in carried)if(item.definition==type)count++;
            count=Mathf.Min(count,rack.CapacityFor(type)-rack.Count);
            float interval=count>1?Mathf.Min(.14f,1.5f/(count-1)):0;
            // CarryPresentation offsets the visible model from the raw hand socket to fit the HUD.
            // Every item in this batch must leave that same visible grip, not the unadjusted socket.
            var launch=new Pose(Active.transform.position,Active.transform.rotation);var launchScale=Active.transform.localScale;
            int placed=0;
            while(placed<count&&Active&&Active.definition==type&&rack.CanAccept(type))
            { if(!PlaceOne(rack,placed*interval,launch,launchScale))break;placed++; }
            return placed>0;
        }
        bool PlaceOne(SortRack rack,float delay,Pose? launch=null,Vector3? launchScale=null)
        {
            CancelPendingDrop();
            if(player.blocked) return false;
            if(!Active || Active.Moving) return Fail("Pick an item first");
            if(!rack) return Fail("Aim at a rack");
            if(!rack.CanAccept(Active.definition)) return Fail(RackRefusal(rack,Active.definition));
            var item=Active; int index=rack.Add(item); RemoveActive(); item.transform.SetParent(null,true);
            if(launch.HasValue)item.transform.SetPositionAndRotation(launch.Value.position,launch.Value.rotation);
            if(launchScale.HasValue)item.transform.localScale=launchScale.Value;
            var pose=rack.SlotPose(index); item.PlaceOnRack(pose,hand,delay,()=>
            {
                if(!this||!isActiveAndEnabled||settlingPresentation||player.blocked||item.location!=ItemLocation.Rack)return;
                ItemPlacementArrived?.Invoke(item);
                Feedback?.Invoke(SortingCue.Place);
            });
            bool firstPlacement=rewardedItems.Add(item.id);if(firstPlacement)ItemFirstSorted?.Invoke(rewardedItems.Count);
            ItemPlaced?.Invoke(item);CheckSmallGoals(rack,firstPlacement);
            EndTossStreak();CheckMilestones();Commit("Placed "+item.definition.displayName);return true;
        }
        public static string RackRefusal(SortRack rack,ItemDefinition type)=>
            rack.Type&&rack.Type!=type?"This rack holds "+rack.Type.displayName:
            !rack.Type&&rack.HomeFor(type)?type.displayName+" already has a rack. Follow the marker":
            rack.Count>0?"Rack full":"Too big for this rack";
        public void ThrowActive()
        {
            CancelHold();
            if(player.blocked)return;
            if(!Active) {Fail("Pick an item first");return;}
            foreach(var held in carried)if(held.MagnetFlying)return;
            // DROP is an immediate release, including during the pickup animation/cooldown.
            Active.CompleteMotion();
            // Resolve the current aim at the click, not the target cached by the throttled preview.
            RefreshTarget();RefreshToss();
            var item=Active;
            Vector3 release=default;
            if(!item.definition.isRat&&!TryPhysicsRelease(item,out release)){Fail("Make a little room to drop this item");return;}
            var path=item.definition.isRat?PlanSmoothDrop(item,out _):null;
            CancelPendingDrop();
            RemoveActive(); item.location=ItemLocation.Loose;item.rack=null;item.slot=-1;item.transform.SetParent(null,true);
            if(item.definition.isRat)item.GetComponent<LabRat>()?.colony.PlayInteraction(item,true);
            EndTossStreak();
            if(item.definition.isRat)item.FollowDropArc(path,path[path.Length-1],()=>NotifyLanded(item));
            else item.ReleaseNaturally(release,DropVelocity(),()=>NotifyLanded(item));
            Commit("Dropped "+item.definition.displayName);Feedback?.Invoke(SortingCue.Throw);
        }
        public bool MessageIsError {get;private set;}
        string lastFailure;float nextFailure;
        bool Fail(string reason) { if(reason==lastFailure&&Time.unscaledTime<nextFailure)return false;lastFailure=reason;nextFailure=Time.unscaledTime+.8f;Message=reason;MessageIsError=true; Notify(); Feedback?.Invoke(SortingCue.Reject);return false; }
        void Commit(string message) { lastFailure=null;nextAction=Time.unscaledTime+.24f; Message=message;MessageIsError=false; saveAt=Time.unscaledTime+.4f; Notify(); }
        void Notify() { Revision++; Changed?.Invoke(); }
        [Serializable] public class Snapshot { public int version=3; public string savedAreaId; public int savedContentVersion; public string completedArea; public int completionCount,completedContentVersion; public bool runCompletionRecorded; public int searchCredits,magnetCredits,slotCredits; public List<string> supplyReceipts; public int layoutRevision; public List<string> completedRacks; public string searchId; public float searchRemaining; public bool completionAcknowledged; public List<string> milestones=new List<string>(); public int cash; public List<string> rewarded=new List<string>(); public int capacity; public Vector3 playerPosition; public Vector2 look; public string active; public List<string> hand=new List<string>(); public List<Entry> items=new List<Entry>(); public bool tutorialRewarded; }
        [Serializable] public class Entry { public string id,rack; public int slot; public ItemLocation location; public Vector3 position; public Quaternion rotation; }
        public Snapshot Capture()
        {
            var data=new Snapshot {layoutRevision=layoutRevision,completedRacks=new List<string>(completedRacks),searchId=SearchType?SearchType.id:null,searchRemaining=SearchSeconds,completionAcknowledged=CompletionAcknowledged,milestones=new List<string>(claimedMilestones),cash=cash,rewarded=new List<string>(rewardedItems),capacity=capacity,playerPosition=player.transform.position,look=new Vector2(player.look.Yaw,player.look.Pitch),active=Active?Active.id:null,tutorialRewarded=TutorialRewarded};
            CaptureSupplies(data);foreach(var item in carried) data.hand.Add(item.id);
            foreach(var item in items)
            {
                Quaternion rot=item.SavedRotation; Vector3 pos=item.BasePosition(item.SavedPosition,rot);
                if(item.location==ItemLocation.Rack) {var pose=item.rack.SlotPose(item.slot);pos=pose.position;rot=pose.rotation;}
                data.items.Add(new Entry {id=item.id,rack=item.rack?item.rack.id:null,slot=item.slot,location=item.location,position=pos,rotation=rot});
            }
            return data;
        }
        public bool Restore(Snapshot data)
        {
            // Validate completely before mutating the live session.
            if(data==null || (data.version<1 || data.version>3) || data.items==null || data.hand==null || data.items.Count!=items.Length || data.capacity<10 || data.capacity>60 || data.hand.Count>data.capacity || (data.version>=2 && data.cash<0)) return false;
            if(!ValidSupplies(data)||data.completionCount<0||data.completedContentVersion<0)return false;
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
                    if(e.rack==null || !rackLookup.TryGetValue(e.rack,out var rack)||e.slot<0||e.slot>=rack.CapacityFor(item.definition)||!occupied.Add(e.rack+":"+e.slot)) return false;
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
            var rackRewards=new HashSet<string>(); if(data.completedRacks!=null){foreach(var id in data.completedRacks)if(id==null||!rackLookup.ContainsKey(id)||!rackRewards.Add(id))return false;} else {foreach(var rack in racks){int count=0;foreach(var e in data.items)if(e.location==ItemLocation.Rack&&e.rack==rack.id)count++;int full=types.TryGetValue(rack.id,out var rackType)?rack.CapacityFor(rackType):0;if(full>0&&count==full)rackRewards.Add(rack.id);}}
            supportChecks.Clear();
            RebuildTypeIndex();CancelPendingDrop();RestoreSupplies(data);RestoreCompletionHistory(data);completedRacks=rackRewards;VisualsReset?.Invoke();CompletionAcknowledged=data.version>=3&&data.completionAcknowledged;TutorialRewarded=data.tutorialRewarded;claimedMilestones=milestones;
            lastFailure=null;MessageIsError=false;CancelHold();EndSearch();ResetMagnetFeedback();cash=data.version>=2?data.cash:200;rewardedItems=rewards;
            ClearTarget(); EndTossStreak(); rackBounds.Clear();  foreach(var rack in racks) rack.Initialize(); carried.Clear(); Active=null; capacity=data.capacity;
            // Older purchases granted slot tokens. Apply them once while preserving any overflow.
            if(bundleEconomy&&SlotCredits>0){int applied=Mathf.Min(SlotCredits,Mathf.Max(0,maxCapacity-capacity));capacity+=applied;SlotCredits-=applied;}
            foreach(var e in data.items)
            {
                var item=lookup[e.id];item.gameObject.SetActive(true);item.location=e.location;item.rack=null;item.slot=-1;
                if(e.location==ItemLocation.Rack) {var rack=rackLookup[e.rack]; rack.Restore(item,e.slot);var pose=rack.SlotPose(e.slot);item.Snap(pose.position,pose.rotation);}
                else item.Snap(e.location==ItemLocation.Loose?e.position+item.GroundLift(e.rotation):e.position,e.rotation);
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
            { try {if(File.Exists(path)&&RestoreLoadedLayout(JsonUtility.FromJson<Snapshot>(File.ReadAllText(path)))) {if(path.EndsWith(".bak",StringComparison.Ordinal)){if(saveWriter==null||writerPath!=SavePath){saveWriter=new DurableSaveWriter(SavePath);writerPath=SavePath;}saveWriter.ProtectBackup();}return true;}} catch(Exception e){Debug.LogWarning("Skipping invalid sorting save: "+e.Message);} }
            return false;
        }
        void OnDisable() { CancelPendingDrop();CancelHold(); ClearTarget(); FlushSaves(); }
        void SettleForBackground()
        {
            CancelPendingDrop();CancelHold();ClearTarget();
            // Rare lifecycle boundary: settle in-flight work before the durable snapshot, regardless of callback order.
            settlingPresentation=true;
            try{foreach(var item in items)if(item&&(item.Moving||item.Presenting))item.CompleteMotion();}
            finally{settlingPresentation=false;}
            foreach(var item in carried){item.Snap(hand.position,hand.rotation,hand);item.gameObject.SetActive(item==Active);}
            Backgrounded?.Invoke();Save();
        }
        public void SuspendForConnection(){if(Initialized)SettleForBackground();}
        void OnApplicationFocus(bool value) { if(!value)SettleForBackground(); }
        void OnApplicationPause(bool value) { if(value)SettleForBackground(); }
        void OnApplicationQuit() { Save(); }
    }
}









