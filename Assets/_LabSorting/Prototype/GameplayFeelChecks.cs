#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;

namespace LabSorting.Prototype
{
    public sealed class GameplayFeelChecks : MonoBehaviour
    {
        readonly StringBuilder report=new StringBuilder();
        public bool Finished {get;private set;}
        void Check(bool ok,string message)
        {
            report.AppendLine((ok?"PASS ":"FAIL ")+message);
            Directory.CreateDirectory("Logs/GameplayFeel");File.WriteAllText("Logs/GameplayFeel/report.txt",report.ToString());
            if(!ok)throw new Exception("GAMEPLAY FEEL: "+message);
        }
        static IEnumerator Wait(float seconds)
        {
            // Tweens use scaled game time. An unfocused Editor can advance less game time than wall time.
            float end=Time.time+seconds,deadline=Time.realtimeSinceStartup+30;
            while(Time.time<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Gameplay verification clock stalled");yield return null;}
        }
        public IEnumerator Run()
        {
            Application.runInBackground=true;
            var s=FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            s.persistence=false;s.player.SetBlocked(false);s.player.SetTutorialBlocked(false);s.player.SetNetworkBlocked(false);s.player.SendMessage("OnApplicationFocus",true);
            var initial=s.Capture();var originalRacks=s.racks;
            var root=new GameObject("Temporary real-rack interaction checks");
            var rack=Instantiate(s.racks.First(r=>!r.ratsOnly&&r.SizeBased),root.transform);
            rack.id="feel-check-rack";rack.transform.SetPositionAndRotation(new Vector3(1000,0,1003),Quaternion.identity);rack.Initialize();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(1000,-.15f,1000);floor.transform.localScale=new Vector3(16,.3f,16);
            s.racks=originalRacks.Concat(new[]{rack}).ToArray();
            var chosen=s.items.Where(i=>i.definition.name=="hand-sanitizer-bottles-1"&&i.location==ItemLocation.Loose).Take(5).ToArray();
            var other=s.items.First(i=>i.definition.name=="radio-1"&&i.location==ItemLocation.Loose);
            var f=s.GetComponent<SortingFeedback>();var ghost=s.GetComponent<RackPlacementPreview>();
            Action reset=()=>{s.Restore(initial);rack.Initialize();s.player.SetBlocked(false);s.player.SetTutorialBlocked(false);s.player.SetNetworkBlocked(false);s.player.character.Motor.SetPositionAndRotation(new Vector3(1000,.03f,1000.5f),Quaternion.identity);s.player.Clear();s.player.look.SetYawPitch(Vector2.zero);};
            try
            {
                Check(s.batchPlacement&&chosen.Length==5,"Production default enables matching batch placement");
                reset();yield return Wait(.3f);
                var camera=s.player.view;
                for(int i=0;i<2;i++)chosen[i].Snap(camera.transform.position+camera.transform.forward*1.2f+camera.transform.right*(i==0?-.3f:.3f),Quaternion.identity);
                Physics.SyncTransforms();
                Check(s.TryTouchPick(camera.WorldToScreenPoint(chosen[0].visual.bounds.center)),"Screen tap picks first visible item");
                Check(s.TryTouchPick(camera.WorldToScreenPoint(chosen[1].visual.bounds.center)),"Second rapid screen tap is accepted during pickup cooldown");
                reset();yield return Wait(.3f);
                for(int i=0;i<3;i++){chosen[i].Snap(new Vector3(999.7f+i*.3f,.5f,1001.3f),Quaternion.identity);Check(s.TryPick(chosen[i]),"Rapid pickup accepted "+i);}
                Check(chosen.Take(3).All(i=>i.Moving),"Rapid pickups retain concurrent visible flights");
                yield return Wait(.45f);
                Check(s.carried.Count==3&&chosen.Take(3).Count(i=>i.gameObject.activeSelf)==1&&s.Active==chosen[2],"Arrival leaves exactly the newest held item visible");
                Check(f.PickupBursts>=3,"Pickup feedback receives real interaction positions");
                s.player.look.SnapToDirection(rack.transform.TransformPoint(0,.95f,0)-s.player.view.transform.position);yield return Wait(.2f);s.RefreshTarget();
                Check(s.TargetRack==rack&&ghost.Visible,"Existing ghost preview remains visible on actual rack geometry");
                var preview=ghost.PreviewPose;int arrivals=f.PlacementArrivals;
                var localGrip=s.hand.InverseTransformPoint(s.Active.transform.position);
                s.BeginHold(true);s.EndHold(true);
                Check(rack.Count==3&&s.carried.Count==0&&!s.Holding,"Place button path reserves matching batch once, without hold/release duplicates");
                Check(chosen.Take(3).Select(i=>i.slot).Distinct().Count()==3,"Batch assigns unique real rack slots");
                Check(Vector3.Distance(rack.SlotPose(chosen[2].slot).position,preview.position)<.001f,"First placement uses the ghost slot");
                yield return Wait(.16f);
                Check(Vector3.Distance(chosen[1].transform.position,s.hand.TransformPoint(localGrip))<.35f,"Staggered batch launches from visible grip instead of raw hand socket");
                Check(f.PlacementArrivals==arrivals,"Placement feedback waits for physical arrival");
                var pending=s.Capture();Check(pending.items.Where(e=>chosen.Take(3).Any(i=>i.id==e.id)).All(e=>e.location==ItemLocation.Rack),"Mid-batch snapshot records reserved rack destinations");
                yield return Wait(1.1f);
                Check(f.PlacementArrivals==arrivals+3&&chosen.Take(3).All(i=>!i.Moving&&i.hitbox.enabled),"Each arrival fires once and restores collision");
                foreach(var item in chosen.Take(3))Check(Vector3.Distance(item.transform.position,rack.SlotPose(item.slot).position)<.001f,"Final pose matches slot "+item.slot);
                File.WriteAllText("Logs/GameplayFeel/inflight-save.json",JsonUtility.ToJson(pending));
                var disk=JsonUtility.FromJson<SortingSession.Snapshot>(File.ReadAllText("Logs/GameplayFeel/inflight-save.json"));
                Check(s.Restore(disk)&&s.carried.Count==0&&rack.Count==3,"Disk round-trip of in-flight batch has no missing or duplicated items");
                yield return Wait(.45f);Check(f.PlacementArrivals==arrivals+3,"Restoring does not replay arrival feedback");

                reset();yield return Wait(.3f);
                Check(s.TryPick(other),"Pick a different category");foreach(var item in chosen.Take(3))Check(s.TryPick(item),"Pick matching batch alongside other category");
                yield return Wait(.4f);Check(s.PlaceMatching(rack),"Place matching subset");
                Check(s.carried.Count==1&&s.Active==other,"Other categories remain held");
                yield return Wait(.9f);int count=rack.Count;
                Check(!s.PlaceMatching(rack)&&rack.Count==count&&s.Active==other,"Wrong-category rack rejects without consuming inventory");
                reset();yield return Wait(.3f);
                var slots=rack.slots;var shelves=rack.shelves;
                var point=new GameObject("Single-slot fixture").transform;point.SetParent(rack.transform,false);point.localPosition=new Vector3(0,.8f,0);
                rack.shelves=Array.Empty<RackShelf>();rack.slots=new[]{point};rack.Initialize();
                foreach(var item in chosen.Take(3))s.TryPick(item);yield return Wait(.4f);
                int celebrations=f.Celebrations;Check(s.PlaceMatching(rack)&&rack.Count==1&&s.carried.Count==2,"Limited capacity places only fitting items");
                Check(f.Celebrations==celebrations,"Rack completion waits for arrival");
                yield return Wait(.65f);Check(f.Celebrations==celebrations+1,"Completion celebration runs after final arrival");
                rack.shelves=shelves;rack.slots=slots;Destroy(point.gameObject);

                reset();yield return Wait(.3f);foreach(var item in chosen.Take(3))s.TryPick(item);yield return Wait(.4f);
                arrivals=f.PlacementArrivals;s.PlaceMatching(rack);s.SendMessage("OnApplicationPause",true);
                Check(chosen.Take(3).All(i=>!i.Moving&&i.hitbox.enabled)&&rack.Count==3,"Backgrounding completes all reserved transfers");
                Check(f.PlacementArrivals==arrivals,"Background completion suppresses late presentation cues");
                reset();yield return Wait(.3f);
                s.TryPick(chosen[0]);s.ThrowActive();
                Check(s.carried.Count==0&&chosen[0].location==ItemLocation.Loose,"Drop during pickup releases exactly once");
                yield return Wait(2.2f);Check(!chosen[0].Moving&&chosen[0].hitbox.enabled&&DropLandingPool.ActiveCount==0,"Immediate drop settles with no leaked physics body");
                reset();yield return Wait(.3f);
                for(int i=0;i<5;i++){s.TryPick(chosen[i]);s.ThrowActive();}
                yield return Wait(2.3f);
                Check(s.carried.Count==0&&chosen.All(i=>i.location==ItemLocation.Loose&&!i.Moving&&i.hitbox.enabled)&&DropLandingPool.ActiveCount==0,"Five rapid drops complete full-flight and fallback paths");
                reset();
                report.AppendLine("CORE_CASES_PASS");File.WriteAllText("Logs/GameplayFeel/report.txt",report.ToString());
            }
            finally{s.racks=originalRacks;s.Restore(initial);s.InitializeSaveCache();Destroy(root);}
            yield return Wait(.3f);
            yield return FullFlightChecks.Run();Check(true,"Existing full-flight wall/crowding/save/pause regression checks");
            yield return SoftInteractionChecks.Run();Check(true,"Existing targeting, hidden actions and repeated drop regression checks");
            Finished=true;Check(true,"ALL_GAMEPLAY_FEEL_CHECKS_PASS");
        }
    }
}
#endif
