using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
using DG.Tweening;
using Unity.Profiling;
namespace LabSorting.Prototype
{
    public static class FullFlightChecks
    {
        static void Check(bool value,string text){if(!value)throw new Exception("FULL FLIGHT: "+text);Debug.Log("FULL FLIGHT PASS: "+text);}
        static IEnumerator Wait(float seconds){float end=Time.time+seconds,limit=Time.realtimeSinceStartup+30;while(Time.time<end){if(Time.realtimeSinceStartup>limit)throw new Exception("Clock stalled");yield return null;}}
        static string Timing(ProfilerRecorder recorder){var samples=recorder.ToArray();return "samples="+samples.Length+" mean_ms="+(samples.Length==0?0:samples.Average(v=>(double)v.Value)/1000000).ToString("F4")+" max_ms="+(samples.Length==0?0:samples.Max(v=>v.Value)/1000000d).ToString("F4");}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var initial=s.Capture();var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1400,-.1f,1400);floor.transform.localScale=new Vector3(20,.2f,20);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(1400,1,1400);wall.transform.localScale=new Vector3(20,2,.12f);
            try
            {
                using var physics=ProfilerRecorder.StartNew(ProfilerCategory.Physics,"Physics.Simulate",512);
                yield return Wait(.5f);string idle=Timing(physics);physics.Reset();physics.Start();
                s.player.SendMessage("OnApplicationFocus",true);yield return Wait(.3f);var chosen=s.items.Take(4).ToArray();int callbacks=0,starts=DropLandingPool.FullFlightStarts,fallbacks=DropLandingPool.HybridFallbacks;
                var targets=new Vector3[4];
                for(int i=0;i<4;i++){var item=chosen[i];var b=s.ReleaseBounds(item);targets[i]=new Vector3(1396+i*2,b.extents.y+.035f-(b.center.y-item.transform.position.y),1399.1f);item.Snap(new Vector3(1396+i*2,1.2f,1397.5f),Quaternion.identity);item.location=ItemLocation.Loose;item.ThrowWithPhysics(targets[i],()=>callbacks++);}
                Check(DropLandingPool.ActiveCount==3&&DropLandingPool.FullFlightStarts==starts+3&&DropLandingPool.HybridFallbacks==fallbacks+1,"Three full flights; fourth uses hybrid without interrupting them");
                var start=chosen[0].transform.position;yield return Wait(.14f);Check(chosen[0].Moving&&chosen[0].transform.position.z>start.z&&chosen[0].transform.position.y>start.y,"Gravity flight visibly moves forward/up before landing");
                var snapshot=s.Capture();Check(snapshot.items.First(e=>e.id==chosen[0].id).position==targets[0],"Mid-flight save retains safe recovery destination");
                yield return Wait(2);var pool=UnityEngine.Object.FindFirstObjectByType<DropLandingPool>();
                Check(DropLandingPool.ActiveCount==0&&callbacks==4&&chosen.All(i=>!i.Moving&&i.hitbox.enabled),"All full/hybrid throws deactivate physics and finish once");
                Check(pool.SettledFlights>0&&pool.ContactCount>0,"Real contact and velocity settling retain actual final pose");
                Check(chosen.All(i=>i.transform.position.z<wall.GetComponent<Collider>().bounds.min.z),"Thrown items remain before the wall");
                snapshot=s.Capture();Check(chosen.All(i=>snapshot.items.First(e=>e.id==i.id).position==i.transform.position),"Settled poses persist exactly");
                string flight=Timing(physics);physics.Stop();
                // Force a real wall impact, rather than merely aiming at unobstructed floor.
                var impact=chosen[0];impact.Snap(new Vector3(1400,1.2f,1399),Quaternion.identity);int contacts=pool.ContactCount;impact.ThrowWithPhysics(new Vector3(1400,.1f,1401),()=>callbacks++);
                yield return Wait(.6f);Check(pool.ContactCount>contacts&&impact.transform.position.z<1400,"Continuous collision blocks a mid-flight wall impact");
                s.Restore(initial);Check(DropLandingPool.ActiveCount==0,"Restore cancels full-flight bodies and reservations");
                impact.Snap(new Vector3(1400,1.2f,1397),Quaternion.identity);impact.ThrowWithPhysics(targets[0],()=>callbacks++);yield return Wait(.1f);pool.SendMessage("OnApplicationPause",true);
                Check(DropLandingPool.ActiveCount==0&&!impact.Moving&&impact.transform.position==targets[0],"Pause safely completes flight and releases its physics body");
                s.Restore(initial);
                var dense=s.items.Where(i=>i.location==ItemLocation.Loose).OrderBy(i=>i.visual.bounds.size.sqrMagnitude).First();
                Check(s.TryPick(dense),"Pick real scene item for dense environment release");DOTween.CompleteAll();yield return Wait(.3f);
                bool found=false;for(int angle=0;angle<360;angle+=30){s.player.look.SetYawPitch(new Vector2(angle,0));yield return null;if(s.TryThrowPosition(dense,out _)){found=true;break;}}
                Check(found,"Dense designer area has a validated front landing");int denseStarts=DropLandingPool.FullFlightStarts;s.ThrowActive();yield return Wait(2.1f);
                Check(DropLandingPool.FullFlightStarts==denseStarts+1&&dense.location==ItemLocation.Loose&&!dense.Moving&&dense.hitbox.enabled&&DropLandingPool.ActiveCount==0,"Actual dense scene drop completes through full-flight physics");
                Physics.SyncTransforms();var near=Physics.OverlapBox(dense.hitbox.bounds.center,dense.hitbox.bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                foreach(var other in near)if(other!=dense.hitbox&&!other.transform.IsChildOf(dense.transform))Check(!Physics.ComputePenetration(dense.hitbox,dense.hitbox.transform.position,dense.hitbox.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)||depth<=.006f,"Dense landing avoids penetration beyond contact tolerance");
                s.Restore(initial);
                AbilityPerfChecks.Measure(s,"Logs/FullFlightAbilities.txt");
                System.IO.File.WriteAllText("Logs/FullFlightPhysics.txt","Scene items="+s.items.Length+"; pooled bodies=3; settled flights="+pool.SettledFlights+"; contacts="+pool.ContactCount+"; physics steps="+pool.PhysicsSteps+". Editor validation, not Android FPS.\nPhysics.Simulate idle: "+idle+"\nPhysics.Simulate flight window: "+flight);
            }
            finally{DOTween.CompleteAll();s.Restore(initial);floor.SetActive(false);wall.SetActive(false);UnityEngine.Object.Destroy(floor);UnityEngine.Object.Destroy(wall);}
        }
    }
}
