#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class CrowdedDropChecks
    {
        static readonly List<string> report=new List<string>();
        static readonly List<string> failures=new List<string>();
        static void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);Debug.Log("CROWDED DROP: "+report.Last());if(!ok)failures.Add(text);}
        static IEnumerator Capture(string name){ShotGate.Capture("Logs/CrowdedDrop/"+name+".png");yield return new WaitForSecondsRealtime(.35f);}
        static string SupportEvidence(SortItem item)
        {
            var b=item.hitbox.bounds;var evidence=new System.Text.StringBuilder();
            evidence.Append(" bounds="+b+" position="+item.transform.position.ToString("F6")+" rotation="+item.transform.rotation.ToString("F6"));
            foreach(var other in Physics.OverlapBox(b.center,b.extents+Vector3.one*ItemSupport.Gap,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
            {
                if(other.transform.IsChildOf(item.transform))continue;
                foreach(float down in new[]{.01f,ItemSupport.Gap})
                    if(Physics.ComputePenetration(item.hitbox,item.hitbox.transform.position-Vector3.up*down,item.hitbox.transform.rotation,other,other.transform.position,other.transform.rotation,out var direction,out float depth))
                        evidence.Append(" contact="+other.name+" down="+down+" normal="+direction+" depth="+depth);
            }
            return evidence.ToString();
        }
        static bool Intersects(SortItem item,out string obstacle)
        {
            Physics.SyncTransforms();var b=item.hitbox.bounds;
            foreach(var c in Physics.OverlapBox(b.center,b.extents,Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore))
            {
                if(c.transform.IsChildOf(item.transform))continue;
                var natural=item.GetComponent<NaturalItemSettling>();var shapes=natural?natural.CollisionShapes:new[]{item.hitbox};
                foreach(var shape in shapes)if(Physics.ComputePenetration(shape,shape.transform.position,shape.transform.rotation,c,c.transform.position,c.transform.rotation,out _,out float depth)&&depth>.012f)
                {obstacle=c.name+" penetration="+depth;return true;}
            }
            obstacle="none";return false;
        }
        public static IEnumerator Run()
        {
            report.Clear();failures.Clear();Directory.CreateDirectory("Logs/CrowdedDrop");
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            s.player.SendMessage("OnApplicationFocus",true);s.player.Clear();s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);
            var saved=s.Capture();bool motor=s.player.character.Motor.enabled;
            try
            {
                Cursor.lockState=CursorLockMode.None;
                var spawn=s.player.transform.position;
                var eye=s.player.view.transform.position;
                var rack=s.racks.Where(r=>!r.ratsOnly).OrderBy(r=>(r.transform.position-spawn).sqrMagnitude).First();
                var rackBounds=new Bounds(rack.transform.position,Vector3.zero);
                foreach(var c in rack.GetComponentsInChildren<Collider>())if(!c.GetComponentInParent<SortItem>())rackBounds.Encapsulate(c.bounds);
                RaycastHit wall=default;Vector3 wallDirection=Vector3.zero;float nearest=100;
                for(int a=0;a<72;a++)
                {
                    var d=Quaternion.Euler(0,a*5,0)*Vector3.forward;
                    foreach(var hit in Physics.RaycastAll(eye,d,30,~(1<<2),QueryTriggerInteraction.Ignore))
                    {
                        if(hit.collider.GetComponentInParent<SortItem>()||hit.collider.GetComponentInParent<SortRack>()||hit.collider.transform.IsChildOf(s.player.transform)||Mathf.Abs(hit.normal.y)>.15f)continue;
                        if(hit.collider.bounds.size.y<2||hit.distance>=nearest)continue;
                        wall=hit;wallDirection=d;nearest=hit.distance;
                    }
                }
                Check(wall.collider,"Found actual Lab wall");
                for(int scenario=0;scenario<3;scenario++)
                {
                    s.Restore(saved);s.player.Clear();s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);
                    string label=scenario==0?"crowded-floor":scenario==1?"rack-side":"wall-side";
                    Vector3 position=spawn,direction;
                    if(scenario==0)
                    {
                        var pile=s.items.Where(i=>i.location==ItemLocation.Loose&&!i.definition.isRat&&(i.transform.position-spawn).sqrMagnitude<16).OrderBy(i=>(i.transform.position-spawn).sqrMagnitude).Take(30).ToArray();
                        var center=pile.Aggregate(Vector3.zero,(p,i)=>p+i.transform.position)/pile.Length;direction=center-spawn;
                    }
                    else if(scenario==1)direction=rackBounds.center-spawn;
                    else
                    {
                        if(!wall.collider)continue;
                        position=wall.point-wallDirection*.85f;position.y=spawn.y;direction=wallDirection;
                    }
                    direction.y=0;direction.Normalize();
                    s.player.character.Motor.enabled=true;s.player.character.Motor.SetPosition(position);
                    s.player.look.SnapToDirection(direction+Vector3.down*.65f);yield return new WaitForSecondsRealtime(.4f);s.player.character.Motor.enabled=false;
                    Physics.SyncTransforms();
                    report.Add(label+" player="+s.player.transform.position+" wall="+(wall.collider?wall.collider.name:"none")+" rack="+rack.name);
                    var pool=s.items.Where(i=>i.location==ItemLocation.Loose&&!i.definition.isRat).OrderBy(i=>(i.transform.position-spawn).sqrMagnitude).ToArray();
                    var chosen=pool.Where(i=>i.definition.displayName.Contains("Yellow")).Take(3).Concat(pool.GroupBy(i=>i.definition).Select(g=>g.First()).Where(i=>!i.definition.displayName.Contains("Yellow")).Take(3)).ToArray();
                    var dropped=new List<SortItem>();
                    foreach(var item in chosen)
                    {
                        Check(s.TryPick(item),label+" pickup "+item.definition.displayName);item.CompleteMotion();
                        var planned=s.PlanSmoothDrop(item,out var expected);
                        var footprint=s.ReleaseBounds(item);var offset=footprint.center-item.transform.position;
                        int crossed=0;string crossedName="";
                        foreach(var p in planned)
                        {
                            foreach(var c in Physics.OverlapBox(p+offset,Vector3.Max(Vector3.one*.001f,footprint.extents-Vector3.one*.015f),Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore))
                            {
                                if(c.transform.IsChildOf(item.transform)||c.transform.IsChildOf(s.player.transform))continue;
                                crossed++;crossedName=c.name;break;
                            }
                        }
                        Check(crossed==0,label+" arc stays clear "+item.name+" crossedSamples="+crossed+" obstacle="+crossedName);
                        s.ThrowActive();dropped.Add(item);
                        var previous=item.transform.position;float lastTime=Time.time;float until=Time.realtimeSinceStartup+35;float largestJump=0;
                        while(item.Moving&&Time.realtimeSinceStartup<until)
                        {
                            yield return null;float dt=Time.time-lastTime;
                            if(dt>0&&dt<.08f)largestJump=Mathf.Max(largestJump,Vector3.Distance(previous,item.transform.position));
                            previous=item.transform.position;lastTime=Time.time;
                        }
                        Check(!item.Moving&&item.hitbox.enabled,label+" drop completes "+item.definition.displayName);
                        Check(!Intersects(item,out var obstacle),label+" no intersection "+item.name+" with "+obstacle);
                        bool supported=!ItemSupport.Floating(item);
                        if(!supported){report.Add(SupportEvidence(item));File.WriteAllText("Logs/CrowdedDrop/unsupported-"+label+"-"+item.id+".json",JsonUtility.ToJson(s.Capture()));}
                        Check(supported,label+" supported landing "+item.name);
                        Check(largestJump<.65f,label+" frame continuity "+item.name+" maxStep="+largestJump.ToString("F3"));
                        report.Add(label+" landed "+item.name+" at="+item.transform.position+" planned="+expected);
                    }
                    // Recheck earlier drops after later items have landed.
                    foreach(var item in dropped)Check(!Intersects(item,out var other),label+" final pile clear "+item.name+" obstacle="+other);
                    var target=dropped.Aggregate(Vector3.zero,(p,i)=>p+i.hitbox.bounds.center)/dropped.Count;
                    for(int i=0;i<3;i++){s.player.look.SnapToDirection(target-s.player.view.transform.position);yield return new WaitForSecondsRealtime(.2f);}
                    yield return Capture(label);
                    int stacks=0;
                    foreach(var item in dropped)
                    {
                        var b=item.hitbox.bounds;
                        if(Physics.Raycast(b.center,Vector3.down,out var below,b.extents.y+.1f,~(1<<2),QueryTriggerInteraction.Ignore)&&below.collider.GetComponentInParent<SortItem>() is SortItem support&&support!=item)stacks++;
                    }
                    report.Add(label+" product-supported landings="+stacks);
                    Check(NaturalItemSettling.ActiveBodies==0,label+" temporary bodies sleep after settling");
                    if(scenario==2)
                    {
                        var pose=dropped[0].transform.position;var rotation=dropped[0].transform.rotation;var snapshot=s.Capture();
                        Check(s.Restore(snapshot),"Save restores naturally tilted dropped items");yield return null;
                        Check(Vector3.Distance(pose,dropped[0].transform.position)<.01f&&Quaternion.Angle(rotation,dropped[0].transform.rotation)<.1f,"Settled drop pose survives save/restore");
                        SortItem supporting=null;
                        foreach(var item in dropped)
                        {
                            var bounds=item.naturalSettling.WorldBounds;
                            var hit=Physics.RaycastAll(bounds.center,Vector3.down,bounds.extents.y+.12f,~(1<<2),QueryTriggerInteraction.Ignore)
                                .FirstOrDefault(h=>h.collider.GetComponentInParent<SortItem>() is SortItem below&&below!=item);
                            if(hit.collider){supporting=hit.collider.GetComponentInParent<SortItem>();break;}
                        }
                        if(supporting)Check(s.TryPick(supporting),"Remove a real pile support");else report.Add("No stacked support in this random landing; deterministic real-product support check follows.");
                        if(supporting)
                        {
                            supporting.CompleteMotion();yield return new WaitForSecondsRealtime(8);
                            foreach(var item in dropped.Where(i=>i!=supporting))Check(!item.Moving&&!ItemSupport.Floating(item),"Support removal leaves stable item "+item.name);
                            yield return Capture("support-removed");
                        }
                    }
                }
            }
            finally
            {
                DG.Tweening.DOTween.CompleteAll();s.player.character.Motor.enabled=motor;s.Restore(saved);
                File.WriteAllLines("Logs/CrowdedDrop/results.txt",report);
            }
            yield return CurrentGameplayChecks.SupportRemoval();if(failures.Count>0)throw new Exception("CROWDED DROP: "+failures.Count+" checks failed: "+string.Join("; ",failures));
            Debug.Log("CROWDED_DROP_OK");
        }
    }
}
#endif
