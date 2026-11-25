#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class PhysicsDropChecks
    {
        static readonly List<string> report=new List<string>();
        static void Check(bool ok,string message)
        {
            report.Add((ok?"PASS ":"FAIL ")+message);
            if(!ok)throw new Exception("PHYSICS_DROP: "+message);
            Debug.Log("PHYSICS_DROP PASS: "+message);
        }
        public static IEnumerator Run(SortingSession s)
        {
            report.Clear();var saved=s.Capture();bool persistence=s.persistence,motor=s.player.character.Motor.enabled;
            var props=new List<GameObject>();int drops=0;
            Action<string,Vector3,Vector3> cube=(name,pos,scale)=>{var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=pos;go.transform.localScale=scale;props.Add(go);};
            try
            {
                s.persistence=false;s.player.Clear();s.player.SetBlocked(false);
                foreach(var i in s.items)i.CompleteMotion();
                // Empty the inventory through snapshot restoration, preserving the original state for finally.
                var setup=s.Capture();setup.hand.Clear();setup.active=null;
                foreach(var e in setup.items)if(e.location==ItemLocation.Hand){e.location=ItemLocation.Loose;e.position=saved.playerPosition+Vector3.down*20;}
                Check(s.Restore(setup),"Test setup restores successfully");
                var groups=s.items.Where(i=>i.location==ItemLocation.Loose&&!i.definition.isRat&&!i.PickLocked)
                    .GroupBy(i=>i.definition.id).Where(g=>g.Count()>=10).ToArray();
                var preferred=new[]{"first-aid","gas-mask","bandage","medicine-bottle","test-tube","walkie"};
                var types=preferred.Select(p=>groups.FirstOrDefault(g=>g.Key.Contains(p))).Where(g=>g!=null).Distinct().ToList();
                foreach(var g in groups)if(types.Count<6&&!types.Contains(g))types.Add(g);
                Check(types.Count==6,"Six item types available");
                for(int scenario=0;scenario<6;scenario++)
                {
                    string label=new[]{"open floor","wall","raised surface","rapid mixed pile","live clutter","rack front"}[scenario];
                    Vector3 origin=new Vector3(1000+scenario*30,0,1000);
                    if(scenario<4)
                    {
                        cube("Physics test floor",origin+Vector3.down*.1f,new Vector3(20,.2f,20));
                        if(scenario==1)cube("Physics test wall",origin+new Vector3(0,2,1),new Vector3(10,4,.15f));
                        if(scenario==2)cube("Physics test platform",origin+new Vector3(0,.3f,1.8f),new Vector3(3,.6f,2.5f));
                    }
                    else if(scenario==4)origin=saved.playerPosition;
                    else origin=s.racks[0].transform.TransformPoint(new Vector3(0,0,-1.2f));
                    s.player.character.Motor.enabled=true;s.player.character.Motor.SetPosition(origin+Vector3.up*.02f);
                    s.player.look.SetYawPitch(scenario==5?new Vector2(s.racks[0].transform.eulerAngles.y,0):Vector2.zero);
                    yield return new WaitForSecondsRealtime(.3f);s.player.character.Motor.enabled=false;
                    var batch=new List<SortItem>();
                    for(int n=0;n<10;n++)
                    {
                        var group=types[scenario==3?n%types.Count:scenario];
                        var item=group.First(i=>i.location==ItemLocation.Loose&&!i.Moving&&!batch.Contains(i));
                        Check(s.TryPick(item),label+" pick "+item.definition.id);batch.Add(item);
                        yield return new WaitForSecondsRealtime(.08f);
                    }
                    yield return new WaitForSecondsRealtime(.9f);
                    Check(s.carried.Count==10,label+" carries 10 items");
                    while(s.Active)
                    {
                        var item=s.Active;float retry=Time.realtimeSinceStartup+3;
                        do{s.ThrowActive();if(item.location==ItemLocation.Hand)yield return new WaitForSecondsRealtime(.15f);}while(item.location==ItemLocation.Hand&&Time.realtimeSinceStartup<retry);
                        Check(item.location==ItemLocation.Loose,label+" releases "+item.definition.id);
                        var body=item.GetComponent<Rigidbody>();
                        Check(body&&!body.isKinematic&&item.Moving,label+" physics active immediately");
                        Check(Vector3.Distance(item.SavedPosition,item.transform.position)<.001f,label+" saves current flight position");
                        drops++;yield return new WaitForSecondsRealtime(.2f);
                    }
                    float end=Time.realtimeSinceStartup+20;
                    while(batch.Any(i=>i.Moving)&&Time.realtimeSinceStartup<end)yield return null;
                    foreach(var item in batch)
                    {
                        Check(!item.Moving,label+" settles "+item.definition.id);
                        Check(item.hitbox.enabled&&item.GetComponent<Rigidbody>().isKinematic,label+" collider restored and body sleeps");
                        Check(!ItemSupport.Floating(item),label+" supported after landing");
                        Check(item.transform.position.y>origin.y-1,label+" stays above floor");
                        if(scenario==1)Check(item.GetComponent<NaturalItemSettling>().WorldBounds.max.z<origin.z+.95f,label+" stays on near side of wall");
                    }
                    float penetration=0;
                    foreach(var item in batch)foreach(var shape in item.GetComponent<NaturalItemSettling>().CollisionShapes)
                    foreach(var other in Physics.OverlapBox(shape.bounds.center,shape.bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                    {
                        if(other.transform.IsChildOf(item.transform))continue;
                        if(Physics.ComputePenetration(shape,shape.transform.position,shape.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth))penetration=Mathf.Max(penetration,depth);
                    }
                    Check(penetration<.025f,label+" maximum collider penetration="+penetration.ToString("F4"));
                    // Pick settled objects back up and drop once more, exercising suspend/reuse.
                    var again=batch[0];Check(s.TryPick(again),label+" re-pick settled item");yield return new WaitForSecondsRealtime(.4f);
                    s.ThrowActive();Check(again.location==ItemLocation.Loose,label+" re-drop settled item");drops++;
                    end=Time.realtimeSinceStartup+20;while(again.Moving&&Time.realtimeSinceStartup<end)yield return null;
                    Check(!again.Moving,label+" repeated drop settles");
                    Debug.Log("PHYSICS_DROP SCENARIO DONE: "+label);
                }
                report.Add("COMPLETE drops="+drops+" scenarios=6 types=6");Debug.Log("PHYSICS_DROP_OK drops="+drops);
            }
            finally
            {
                foreach(var i in s.items)i.CompleteMotion();
                foreach(var go in props){go.SetActive(false);UnityEngine.Object.Destroy(go);}
                s.player.character.Motor.enabled=motor;s.Restore(saved);s.persistence=persistence;
                System.IO.Directory.CreateDirectory("Logs/PhysicsDropReview");System.IO.File.WriteAllLines("Logs/PhysicsDropReview/results.txt",report);
            }
        }
    }
}
#endif
