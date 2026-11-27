using System;
using UnityEngine;
namespace LabSorting.Sorting
{
    // Created lazily for a dropped item and for any loose item it strikes hard, never for the whole level.
    // Physics uses the item's own BoxCollider (its mesh bounds). Runtime convex MeshColliders were costly to cook,
    // thin hulls sank through the floor when dropped from a height, and turning the box into a trigger left
    // every nearby item unpickable (raycasts ignore triggers).
    public sealed class NaturalItemSettling : MonoBehaviour
    {
        // Below this the item has fallen through the floor: stop simulating so ItemRecovery can bring it back.
        public const float LostBelowY=-.5f;
        SortItem item;Rigidbody body;
        Action arrived;float quiet;bool running;
        public bool Running=>running;
        public Collider[] CollisionShapes=>item&&item.hitbox?new[]{item.hitbox}:Array.Empty<Collider>();
        public Bounds WorldBounds=>item.hitbox.bounds;
        public static int ActiveBodies {get;private set;}
        public static int PeakBodies {get;private set;}
        public static NaturalItemSettling Fit(SortItem item)
        {
            if(!item||item.definition.isRat||!item.hitbox)return null;
            var existing=item.GetComponent<NaturalItemSettling>();if(existing)return existing;
            return item.gameObject.AddComponent<NaturalItemSettling>();
        }
        void Awake()
        {
            item=GetComponent<SortItem>();item.naturalSettling=this;
            body=GetComponent<Rigidbody>();if(!body)body=gameObject.AddComponent<Rigidbody>();
            body.isKinematic=true;body.useGravity=true;body.mass=ThrowFeel.Mass(item.definition);
            body.interpolation=RigidbodyInterpolation.Interpolate;body.maxDepenetrationVelocity=2f;body.linearDamping=.35f;body.angularDamping=2.5f;body.solverIterations=10;body.solverVelocityIterations=3;
        }
        public static void Suspend(SortItem item)
        {
            var natural=item.GetComponent<NaturalItemSettling>();if(!natural)return;
            natural.Stop(false);
        }
        public void Begin(Action done) => Begin(done,Vector3.down*.1f); public void Begin(Action done,Vector3 velocity)
        {
            arrived=done;quiet=0;if(!running){running=true;ActiveBodies++;PeakBodies=Mathf.Max(PeakBodies,ActiveBodies);}
            item.hitbox.enabled=true;item.hitbox.isTrigger=false;
            body.isKinematic=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.constraints=RigidbodyConstraints.None;body.linearVelocity=velocity;body.angularVelocity=Vector3.zero;body.WakeUp();
            // A struck neighbour is drawn by ItemBatchRenderer: hand it back to its own renderer while it moves.
            item.PhysicsWoke();
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
            // A body falling forever would stay "Moving", and ItemRecovery skips moving items.
            if(body.position.y<LostBelowY){Stop(true);return;}
            quiet=body.linearVelocity.sqrMagnitude<.0025f&&body.angularVelocity.sqrMagnitude<.01f?quiet+Time.fixedDeltaTime:0;
            if(body.IsSleeping()||quiet>.35f)Stop(true);
        }
        void OnCollisionEnter(Collision collision)
        {
            // Wake only the product actually struck; never simulate the whole room.
            if(!running||collision.relativeVelocity.sqrMagnitude<.25f)return;
            var other=collision.collider.GetComponentInParent<SortItem>();
            if(!other||other==item||other.location!=ItemLocation.Loose||other.Moving||other.Searched)return;
            var natural=Fit(other);if(natural)natural.Begin(null);
        }
        void OnDisable(){Stop(false);}
    }
}
