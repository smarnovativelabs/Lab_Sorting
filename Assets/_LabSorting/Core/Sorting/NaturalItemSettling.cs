using System;
using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    // Created lazily for recently dropped items and their immediate supports, never for the whole level.
    public sealed class NaturalItemSettling : MonoBehaviour
    {
        SortItem item;Rigidbody body;readonly List<Collider> shapes=new List<Collider>();
        Action arrived;float quiet;bool running;
        public bool Running=>running;
        public Collider[] CollisionShapes=>shapes.ToArray();
        public Bounds WorldBounds
        {
            get{if(shapes.Count==0)return item.hitbox.bounds;var bounds=shapes[0].bounds;foreach(var shape in shapes)bounds.Encapsulate(shape.bounds);return bounds;}
        }
        public static int ActiveBodies {get;private set;}
        public static int PeakBodies {get;private set;}
        public static NaturalItemSettling Fit(SortItem item)
        {
            if(!item||item.definition.isRat)return null;
            var existing=item.GetComponent<NaturalItemSettling>();if(existing)return existing;
            return item.gameObject.AddComponent<NaturalItemSettling>();
        }
        void Awake()
        {
            item=GetComponent<SortItem>();item.naturalSettling=this;
            foreach(var renderer in item.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh||renderer.name.StartsWith("__QuickOutline"))continue;
                var child=new GameObject("Drop collision shape");child.layer=LayerMask.NameToLayer("SortingItem");child.transform.SetParent(renderer.transform,false);
                var shape=child.AddComponent<MeshCollider>();shape.sharedMesh=filter.sharedMesh;shape.convex=true;shape.contactOffset=.002f;shape.enabled=false;shapes.Add(shape);
            }
            body=GetComponent<Rigidbody>();if(!body)body=gameObject.AddComponent<Rigidbody>();
            body.isKinematic=true;body.useGravity=true;body.mass=ThrowFeel.Mass(item.definition);
            body.linearDamping=.35f;body.angularDamping=2.5f;body.solverIterations=10;body.solverVelocityIterations=3;
            RefreshColliders();
        }
        void RefreshColliders()
        {
            bool world=item.location==ItemLocation.Loose&&(running||!item.Moving)&&!item.Searched;
            foreach(var shape in shapes)if(shape)shape.enabled=world;
            // The broad box remains available to aiming helpers but does not support physics piles.
            if(item.hitbox)item.hitbox.isTrigger=world&&shapes.Count>0;
        }
        public static void Suspend(SortItem item)
        {
            var natural=item.GetComponent<NaturalItemSettling>();if(!natural)return;
            natural.Stop(false);foreach(var shape in natural.shapes)if(shape)shape.enabled=false;
            item.hitbox.isTrigger=false;
        }
        public void Begin(Action done)
        {
            if(shapes.Count==0){item.FinishPhysicsLanding(transform.position,transform.rotation,done);return;}
            arrived=done;quiet=0;if(!running){running=true;ActiveBodies++;PeakBodies=Mathf.Max(PeakBodies,ActiveBodies);}
            item.hitbox.enabled=true;RefreshColliders();
            body.isKinematic=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.constraints=RigidbodyConstraints.None;body.linearVelocity=Vector3.down*.1f;body.angularVelocity=Vector3.zero;body.WakeUp();
        }
        public void FinishNow(){Stop(true);}
        void Stop(bool complete)
        {
            if(!running)return;running=false;ActiveBodies--;
            body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;
            var done=arrived;arrived=null;
            if(complete)item.FinishPhysicsLanding(transform.position,transform.rotation,done);
        }
        void FixedUpdate()
        {
            if(!running)return;
            if(item.location!=ItemLocation.Loose){Stop(false);return;}
            quiet=body.linearVelocity.sqrMagnitude<.0025f&&body.angularVelocity.sqrMagnitude<.01f?quiet+Time.fixedDeltaTime:0;
            if(body.IsSleeping()||quiet>.35f)Stop(true);
        }
        void LateUpdate(){RefreshColliders();}
        void OnDisable(){Stop(false);}
    }
}
