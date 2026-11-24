#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace LabSorting.Sorting
{
    public static class RatGameplayChecks
    {
        static IEnumerator Wait(float seconds){float until=Time.time+seconds,deadline=Time.realtimeSinceStartup+90;while(Time.time<until){if(Time.realtimeSinceStartup>deadline)throw new Exception("Rat test game clock stopped");yield return null;}}
        static void Check(bool value,string message){if(!value)throw new Exception("RAT CHECK: "+message);Debug.Log("RAT PASS: "+message);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var colony=UnityEngine.Object.FindFirstObjectByType<RatColony>();
            yield return null;s.player.blocked=false;
            var initial=s.Capture();var saveName=s.saveName;bool persistence=s.persistence;
            try
            {
                Check(colony&&colony.rats.Length==10,"Exactly ten rats");
                var rats=colony.rats.Select(r=>r.Item).ToArray();var glass=s.racks.Single(r=>r.ratsOnly);
                Check(rats.Select(r=>r.id).Distinct().Count()==10,"Unique persistent IDs");
                Check(colony.rats.All(r=>Vector3.Distance(r.animator.transform.localScale,Vector3.one)<.001f),"Rat FBX models retain original scale one");
                Check(rats.All(r=>r.definition.size.z>.4f&&Vector3.Distance(((BoxCollider)r.hitbox).size,r.definition.size)<.001f),"Full-size rat colliders match model bounds");
                var containerBounds=glass.GetComponent<Renderer>().bounds;
                Check(containerBounds.min.y>3,"Container sits on upper landing");
                Check(s.racks.Where(r=>r!=glass).All(r=>!r.GetComponentsInChildren<Collider>().Any(c=>c.bounds.Intersects(containerBounds))),"Container does not overlap other racks");
                var slotBounds=new Bounds[10];
                for(int i=0;i<10;i++)
                {
                    var pose=glass.SlotPoseFor(rats[0].definition,i);var local=SortRack.RotatedBounds(rats[0].definition,pose.rotation);
                    slotBounds[i]=new Bounds(pose.position+local.center,local.size);
                    Check(containerBounds.Contains(slotBounds[i].min)&&containerBounds.Contains(slotBounds[i].max),"Full-size rat fits inside container slot "+i);
                    for(int j=0;j<i;j++)Check(!slotBounds[i].Intersects(slotBounds[j]),"Container slots do not overlap "+j+"/"+i);
                }
                Check(Vector3.Distance(glass.GetComponent<Collider>().bounds.size,containerBounds.size)<.01f,"Scaled container collider matches glass bounds");
                Check(glass.CapacityFor(rats[0].definition)==10,"Glass container capacity ten");
                Check(!glass.CanAccept(s.items.First(i=>!i.definition.isRat).definition),"Container rejects ordinary items");
                Check(s.racks.Where(r=>!r.ratsOnly).All(r=>r.CapacityFor(rats[0].definition)==0),"Normal racks reject rats");
                Check(colony.AmbientVoiceCount==2&&colony.GetComponentsInChildren<AudioSource>().Length==3,"Two shared ambient voices and one interaction voice");
                foreach(var voice in colony.GetComponentsInChildren<AudioSource>())Check(voice.spatialBlend==1&&voice.maxDistance==8&&voice.dopplerLevel==0,"Spatial audio bounded to eight meters");
                Check(colony.rats.All(r=>r.animator&&r.animator.runtimeAnimatorController),"Idle/run animators assigned");
                var positions=rats.Select(r=>r.transform.position).ToArray();
                yield return Wait(4);
                Check(rats.Where((r,i)=>Vector3.Distance(r.transform.position,positions[i])>.05f).Any(),"Loose rats roam; blocked="+s.player.blocked+" initialized="+s.Initialized+" enabled="+colony.rats[0].enabled+" moving="+rats[0].Moving+" location="+rats[0].location+" speed="+colony.rats[0].speed+" step="+colony.TryStep(colony.rats[0],rats[0].transform.position+Vector3.forward*.02f,out var probe)+" pos="+rats[0].transform.position);
                foreach(var rat in rats){Check(s.TryPick(rat),"Pick "+rat.id);rat.CompleteMotion();}
                Check(s.carried.Count==10,"Ten rats held in inventory");
                var heldRadius=s.Active.definition.size.magnitude*.5f+s.Active.definition.center.magnitude;
                Check(heldRadius*s.Active.CarryScale<=.181f&&s.Active.CarryScale>.3f,"Held rat stays visible within hand clearance envelope");
                foreach(var rat in rats){Check(s.TryPlace(glass),"Place into glass");rat.CompleteMotion();}
                Check(glass.Count==10&&!glass.CanAccept(rats[0].definition),"Container full at 10/10");
                foreach(var rat in rats)rat.CompleteMotion();
                yield return Wait(.15f);
                Check(colony.rats.All(r=>!r.animator.GetBool("Running")),"Captured rats use idle animation");
                var enclosure=glass.GetComponent<Renderer>().bounds;
                s.player.character.Motor.SetPosition(new Vector3(enclosure.center.x-1.4f,enclosure.min.y+.03f,enclosure.center.z));
                yield return Wait(.1f);s.player.look.SnapToDirection(enclosure.center-s.player.view.transform.position);Physics.SyncTransforms();s.RefreshTarget();
                Check(s.TargetRack==glass&&s.TargetItem&&s.TargetItem.rack==glass,"Glass aim targets captured rat for retrieval");
                var full=s.Capture();Check(s.Restore(full)&&glass.Count==10,"Full container snapshot restores");
                Check(s.TryPick(rats[0]),"Re-pick captured rat");rats[0].CompleteMotion();
                Check(glass.Count==9,"Re-pick releases container slot");
                s.player.character.Motor.SetPosition(new Vector3(0,.03f,-8.1f));s.player.look.SetYawPitch(Vector2.zero);
                yield return Wait(.3f);
                Check(s.TryThrowPosition(rats[0],out var destination),"Safe throw landing is available");
                s.ThrowActive();yield return Wait(2);
                Check(rats[0].location==ItemLocation.Loose&&!rats[0].Moving&&rats[0].hitbox.enabled,"Thrown rat lands and becomes pickable");
                Check(Vector3.Distance(rats[0].transform.localScale,Vector3.one)<.001f,"Throw restores full world size");
                var landedPosition=rats[0].transform.position;yield return Wait(4);
                Check(Vector3.Distance(landedPosition,rats[0].transform.position)>.05f,"Thrown rat resumes roaming");
                Check(!colony.TryStep(colony.rats[0],new Vector3(100,0,100),out _),"Rats cannot leave room bounds");
                Check(s.TryPick(rats[1]),"Hold another rat for save test");rats[1].CompleteMotion();
                s.saveName="rat-system-test-"+Guid.NewGuid().ToString("N")+".json";s.persistence=true;
                s.Save();Check(!s.SavePending&&s.LastSaveError==null,"Durable save completes");
                Check(s.TryPlace(glass),"Mutate state after save");rats[1].CompleteMotion();
                Check(s.Load(),"Disk save loads");Check(glass.Count==8&&s.carried.Contains(rats[1])&&rats[0].location==ItemLocation.Loose,"Mixed floor/hand/container state preserved");
                var legacy=s.Capture();legacy.savedContentVersion=0;legacy.items.RemoveAll(e=>e.id.StartsWith("lab-rat-"));legacy.hand.Clear();legacy.active=null;legacy.rewarded.RemoveAll(id=>id.StartsWith("lab-rat-"));legacy.completedRacks.Remove(glass.id);
                Check(s.RestoreLoadedLayout(legacy),"Existing saves gain ten rats");Check(rats.All(r=>r.location==ItemLocation.Loose),"Migrated rats start loose");
                var partial=s.Capture();partial.items.RemoveAll(e=>e.id==rats[0].id);
                Check(!s.RestoreLoadedLayout(partial)&&partial.items.Count==s.items.Length-1,"Missing rat in modern save is rejected without silent recreation");
                var missingOrdinary=s.Capture();missingOrdinary.items.RemoveAt(0);
                Check(!s.RestoreLoadedLayout(missingOrdinary),"Missing ordinary item is rejected");
                var sources=colony.GetComponentsInChildren<AudioSource>();
                colony.feedback.SetSound(true,false);colony.SendMessage("OnApplicationFocus",true);
                foreach(var source in sources){source.clip=colony.squeak;source.Play();}
                colony.SendMessage("OnApplicationPause",true);
                Check(sources.All(a=>!a.isPlaying)&&!colony.Audible,"Application pause silences all rat voices");
                colony.SendMessage("OnApplicationPause",false);
                foreach(var source in sources)source.Play();
                colony.SendMessage("OnApplicationFocus",false);
                Check(sources.All(a=>!a.isPlaying)&&!colony.Audible,"Focus loss silences all rat voices");
                colony.SendMessage("OnApplicationFocus",true);
                colony.feedback.SetSound(false,false);yield return null;Check(colony.GetComponentsInChildren<AudioSource>().All(a=>!a.isPlaying),"Sound setting silences rat voices");colony.feedback.SetSound(true,false);
                Debug.Log("RAT_GAMEPLAY_CHECKS_PASSED");
            }
            finally
            {
                s.persistence=false;s.FlushSaves();s.saveName=saveName;s.Restore(initial);s.InitializeSaveCache();s.persistence=persistence;
            }
        }
    }
}
#endif


