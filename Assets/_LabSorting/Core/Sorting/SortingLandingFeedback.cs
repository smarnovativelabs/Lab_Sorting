using UnityEngine;
using Lofelt.NiceVibrations;
namespace LabSorting.Sorting
{
    public sealed partial class SortingFeedback
    {
        readonly AudioSource[] landingSources=new AudioSource[3];
        int landingVoice;
        public int ImpactCues {get;private set;}
        public AudioClip LastLandingClip {get;private set;}
        public float LastLandingPitch {get;private set;}
        public AudioClip LandingClip(ItemDefinition definition)
        {
            var clip=definition?definition.placementMaterial switch{
                PlacementMaterial.Glass=>glassPlace,PlacementMaterial.Plastic=>plasticPlace,
                PlacementMaterial.Metal=>metalPlace,_=>drop}:drop;
            return clip?clip:drop;
        }
        void InitializeLandingAudio()
        {
            for(int i=0;i<landingSources.Length;i++){
                var voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=0;
                voice.outputAudioMixerGroup=source.outputAudioMixerGroup;landingSources[i]=voice;
            }
        }
        void StopLandingAudio(){foreach(var voice in landingSources)if(voice)voice.Stop();}
        void ImpactSound(SortItem item,float speed)
        {
            if(!isActiveAndEnabled||!item||item.ImpactFeedbackPlayed)return;
            item.ImpactFeedbackPlayed=true;ImpactCues++;
            LastLandingClip=LandingClip(item.definition);LastLandingPitch=ThrowFeel.Pitch(item.definition);
            if(SoundEnabled&&LastLandingClip){var voice=landingSources[landingVoice++%landingSources.Length];voice.Stop();voice.pitch=LastLandingPitch;voice.clip=LastLandingClip;voice.volume=volume*ThrowFeel.Gain(item.definition,speed);voice.Play();PlayedSounds++;}
            #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if(HapticsEnabled&&Time.unscaledTime>=nextLanding){nextLanding=Time.unscaledTime+.08f;HapticPatterns.PlayPreset(ThrowFeel.Weight(item.definition)>.55f?HapticPatterns.PresetType.LightImpact:HapticPatterns.PresetType.Selection);}
            #endif
        }
    }
}
