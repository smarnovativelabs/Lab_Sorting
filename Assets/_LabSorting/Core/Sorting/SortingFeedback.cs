using UnityEngine;
using Lofelt.NiceVibrations;
namespace LabSorting.Sorting
{
    public enum SortingCue { Pick, Place, Throw, Reject }
    [RequireComponent(typeof(AudioSource))]
    public sealed class SortingFeedback : MonoBehaviour
    {
        public SortingSession session;
        public AudioClip pick,place,drop,reject;
        [Range(0,1)] public float volume=.45f;
        public bool SoundEnabled {get;private set;}=true;
        public bool HapticsEnabled {get;private set;}=true;
        public int PlayedSounds {get;private set;}
        public int AcceptedCues {get;private set;}
        AudioSource source;
        float nextFeedback;
        public const string SoundKey="LabSorting.Sound",HapticsKey="LabSorting.Haptics";
        void Awake()
        {
            source=GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.loop=false;
            SoundEnabled=PlayerPrefs.GetInt(SoundKey,1)!=0;HapticsEnabled=PlayerPrefs.GetInt(HapticsKey,1)!=0;
        }
        void OnEnable() {if(session) session.Feedback+=Play;}
        void OnDisable() {if(session) session.Feedback-=Play;if(source) source.Stop();}
        public void SetSound(bool value,bool persist=true) {SoundEnabled=value;if(!value&&source) source.Stop();if(persist){PlayerPrefs.SetInt(SoundKey,value?1:0);PlayerPrefs.Save();}}
        public void SetHaptics(bool value,bool persist=true) {HapticsEnabled=value;if(persist){PlayerPrefs.SetInt(HapticsKey,value?1:0);PlayerPrefs.Save();}}
        public void ToggleSound() {SetSound(!SoundEnabled);}
        public void ToggleHaptics() {SetHaptics(!HapticsEnabled);}
        public void Play(SortingCue cue)
        {
            // A magnet batch or repeated invalid tap produces one soft response, not a burst.
            if(!isActiveAndEnabled||Time.unscaledTime<nextFeedback) return;
            nextFeedback=Time.unscaledTime+(cue==SortingCue.Reject ? .18f : .075f);AcceptedCues++;
            var clip=cue==SortingCue.Pick?pick:cue==SortingCue.Place?place:cue==SortingCue.Throw?drop:reject;
            if(SoundEnabled&&clip&&source) {source.pitch=1;source.PlayOneShot(clip,volume);PlayedSounds++;}
            #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if(HapticsEnabled) HapticPatterns.PlayPreset(cue==SortingCue.Place?HapticPatterns.PresetType.LightImpact:cue==SortingCue.Reject?HapticPatterns.PresetType.Warning:HapticPatterns.PresetType.Selection);
            #endif
        }
    }
}
