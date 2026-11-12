using UnityEngine;
namespace LabSorting.Sorting
{
    // Three reusable physics proxies; the 4032 scene items remain static colliders.
    public sealed class DropLandingPool : MonoBehaviour
    {
        sealed class Slot {public Rigidbody body;public BoxCollider box;public SortItem item;public Vector3 target,offset;public Quaternion rotation;public float until,quiet;public bool flight;public System.Action done;}
        readonly Collider[] overlaps=new Collider[32];
        public static int FullFlightStarts {get;private set;}
        public static int HybridFallbacks {get;private set;}
        static DropLandingPool instance;
        static readonly System.Collections.Generic.Dictionary<SortItem,Bounds> reservations=new System.Collections.Generic.Dictionary<SortItem,Bounds>();
        public static bool Reserved(Bounds bounds,SortItem except){foreach(var pair in reservations)if(pair.Key!=except&&pair.Value.Intersects(bounds))return true;return false;}
        public static void Reserve(SortItem item,Vector3 target){var b=item.visual.bounds;b.center+=target-item.transform.position;b.Expand(.03f);reservations[item]=b;}
        public static void Unreserve(SortItem item){reservations.Remove(item);}
        readonly Slot[] slots=new Slot[3];
        PhysicsMaterial material;
        public int ContactCount {get;private set;}
        public int SettledFlights {get;private set;}
        public int RecoveredFlights {get;private set;}
        public int PhysicsSteps {get;private set;}
        public string LastFinishReason {get;private set;}
        public Vector3 LastFinishPosition {get;private set;}
        public Vector3 LastTarget {get;private set;}
        public static event System.Action<SortItem,float> Impact;
        public void RecordContact(GameObject body,float speed)
        {
            ContactCount++;
            if(speed<.15f)return;
            foreach(var slot in slots)if(slot.item&&slot.body.gameObject==body){if(!slot.item.ImpactFeedbackPlayed)Impact?.Invoke(slot.item,speed);break;}
        }
        public static int ActiveCount {get{int count=0;if(instance)foreach(var slot in instance.slots)if(slot!=null&&slot.item)count++;return count;}}
        void Awake()
        {
            material=new PhysicsMaterial("Short drop landing"){bounciness=.06f,dynamicFriction=.6f,staticFriction=.6f,bounceCombine=PhysicsMaterialCombine.Minimum};
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Landing body "+i);go.transform.SetParent(transform);go.layer=LayerMask.NameToLayer("SortingItem");
                var box=go.AddComponent<BoxCollider>();box.sharedMaterial=material;box.enabled=false;
                var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;body.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ;body.solverIterations=4;body.solverVelocityIterations=1;body.maxDepenetrationVelocity=1;
                go.AddComponent<DropLandingContact>().pool=this;slots[i]=new Slot{body=body,box=box};
            }
        }
        public static void Begin(SortItem item,Vector3 target,Quaternion rotation,System.Action done)
        { Start(item,target,rotation,done,false); }
        public static bool TryFlight(SortItem item,Vector3 target,System.Action done)
        {return Start(item,target,Quaternion.identity,done,true);}
        static bool Start(SortItem item,Vector3 target,Quaternion rotation,System.Action done,bool flight)
        {
            if(!instance)instance=new GameObject("Temporary drop physics (3 bodies)").AddComponent<DropLandingPool>();
            Slot slot=null;foreach(var candidate in instance.slots)if(!candidate.item){slot=candidate;break;}
            if(slot==null){if(flight){HybridFallbacks++;return false;}item.FinishPhysicsLanding(target,rotation,done);return true;}
            var box=item.hitbox as BoxCollider;if(!box){if(flight)return false;item.FinishPhysicsLanding(target,rotation,done);return true;}
            var size=Vector3.Scale(box.size,item.originalScale);size=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
            var root=flight?item.transform.position:target;
            var bounds=new Bounds(root+Vector3.Scale(box.center,item.originalScale),size);if(item.visual)bounds.Encapsulate(item.visual.bounds);
            slot.box.size=bounds.size;slot.offset=bounds.center-root;slot.target=target;slot.rotation=rotation;slot.item=item;slot.done=done;slot.flight=flight;slot.quiet=0;slot.until=Time.time+(flight?1.8f:.22f);
            // Changing body mode invalidates interpolation history. Teleport both representations
            // after activating, so a newly pooled body cannot resume at its old/origin pose.
            slot.body.interpolation=RigidbodyInterpolation.None;slot.body.isKinematic=false;
            var position=root+slot.offset+(flight?Vector3.zero:Vector3.up*.015f);slot.body.transform.SetPositionAndRotation(position,Quaternion.identity);slot.body.position=position;slot.body.rotation=Quaternion.identity;
            slot.body.constraints=flight?(RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ):(RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ);
            float weight=ThrowFeel.Weight(item.definition);
            slot.body.mass=ThrowFeel.Mass(item.definition);
            slot.body.linearDamping=flight?Mathf.Lerp(.22f,.1f,weight):0;slot.body.angularDamping=Mathf.Lerp(3.5f,5.5f,weight);
            slot.box.enabled=true;slot.body.useGravity=true;slot.body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;slot.body.linearVelocity=Vector3.down*.15f;slot.body.angularVelocity=Vector3.zero;slot.body.WakeUp();
            if(flight)
            {
                float gravity=Mathf.Max(.1f,-Physics.gravity.y),rise=Mathf.Max(root.y,target.y)+Mathf.Lerp(.30f,.22f,weight)-root.y;
                float up=Mathf.Sqrt(2*gravity*rise),time=up/gravity+Mathf.Sqrt(2*(root.y+rise-target.y)/gravity);
                var velocity=(target-root)/Mathf.Max(.25f,time);velocity.y=up;slot.body.linearVelocity=velocity;slot.body.angularVelocity=new Vector3(0,Mathf.Lerp(.65f,.2f,weight),0);FullFlightStarts++;
            }
            return true;
        }
        public static void Stop(SortItem item,bool finish)
        {
            if(!finish)Unreserve(item);
            if(!instance)return;foreach(var slot in instance.slots)if(slot.item==item){if(finish)instance.Finish(slot);else instance.Release(slot);return;}
        }
        void Release(Slot slot){slot.box.enabled=false;slot.body.linearVelocity=Vector3.zero;slot.body.angularVelocity=Vector3.zero;slot.body.collisionDetectionMode=CollisionDetectionMode.Discrete;slot.body.isKinematic=true;slot.item=null;slot.done=null;}
        bool ValidRest(Slot slot)
        {
            var center=slot.body.position;var extents=slot.box.size*.5f;
            if(Reserved(slot.box.bounds,slot.item))return false;
            int count=Physics.OverlapBoxNonAlloc(center,extents,overlaps,slot.body.rotation,~0,QueryTriggerInteraction.Ignore);if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++){var other=overlaps[i];if(other==slot.box||other.transform.IsChildOf(slot.item.transform))continue;float tolerance=other.GetComponentInParent<SortItem>()?0:.006f;if(Physics.ComputePenetration(slot.box,center,slot.body.rotation,other,other.transform.position,other.transform.rotation,out _,out var depth)&&depth>tolerance)return false;}
            return Physics.Raycast(center,Vector3.down,out var hit,extents.y+.06f,~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider!=slot.box&&hit.normal.y>.7f;
        }
        void Finish(Slot slot,bool actual=false){LastFinishPosition=slot.body.position-slot.body.rotation*slot.offset;LastTarget=slot.target;var item=slot.item;var done=slot.done;var target=actual?LastFinishPosition:slot.target;var rotation=actual?slot.body.rotation:slot.rotation;Release(slot);if(item)item.FinishPhysicsLanding(target,rotation,done);}
        void FixedUpdate()
        {
            foreach(var slot in slots)if(slot.item)
            {
                PhysicsSteps++;
                if(slot.item.location!=ItemLocation.Loose){Release(slot);continue;}
                slot.item.transform.SetPositionAndRotation(slot.body.position-slot.body.rotation*slot.offset,slot.body.rotation);
                if(slot.flight)
                {
                    slot.quiet=slot.body.linearVelocity.sqrMagnitude<.0064f&&slot.body.angularVelocity.sqrMagnitude<.04f?slot.quiet+Time.fixedDeltaTime:0;
                    if(slot.quiet>=.12f&&ValidRest(slot)){SettledFlights++;LastFinishReason="settled";Finish(slot,true);}
                    else if(Time.time>=slot.until||slot.body.position.y<slot.target.y-2){RecoveredFlights++;LastFinishReason="flight recovery";Finish(slot);}
                    continue;
                }
                if(Time.time>=slot.until||slot.body.IsSleeping()||Vector3.Distance(slot.body.position-slot.offset,slot.target)>.12f){LastFinishReason=Time.time>=slot.until?"timeout":slot.body.IsSleeping()?"sleep":"deviation";Finish(slot);}
            }
        }
        void LateUpdate(){foreach(var slot in slots)if(slot.item)slot.item.transform.SetPositionAndRotation(slot.body.transform.position-slot.body.transform.rotation*slot.offset,slot.body.transform.rotation);}
        void OnApplicationPause(bool paused){if(paused)foreach(var slot in slots)if(slot.item)Finish(slot);}
        void OnDestroy(){foreach(var slot in slots)if(slot!=null&&slot.item)Finish(slot);reservations.Clear();if(material)Destroy(material);}
    }
}
