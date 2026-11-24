using UnityEngine;

namespace LabSorting.Sorting
{
    [RequireComponent(typeof(SortItem))]
    public sealed class LabRat : MonoBehaviour
    {
        public RatColony colony;
        public Animator animator;
        public float speed=.65f;
        public SortItem Item {get;private set;}
        public bool Running {get;private set;}
        public float NextSqueak {get;set;}
        Vector3 direction;
        float decisionAt,dirtyAt;
        static readonly int Run=Animator.StringToHash("Running");
        public void StopRunning()
        {
            Running=false;direction=Vector3.zero;decisionAt=0;
            FreezePose();
        }
        void FreezePose()
        {
            if(!animator)return;
            animator.SetBool(Run,false);
            // The imported "Idle" take also contains leg motion. Selecting that state
            // alone does not stop the visible running cycle: sample and hold its pose.
            animator.Play("Idle",0,0);animator.Update(0);animator.speed=0;
        }
        void OnEnable(){if(Item&&Item.location!=ItemLocation.Loose)StopRunning();}
        // Rat.fbx is authored nose toward local -Z (the tail runs along +Z), so the body
        // must face the opposite of LookRotation(heading) or the rat runs tail-first.
        public static Quaternion Facing(Vector3 heading)=>Quaternion.LookRotation(-heading);
        void Awake(){Item=GetComponent<SortItem>();NextSqueak=Time.time+(colony?colony.SqueakDelay:30f);}
        void Update()
        {
            Running=false;
            if(!colony||!colony.session.Initialized){if(animator&&animator.speed!=0)FreezePose();return;}
            if(Item.location==ItemLocation.Loose&&!Item.Moving&&!Item.Searched&&!colony.session.player.blocked&&Time.deltaTime>0)
            {
                if(Time.time>=decisionAt)
                {
                    decisionAt=Time.time+Random.Range(1f,3f);
                    direction=Random.value<.25f?Vector3.zero:Quaternion.Euler(0,Random.Range(0,360),0)*Vector3.forward;
                    var away=transform.position-colony.session.player.transform.position;away.y=0;
                    if(away.sqrMagnitude<1.8f*1.8f)direction=away.normalized;
                }
                if(direction.sqrMagnitude>.1f)
                {
                    // Turn toward the chosen direction and always advance along the body's own
                    // nose, so a sharp turn becomes a short arc instead of a backwards slide.
                    var turned=Quaternion.RotateTowards(transform.rotation,Facing(direction),540*Time.deltaTime);
                    var heading=turned*Vector3.back;heading.y=0;heading.Normalize();
                    var next=transform.position+heading*speed*Time.deltaTime;
                    if(colony.TryStep(this,next,out var grounded))
                    {
                        transform.SetPositionAndRotation(grounded,turned);Running=true;
                        if(Time.time>=dirtyAt){Item.MarkPoseDirty();dirtyAt=Time.time+.5f;}
                    }
                    else
                    {
                        // Try the target direction (the mid-turn heading may be the blocked one),
                        // then tangents/reverse, instead of repeatedly choosing a direction into
                        // the same wall (especially while fleeing the player).
                        bool escaped=false;
                        for(int turn=0;turn<=7;turn++)
                        {
                            var candidate=Quaternion.Euler(0,(turn%2==0?-1:1)*Mathf.Ceil(turn/2f)*45,0)*direction;
                            if(!colony.TryStep(this,transform.position+candidate*speed*Time.deltaTime,out grounded))continue;
                            direction=candidate;transform.SetPositionAndRotation(grounded,Facing(direction));
                            Running=escaped=true;decisionAt=Time.time+1;Item.MarkPoseDirty();break;
                        }
                        if(!escaped){direction=Vector3.zero;decisionAt=Time.time+.15f;}
                    }
                }
            }
            if(animator)
            {
                if(Running){animator.speed=1;animator.SetBool(Run,true);}
                else if(animator.speed!=0)FreezePose();
            }
        }
    }
}
