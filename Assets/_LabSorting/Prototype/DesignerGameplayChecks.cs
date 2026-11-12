using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using LabSorting.Sorting;
using UnityEngine;
using Unity.Profiling;

namespace LabSorting.Prototype
{
    public static class DesignerGameplayChecks
    {
        static int checks;
        static void Check(bool ok,string message){if(!ok)throw new Exception("DESIGNER CHECK FAILED: "+message);checks++;Debug.Log("DESIGNER PASS: "+message);}
        static IEnumerator Wait(float seconds)
        {
            float end=Time.time+seconds,deadline=Time.realtimeSinceStartup+30;
            while(Time.time<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Designer player loop stopped");yield return null;}
        }
        static IEnumerator Motion(SortItem item)
        {
            float deadline=Time.realtimeSinceStartup+10;
            do{yield return null;if(Time.realtimeSinceStartup>deadline)throw new Exception("Designer animation timeout");}while(item.Moving);
        }
        static IEnumerator Aim(SortingSession s,Vector3 feet,Vector3 target)
        {
            s.player.Clear();s.player.character.Motor.BaseVelocity=Vector3.zero;s.player.character.Motor.SetPositionAndRotation(feet,Quaternion.identity);
            yield return Wait(.12f);
            // A teleported KCC settles/interpolates its eye position over subsequent player frames.
            // Track the target during that settling, as a player correcting their aim would.
            float end=Time.time+.2f,deadline=Time.realtimeSinceStartup+10;
            while(Time.time<end)
            {
                if(Time.realtimeSinceStartup>deadline)throw new Exception("Designer aiming player loop stopped");
                s.player.look.SnapToDirection(target-s.player.view.transform.position);yield return null;
            }
            yield return Wait(.05f);
        }
        // A standing spot about a metre from a loose item with a clear line of sight to it (piles hide some items).
        static float eyeHeight=1.6f;
        static bool ViewPoint(SortItem item,out Vector3 feet)
        {
            var center=item.hitbox.bounds.center;float floor=item.hitbox.bounds.min.y;
            foreach(float distance in new[]{1.1f,1.5f})
            for(int d=0;d<24;d++)
            {
                float angle=d*Mathf.PI/12;feet=new Vector3(center.x+Mathf.Cos(angle)*distance,floor+.03f,center.z+Mathf.Sin(angle)*distance);
                if(!Physics.Raycast(feet+Vector3.up*.5f,Vector3.down,out var ground,.7f,~(1<<2),QueryTriggerInteraction.Ignore)||ground.collider.GetComponentInParent<SortItem>())continue;
                feet.y=ground.point.y+.01f;
                if(Physics.CheckCapsule(feet+Vector3.up*.35f,feet+Vector3.up*1.5f,.25f,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                // A thick sight line (3 cm) so nothing in the pile grazes it.
                var eye=feet+Vector3.up*eyeHeight;
                if(Physics.Raycast(eye,(center-eye).normalized,out var direct,4,~(1<<2),QueryTriggerInteraction.Ignore)&&direct.collider==item.hitbox&&
                    Physics.SphereCast(eye,.03f,center-eye,out var hit,4,~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider==item.hitbox)return true;
            }
            feet=default;return false;
        }
        // From 1.6 m eye height the middle shelf's front beam hides the bottom shelf's back row up close, so stand back:
        // up to 2.2 m, but never past the end of the floor in front of the rack (catwalk racks have about 1.9 m).
        static Vector3 FrontSpot(SortRack rack)
        {
            float y=rack.transform.position.y;var blockers=new Collider[16];
            for(float d=2.2f;d>=1.19f;d-=.1f)
            {
                var spot=rack.transform.TransformPoint(new Vector3(0,0,-d));spot.y=y+.03f;
                // Room for the player's body (loose items do not block the controller) and flat floor at the rack's level.
                int n=Physics.OverlapCapsuleNonAlloc(spot+Vector3.up*.4f,spot+Vector3.up*1.5f,.32f,blockers,~(1<<2),QueryTriggerInteraction.Ignore);
                bool clear=true;for(int i=0;i<n;i++)if(!blockers[i].GetComponentInParent<SortItem>())clear=false;
                if(!clear)continue;
                var hits=Physics.RaycastAll(spot+Vector3.up*.3f,Vector3.down,.6f,~(1<<2),QueryTriggerInteraction.Ignore);
                if(hits.Any(h=>!h.collider.GetComponentInParent<SortItem>()&&h.normal.y>=.95f&&Mathf.Abs(h.point.y-y)<.1f))return spot;
            }
            var fallback=rack.transform.TransformPoint(new Vector3(0,0,-1.2f));fallback.y=y+.03f;return fallback;
        }
        static bool OnShelf(SortRack rack,SortItem item)
        {
            float bottom=item.hitbox.bounds.min.y;
            return rack.shelves.Any(shelf=>Mathf.Abs(bottom-rack.transform.TransformPoint(0,shelf.height,0).y)<.004f);
        }
        static void Capture(Camera camera,string file)
        {
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(image);}
        }
        [Serializable] public class Metric {public bool instanced;public double renderMedianMs,batches;public int submitted,visible;}
        [Serializable] public class Report {public int checks,items,categories,racks,fullRackItems;public string context="Actual designer items and racks; Editor camera-render CPU timing, not Android FPS.";public List<Metric> samples=new List<Metric>();}
        static IEnumerator Measure(Camera camera,ItemBatchRenderer batch,bool enabled,Report report)
        {
            batch.enabled=enabled;if(enabled)batch.Rebuild();for(int i=0;i<15;i++){camera.Render();yield return null;}
            using(var counter=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
            {
                var times=new List<double>();double batches=0;var timer=new System.Diagnostics.Stopwatch();
                for(int i=0;i<40;i++){timer.Restart();camera.Render();timer.Stop();times.Add(timer.Elapsed.TotalMilliseconds);yield return null;batches+=counter.Valid?counter.LastValue:-1;}
                times.Sort();report.samples.Add(new Metric{instanced=enabled,renderMedianMs=times[20],batches=batches/40,submitted=enabled?batch.SubmittedBatches:0,visible=enabled?batch.VisibleInstances:0});
            }
        }
        public static IEnumerator Run()
        {
            checks=0;var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();s.persistence=false;
            s.player.SendMessage("OnApplicationFocus",true);yield return Wait(.15f);
            eyeHeight=s.player.view.transform.position.y-s.player.transform.position.y;
            var initial=s.Capture();var items=s.items;string oldName=s.saveName;s.saveName="designer-tests-"+Guid.NewGuid().ToString("N")+".json";string savePath=s.SavePath;
            var batch=s.GetComponent<ItemBatchRenderer>();var recovery=s.GetComponent<ItemRecovery>();bool recoveryEnabled=recovery.automatic;recovery.automatic=false;
            GameObject cameraObject=null;RenderTexture target=null;var report=new Report{items=items.Length,racks=s.racks.Length};
            try
            {
                var types=items.Select(i=>i.definition).Distinct().OrderBy(d=>d.id,StringComparer.Ordinal).ToList();report.categories=types.Count;
                Check(types.Count==46&&s.racks.Length==46,"46 item types (Batch 01 + Batch 02) and 46 registered racks");
                Check(s.racks.All(r=>r.SizeBased&&r.shelves.Length==3),"Every rack uses size-based points on its three lower shelves");
                Check(UnityEngine.Object.FindObjectsByType<SortRack>(FindObjectsSortMode.None).Length==s.racks.Length,"No unregistered racks in the scene");
                foreach(var type in types)
                {
                    int perRack=s.racks.Min(r=>r.CapacityFor(type));
                    Check(perRack>0&&items.Count(i=>i.definition==type)==perRack,"Exactly one full rack of "+type.displayName+" ("+perRack+")");
                }
                Check(items.Select(i=>i.visual.sharedMaterial).Distinct().Count()<=6,"Shared atlas materials only, no per-item copies");
                Check(items.All(i=>!i.visual.receiveShadows&&i.visual.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off),"All item shadows disabled");
                Check(items.All(i=>i.location==ItemLocation.Loose&&recovery.Contains(i.transform.position)),"Every item starts loose inside the playable area");
                var floor=GameObject.Find("AlignedFloorCollision").GetComponent<BoxCollider>();Check(floor.bounds.size.y<.3f&&floor.bounds.size.z>17,"Designer floor collider is horizontal");
                Check(s.player.transform.position.y>=-.02f&&s.player.transform.position.y<.1f,"Player stands on actual designer floor");
                Capture(s.player.view,"Logs/DesignerGameplay-Spawn.png");

                // Every item type: real targeting, pick, place on its own rack, retrieve and replace.
                for(int i=0;i<types.Count;i++)
                {
                    var type=types[i];var rack=s.racks[i];
                    // Piles hide items from some angles, so try a few visible ones from the pile edges.
                    SortItem item=null;string misses="";int tried=0;
                    foreach(var candidate in items.Where(x=>x.definition==type&&x.location==ItemLocation.Loose))
                    {
                        if(tried>=10)break;if(!ViewPoint(candidate,out var feet))continue;tried++;
                        yield return Aim(s,feet,candidate.hitbox.bounds.center);
                        if(s.TargetItem==candidate){item=candidate;break;}
                        misses+=" ["+candidate.id+" aimed "+(s.TargetItem?s.TargetItem.id:"nothing")+" from "+s.player.transform.position.ToString("F2")+" wanted "+feet.ToString("F2")+"]";
                    }
                    Check(item,"Real collider target: "+type.displayName+misses);
                    Check(s.TryPick(item),"Pick "+type.id);yield return Motion(item);
                    var front=FrontSpot(rack);
                    yield return Aim(s,front,rack.label.transform.position);
                    Check(s.TargetRack==rack,"Rack can be targeted: "+rack.name);
                    Check(s.TryPlace(rack),"Place "+type.id+" on "+rack.name);yield return Motion(item);Physics.SyncTransforms();
                    Check(rack.Type==type&&rack.Count==1&&rack.Capacity==s.racks.Min(r=>r.CapacityFor(type)),"Rack takes the item's type and point count: "+type.displayName);
                    Check(Vector3.Distance(item.transform.position,rack.SlotPose(item.slot).position)<.001f&&OnShelf(rack,item),"Item sits on the shelf at its point: "+type.displayName);
                    int cash=s.Cash;
                    // Like a player, aim at the part that shows: centre first, then the lower and upper front of the item.
                    var box=item.hitbox.bounds;var toFront=-rack.transform.forward*box.extents.z*.8f;
                    foreach(var aim in new[]{box.center,box.center+toFront-Vector3.up*box.extents.y*.6f,box.center+toFront+Vector3.up*box.extents.y*.6f})
                    {s.player.look.SnapToDirection(aim-s.player.view.transform.position);yield return Wait(.15f);if(s.TargetItem==item)break;}
                    Check(s.TargetItem==item,"Placed item remains targetable: "+type.displayName+" (aimed "+(s.TargetItem?s.TargetItem.id:"nothing")+")");
                    Check(s.TryPick(item),"Retrieve placed item: "+type.displayName);yield return Motion(item);
                    Check(rack.Count==0&&rack.Type==null&&rack.Capacity==0,"Empty rack releases its type: "+rack.name);
                    Check(s.TryPlace(rack),"Replace retrieved item: "+type.displayName);yield return Motion(item);Check(s.Cash==cash,"No duplicate placement cash: "+type.displayName);
                }
                Capture(s.player.view,"Logs/DesignerGameplay-Rack.png");
                var other=items.First(x=>x.definition==types[1]&&x.location==ItemLocation.Loose);Check(s.TryPick(other),"Pick a second type");DOTween.CompleteAll();
                Check(!s.TryPlace(s.racks[0])&&s.Message.StartsWith("This rack holds"),"Wrong type is rejected by an assigned rack");
                Check(s.TryPlace(s.racks[1]),"Matching type accepted");DOTween.CompleteAll();

                // Fill the busiest rack completely: every point used, nothing overlapping, rack completion pays once.
                Check(s.Restore(initial),"Reset designer catalog");
                var fullType=types.OrderByDescending(t=>items.Count(x=>x.definition==t)).First();var fullRack=s.racks[0];
                var batchOfType=items.Where(x=>x.definition==fullType).ToList();int completed=0;System.Action<SortRack> onComplete=_=>completed++;s.RackCompleted+=onComplete;
                try
                {
                    int cursor=0;
                    while(cursor<batchOfType.Count)
                    {
                        while(cursor<batchOfType.Count&&s.carried.Count<s.capacity){Check(s.TryPick(batchOfType[cursor++]),"Pick for full rack");DOTween.CompleteAll();}
                        while(s.Active){Check(s.TryPlace(fullRack),"Place into full rack "+fullRack.Count);DOTween.CompleteAll();}
                        yield return null;
                    }
                }
                finally{s.RackCompleted-=onComplete;}
                Physics.SyncTransforms();report.fullRackItems=fullRack.Count;
                Check(fullRack.Count==fullRack.Capacity&&fullRack.Count==batchOfType.Count,"Full rack holds all "+batchOfType.Count+" "+fullType.displayName);
                Check(completed==1&&s.CompletedRackCount>=1,"Rack completion fires once");
                Check(batchOfType.All(x=>OnShelf(fullRack,x)),"Every placed item rests on a shelf");
                var boxes=batchOfType.Select(x=>x.hitbox.bounds).ToList();bool overlap=false;
                for(int a=0;a<boxes.Count&&!overlap;a++)for(int b=a+1;b<boxes.Count;b++){var A=boxes[a];var B=boxes[b];if(Mathf.Min(A.max.x,B.max.x)-Mathf.Max(A.min.x,B.min.x)>.001f&&Mathf.Min(A.max.y,B.max.y)-Mathf.Max(A.min.y,B.min.y)>.001f&&Mathf.Min(A.max.z,B.max.z)-Mathf.Max(A.min.z,B.min.z)>.001f){overlap=true;break;}}
                Check(!overlap,"No two items on the full rack overlap");
                Check(!s.TryPick(items.First(x=>x.definition!=fullType))||!s.TryPlace(fullRack),"A full rack rejects more items");DOTween.CompleteAll();
                yield return Aim(s,FrontSpot(fullRack),fullRack.transform.TransformPoint(new Vector3(0,.9f,0)));
                Capture(s.player.view,"Logs/DesignerGameplay-FullRack.png");
                Check(s.Restore(initial),"Reset designer catalog after full rack");

                // Throw from the cleared spawn area.
                var thrown=items.First(x=>x.location==ItemLocation.Loose);Check(s.TryPick(thrown),"Pick actual model for safe throw");yield return Motion(thrown);
                var spawn=initial.playerPosition;yield return Aim(s,spawn,spawn+new Vector3(0,.4f,2));s.ThrowActive();yield return Motion(thrown);
                Check(thrown.location==ItemLocation.Loose&&s.carried.Count==0,"Safe throw releases one actual model");
                Check(s.Restore(initial),"Reset authored designer catalog");

                // Rendering and abilities at the real item count.
                s.player.SetBlocked(true);
                cameraObject=new GameObject("DesignerStressCamera");var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=13;camera.transform.SetPositionAndRotation(new Vector3(0,6.8f,0),Quaternion.Euler(90,0,0));camera.farClipPlane=20;
                target=new RenderTexture(1280,720,24);camera.targetTexture=target;s.player.view.enabled=false;batch.targetCamera=camera;
                yield return Measure(camera,batch,false,report);yield return Measure(camera,batch,true,report);
                Check(batch.BatchedItems==items.Length,"All "+items.Length+" items use the instanced path");
                Check(batch.SubmittedBatches<=300,"Item submissions stay bounded: "+batch.SubmittedBatches);
                Check(report.samples[1].batches>0&&report.samples[1].batches<report.samples[0].batches,"Batching reduces total batches");
                Capture(camera,"Logs/DesignerPiles.png");
                s.player.view.enabled=true;batch.targetCamera=s.player.view;s.player.SetBlocked(false);
                var first=items.First(x=>x.location==ItemLocation.Loose);Check(s.TryPick(first),items.Length+" items: pick");yield return Motion(first);
                Check(s.TrySearch(),items.Length+" items: search");Check(s.TryMagnet(),items.Length+" items: magnet");yield return Wait(.3f);
                int wallet=s.Cash;Check(!s.TryMagnet()&&s.Cash==wallet,items.Length+" items: full magnet does not charge");
                s.persistence=true;s.Save();Check(s.FlushSaves()&&s.LastSaveError==null,items.Length+" item checkpoint durable");s.persistence=false;
                Check(s.Load()&&s.Cash==wallet&&s.carried.Count==s.capacity&&s.items.Length==items.Length,items.Length+" item inventory/cash reload");
                report.checks=checks;File.WriteAllText("Logs/DesignerGameplayTests.json",JsonUtility.ToJson(report,true));Debug.Log("DESIGNER_GAMEPLAY_OK checks="+checks);
            }
            finally
            {
                DOTween.CompleteAll();s.FlushSaves();s.persistence=false;s.player.SetBlocked(false);s.player.view.enabled=true;batch.targetCamera=s.player.view;
                if(cameraObject){cameraObject.GetComponent<Camera>().targetTexture=null;UnityEngine.Object.Destroy(cameraObject);}if(target){target.Release();UnityEngine.Object.Destroy(target);}
                s.Restore(initial);s.InitializeSaveCache();batch.enabled=true;batch.Rebuild();recovery.automatic=recoveryEnabled;s.saveName=oldName;
                foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(savePath+suffix))File.Delete(savePath+suffix);
            }
        }
    }
}
