using UnityEngine;
using Lofelt.NiceVibrations;
namespace LabSorting.Sorting
{
    public enum SortingCue { Pick, Place, Throw, Reject, RackComplete, Toss }
    [RequireComponent(typeof(AudioSource))]
    public sealed partial class SortingFeedback : MonoBehaviour
    {
        public SortingSession session;
        public AudioClip pick,place,drop,reject;
        [Range(0,1)] public float volume=.45f;
        [Tooltip("Optional dust burst played where thrown or tossed items land.")] public ParticleSystem dust;
        public bool SoundEnabled {get;private set;}=true;
        public bool HapticsEnabled {get;private set;}=true;
        public int PlayedSounds {get;private set;}
        public int AcceptedCues {get;private set;}
        public int Landings {get;private set;}
        AudioSource source;
        float nextFeedback,nextLanding;
        public const string SoundKey="LabSorting.Sound",HapticsKey="LabSorting.Haptics";
        void Awake()
        {
            source=GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.loop=false;
            InitializeLandingAudio();
            SoundEnabled=PlayerPrefs.GetInt(SoundKey,1)!=0;HapticsEnabled=PlayerPrefs.GetInt(HapticsKey,1)!=0;
        }
        void OnEnable() {DropLandingPool.Impact+=ImpactSound;if(session){session.Backgrounded+=BackgroundFeedback;session.Feedback+=Play;session.ItemLanded+=Landed;session.ItemPlaced+=Placed;session.RackCompleted+=Celebrate;session.VisualsReset+=StopCelebration;}}
        void OnDisable() {DropLandingPool.Impact-=ImpactSound;if(session){session.Backgrounded-=BackgroundFeedback;session.Feedback-=Play;session.ItemLanded-=Landed;session.ItemPlaced-=Placed;session.RackCompleted-=Celebrate;session.VisualsReset-=StopCelebration;}if(source) source.Stop();StopCelebration();StopLandingAudio();}
        public void SetSound(bool value,bool persist=true) {SoundEnabled=value;if(!value){if(source)source.Stop();if(chimeSource)chimeSource.Stop();StopLandingAudio();}if(persist){PlayerPrefs.SetInt(SoundKey,value?1:0);PlayerPrefs.Save();}}
        public void SetHaptics(bool value,bool persist=true) {HapticsEnabled=value;if(persist){PlayerPrefs.SetInt(HapticsKey,value?1:0);PlayerPrefs.Save();}}
        public void ToggleSound() {SetSound(!SoundEnabled);}
        void BackgroundFeedback(){StopCelebration();StopLandingAudio();if(source)source.Stop();}
        public void ToggleHaptics() {SetHaptics(!HapticsEnabled);}
        public void Play(SortingCue cue)
        {
            // A magnet batch or repeated invalid tap produces one soft response, not a burst.
            if(!isActiveAndEnabled||Time.unscaledTime<nextFeedback) return;
            nextFeedback=Time.unscaledTime+(cue==SortingCue.Reject ? .18f : .075f);AcceptedCues++;
            // A dropped item makes its sound when it lands (see Landed), not when it leaves the hand.
            var clip=cue==SortingCue.Pick?pick:cue==SortingCue.Place?PlacementClip(lastPlaced):(cue==SortingCue.RackComplete||cue==SortingCue.Toss)?place:cue==SortingCue.Reject?reject:null;
            // Each toss in a streak sounds a little higher.
            float pitch=cue==SortingCue.RackComplete?1.2f:cue==SortingCue.Toss?1.05f+.07f*Mathf.Min(session?session.TossStreak-1:0,6):1;
            if(SoundEnabled&&clip&&source) {source.pitch=pitch;source.PlayOneShot(clip,volume);PlayedSounds++;}
            #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if(HapticsEnabled) HapticPatterns.PlayPreset((cue==SortingCue.Place||cue==SortingCue.RackComplete||cue==SortingCue.Toss)?HapticPatterns.PresetType.LightImpact:cue==SortingCue.Reject?HapticPatterns.PresetType.Warning:HapticPatterns.PresetType.Selection);
            #endif
        }
        // Touchdown: a puff of dust sized to the item, and a thud that is deeper for bigger items.
        void Landed(SortItem item,bool tossed)
        {
            if(!isActiveAndEnabled||!item)return;Landings++;
            if(dust&&!tossed)
            {
                var bounds=item.hitbox.bounds;dust.transform.position=new Vector3(bounds.center.x,bounds.min.y+.01f,bounds.center.z);
                var emit=new ParticleSystem.EmitParams{applyShapeToPosition=true,startSize=.025f,startLifetime=.22f,startColor=new Color(.8f,.8f,.78f,.22f),velocity=Vector3.up*.06f};dust.Emit(emit,2);
            }
            if(!tossed){ImpactSound(item,1.5f);return;}
            if(Time.unscaledTime<nextLanding)return;nextLanding=Time.unscaledTime+.05f;
            if(SoundEnabled&&place&&source){source.pitch=1.1f;source.PlayOneShot(place,volume*.45f);PlayedSounds++;}
            #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if(HapticsEnabled&&!tossed) HapticPatterns.PlayPreset(HapticPatterns.PresetType.Selection);
            #endif
        }
    }
}
