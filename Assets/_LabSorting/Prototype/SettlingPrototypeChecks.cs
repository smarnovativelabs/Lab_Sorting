#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    // Experimental only. Independent physics world and visual copies; production items/drop code are untouched.
    public static class SettlingPrototypeChecks
    {
        static readonly List<string> report=new List<string>();
        static readonly Vector3 origin=new Vector3(1000,0,1000);
        static GameObject Solid(Scene scene,string name,Vector3 position,Vector3 size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
            SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=origin+position;go.transform.localScale=size;
            return go;
        }
        static Rigidbody Copy(Scene scene,SortItem source,Vector3 position)
        {
            var go=new GameObject("Prototype "+source.definition.displayName);SceneManager.MoveGameObjectToScene(go,scene);
            go.transform.position=origin+position;go.transform.localScale=source.originalScale;
            foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh||renderer.name.StartsWith("__QuickOutline"))continue;
                var child=new GameObject(renderer.name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));child.transform.SetParent(go.transform,false);
                child.transform.localPosition=source.transform.InverseTransformPoint(renderer.transform.position);
                child.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*renderer.transform.rotation;
                var m=source.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;child.transform.localScale=m.lossyScale;
                child.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var shown=child.GetComponent<MeshRenderer>();shown.sharedMaterials=renderer.sharedMaterials;shown.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
                var collider=child.GetComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;collider.convex=true;
            }
            var body=go.AddComponent<Rigidbody>();body.mass=.3f;body.linearDamping=.3f;body.angularDamping=2;
            body.solverIterations=12;body.solverVelocityIterations=4;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            return body;
        }
        static Bounds Bounds(Rigidbody body)
        {
            var colliders=body.GetComponentsInChildren<Collider>();var b=colliders[0].bounds;
            foreach(var c in colliders)b.Encapsulate(c.bounds);return b;
        }
        static IEnumerator Simulate(PhysicsScene physics,int steps)
        {
            for(int i=0;i<steps;i++){physics.Simulate(1f/60f);if(i%12==0)yield return null;}
        }
        public static IEnumerator Run()
        {
            report.Clear();Directory.CreateDirectory("Logs/SettlingPrototype");
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var saved=s.Capture();bool motor=s.player.character.Motor.enabled;
            var stage=SceneManager.CreateScene("Isolated settling experiment",new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=stage.GetPhysicsScene();
            var bodies=new List<Rigidbody>();int failures=0;
            try
            {
                s.player.Clear();s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);
                Solid(stage,"Test floor",new Vector3(0,-.1f,0),new Vector3(12,.2f,12));
                Solid(stage,"Test wall",new Vector3(0,1.5f,2.5f),new Vector3(10,3,.2f));
                Solid(stage,"Shelf",new Vector3(2,.8f,1.4f),new Vector3(1.5f,.08f,1.5f));
                Solid(stage,"Rack post",new Vector3(1.3f,.9f,.7f),new Vector3(.08f,1.8f,.08f));
                var types=s.items.Where(i=>i.location==ItemLocation.Loose&&!i.definition.isRat).GroupBy(i=>i.definition).Select(g=>g.First()).ToArray();
                var bottle=types.First(i=>i.definition.displayName.Contains("Yellow"));
                var pack=types.First(i=>i.definition.displayName.Contains("Antibiotic"));
                for(int i=0;i<12;i++)
                {
                    // Three release lanes spread crowded drops instead of constructing a tall fixed tower.
                    float lane=(i%3-1)*.38f;
                    var body=Copy(stage,i%3==0?pack:bottle,new Vector3(lane,1.2f,-1.8f));bodies.Add(body);
                    body.linearVelocity=new Vector3(0,2.2f,4.2f);body.angularVelocity=new Vector3(.3f,.7f,.2f);
                    yield return Simulate(physics,90);
                }
                yield return Simulate(physics,600);
                foreach(var body in bodies)
                {
                    bool still=body.linearVelocity.sqrMagnitude<.01f&&body.angularVelocity.sqrMagnitude<.04f;
                    var b=Bounds(body);bool onStage=b.min.y>-.025f&&b.center.z<origin.z+2.5f;
                    bool ok=still&&onStage;if(!ok)failures++;
                    report.Add((ok?"PASS ":"FAIL ")+body.name+" settled="+still+" bounds="+b+" velocity="+body.linearVelocity);
                    if(still){body.Sleep();body.isKinematic=true;}
                }
                float height=bodies.Max(b=>Bounds(b).max.y)-origin.y;report.Add("Pile maximum height="+height.ToString("F3")+"m; bodies="+bodies.Count);
                s.player.character.Motor.SetPosition(origin+new Vector3(0,.01f,-3));s.player.character.Motor.enabled=false;
                for(int i=0;i<3;i++){s.player.look.SnapToDirection(origin+new Vector3(0,.3f,1)-s.player.view.transform.position);yield return new WaitForSecondsRealtime(.2f);}
                ShotGate.Capture("Logs/SettlingPrototype/01-settled-pile.png");yield return new WaitForSecondsRealtime(.4f);
                // Wake nearby settled bodies when a lower support is removed.
                var lower=bodies.OrderBy(b=>Bounds(b).center.y).First();var removed=Bounds(lower);UnityEngine.Object.Destroy(lower.gameObject);bodies.Remove(lower);yield return null;
                foreach(var body in bodies){body.isKinematic=false;body.WakeUp();}
                yield return Simulate(physics,360);
                int moving=bodies.Count(b=>b.linearVelocity.sqrMagnitude>.01f||b.angularVelocity.sqrMagnitude>.04f);
                report.Add((moving==0?"PASS ":"FAIL ")+"Support removal recovery; moving bodies="+moving);if(moving>0)failures++;
                ShotGate.Capture("Logs/SettlingPrototype/02-support-removed.png");yield return new WaitForSecondsRealtime(.4f);
            }
            finally
            {
                File.WriteAllLines("Logs/SettlingPrototype/results.txt",report);
                SceneManager.UnloadSceneAsync(stage);s.player.character.Motor.enabled=motor;s.Restore(saved);
            }
            if(failures>0)throw new Exception("SETTLING PROTOTYPE: "+failures+" failed checks; see results.txt");
            Debug.Log("SETTLING_PROTOTYPE_OK");
        }
    }
}
#endif
