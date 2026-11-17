using UnityEngine;

namespace LabSorting.Sorting
{
    // Ten agents share two ambient voices. No per-rat AudioSource or allocating physics queries.
    public sealed class RatColony : MonoBehaviour
    {
        public SortingSession session;
        public SortingFeedback feedback;
        public LabRat[] rats;
        public AudioClip squeak,scurry,release,landing;
        [Tooltip("Multiplier on every rat sound")][Range(0,1)] public float volume=.65f;
        [Tooltip("Seconds between idle squeaks of one rat")] public Vector2 squeakInterval=new Vector2(25,40);
        [Tooltip("Minimum seconds between any two idle squeaks across the colony")] public float squeakGap=15;
        [Tooltip("Seconds between scurry patters while a nearby rat runs")] public Vector2 scurryInterval=new Vector2(.8f,1.3f);
        public float SqueakDelay=>Random.Range(squeakInterval.x,squeakInterval.y);
        public Vector2 roomX=new Vector2(-11.5f,11.5f),roomZ=new Vector2(-8.5f,8.5f);
        readonly RaycastHit[] hits=new RaycastHit[32];
        readonly Collider[] movementOverlaps=new Collider[32];
        readonly AudioSource[] voices=new AudioSource[2];
        AudioSource interaction;
        LabRat first,second;
        float scanAt,stepAt,interactionAt,squeakAt;
        bool paused,focused=true;
        public int AmbientVoiceCount=>voices.Length;
        public bool Audible=>feedback&&feedback.SoundEnabled&&!paused&&focused&&Time.timeScale>0&&!session.player.blocked;
        int WorldMask=>~((1<<2)|(1<<LayerMask.NameToLayer("SortingItem")));
        void Awake(){for(int i=0;i<2;i++)voices[i]=Voice("Rat ambient "+i);interaction=Voice("Rat interaction");}
        AudioSource Voice(string title)
        {
            var go=new GameObject(title);go.transform.SetParent(transform,false);var s=go.AddComponent<AudioSource>();
            s.playOnAwake=false;s.spatialBlend=1;s.rolloffMode=AudioRolloffMode.Linear;s.minDistance=3;s.maxDistance=8;s.dopplerLevel=0;s.priority=180;return s;
        }
        void OnEnable(){session.ItemPlaced+=Placed;session.ItemLanded+=Landed;}
        void OnDisable(){session.ItemPlaced-=Placed;session.ItemLanded-=Landed;Silence();}
        public void PlayInteraction(SortItem item,bool thrown=false)
        {
            // A rat in the container is silent.
            if(!item||!item.definition.isRat||item.location==ItemLocation.Rack||!Audible||Time.time<interactionAt)return;
            interactionAt=Time.time+.09f;interaction.transform.position=item.transform.position;interaction.Stop();interaction.pitch=Random.Range(.94f,1.08f);
            interaction.PlayOneShot(thrown?release:squeak,.15f*volume);
            var rat=item.GetComponent<LabRat>();if(rat)rat.NextSqueak=Time.time+SqueakDelay;
        }
        // Placing (or tossing) a rat into the container makes no rat sound, and cuts any still playing.
        void Placed(SortItem item){if(item&&item.definition.isRat&&interaction)interaction.Stop();}
        void Landed(SortItem item,bool tossed)
        {
            if(!item||!item.definition.isRat)return;
            if(tossed||item.location==ItemLocation.Rack){if(interaction)interaction.Stop();return;}
            if(Audible){interaction.transform.position=item.transform.position;interaction.PlayOneShot(landing,.08f*volume);}
        }
        void Silence(){foreach(var s in voices)if(s)s.Stop();if(interaction)interaction.Stop();}
        void OnApplicationPause(bool value){paused=value;if(value)Silence();}
        void OnApplicationFocus(bool value){focused=value;if(!value)Silence();}
        void Update()
        {
            if(!Audible){Silence();return;}
            var listener=session.player.view.transform.position;
            if(Time.time>=scanAt)
            {
                scanAt=Time.time+.25f;first=second=null;float a=64,b=64;
                foreach(var rat in rats)
                {
                    if(!rat||!rat.isActiveAndEnabled||rat.Item.location!=ItemLocation.Loose)continue;
                    float d=(rat.transform.position-listener).sqrMagnitude;
                    if(d<a){b=a;second=first;a=d;first=rat;}else if(d<b){b=d;second=rat;}
                }
            }
            Ambient(first,voices[0],listener);Ambient(second,voices[1],listener);
            if((interaction.transform.position-listener).sqrMagnitude>=64)interaction.Stop();
        }
        void Ambient(LabRat rat,AudioSource voice,Vector3 listener)
        {
            // Only loose rats on the floor make ambient sound; held or boxed rats stay quiet.
            if(!rat||!rat.isActiveAndEnabled||rat.Item.location!=ItemLocation.Loose||(rat.transform.position-listener).sqrMagnitude>=64){voice.Stop();return;}
            voice.transform.position=rat.transform.position;
            if(voice.isPlaying)return;
            if(Time.time>=rat.NextSqueak)
            {
                rat.NextSqueak=Time.time+SqueakDelay;
                // The colony-wide gap keeps the two nearest rats from squeaking back to back.
                if(Time.time>=squeakAt){squeakAt=Time.time+squeakGap;voice.pitch=Random.Range(.9f,1.12f);voice.PlayOneShot(squeak,.07f*volume);}
            }
            else if(rat.Running&&Time.time>=stepAt){stepAt=Time.time+Random.Range(scurryInterval.x,scurryInterval.y);voice.pitch=Random.Range(.9f,1.1f);voice.PlayOneShot(scurry,.03f*volume);}
        }
        public bool TryStep(LabRat rat,Vector3 next,out Vector3 grounded)
        {
            grounded=next;
            if(next.x<roomX.x||next.x>roomX.y||next.z<roomZ.x||next.z>roomZ.y)return false;
            var item=rat.GetComponent<SortItem>();var delta=next-rat.transform.position;
            if(delta.sqrMagnitude<.0000001f)return false;
            var half=item.definition.size*.5f;half.y=Mathf.Max(.005f,half.y-.002f);
            var from=rat.transform.position+rat.transform.rotation*item.definition.center;
            int count=Physics.BoxCastNonAlloc(from,half,delta.normalized,hits,rat.transform.rotation,delta.magnitude+.025f,WorldMask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(hits[i].normal.y<.65f&&!hits[i].collider.transform.IsChildOf(session.player.transform))return false;
            if(!Physics.Raycast(next+Vector3.up*.15f,Vector3.down,out var floor,3f,WorldMask,QueryTriggerInteraction.Ignore)||floor.normal.y<.8f)return false;
            // Loose items do not support/trap a rat after a crowded drop. Ease down to
            // the real floor; never step up a wall or walk across an unsupported edge.
            float floorY=floor.point.y+.004f;
            if(floorY-rat.transform.position.y>=.08f)return false;
            grounded.y=Mathf.MoveTowards(rat.transform.position.y,floorY,2f*Time.deltaTime);
            // Include the upcoming turn, so the longer full-size body cannot rotate through a wall.
            var facing=LabRat.Facing(delta.normalized);
            count=Physics.OverlapBoxNonAlloc(grounded+facing*item.definition.center,half,movementOverlaps,facing,WorldMask,QueryTriggerInteraction.Ignore);
            if(count==movementOverlaps.Length)return false;
            for(int i=0;i<count;i++)if(!movementOverlaps[i].transform.IsChildOf(session.player.transform))return false;
            return true;
        }
    }
}
