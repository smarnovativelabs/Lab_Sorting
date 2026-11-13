using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class LabSoundscape:MonoBehaviour
    {
        public SortingFeedback feedback;
        public FirstLaunchTour tour;
        public AudioSource ambience,ui;
        public AudioSource music;
        [Range(0,1)] public float musicVolume=.10f;
        public AudioClip click,close;
        [Range(0,1)] public float gameplayVolume=.16f,tourVolume=.23f,uiVolume=.22f;
        bool paused,focused=true;float nextClick;
        public int ClicksPlayed {get;private set;}
        void OnEnable(){ambience.volume=0;var boot=BootMusic.Take();if(boot){if(music)Destroy(music.gameObject);music=boot;music.transform.SetParent(transform,false);}else if(music)music.volume=0;}
        void Update()
        {
            bool audible=feedback.SoundEnabled&&!paused&&focused;
            if(!audible){ambience.volume=0;if(ambience.isPlaying)ambience.Pause();if(music){music.volume=0;music.Pause();}ui.Stop();return;}
            if(music){if(!music.isPlaying){music.UnPause();if(!music.isPlaying)music.Play();}music.volume=Mathf.MoveTowards(music.volume,musicVolume,Time.unscaledDeltaTime*.08f);}
            if(!ambience.isPlaying){ambience.UnPause();if(!ambience.isPlaying)ambience.Play();}
            float target=tour&&tour.IsRunning?tourVolume:gameplayVolume;
            ambience.volume=Mathf.MoveTowards(ambience.volume,target,Time.unscaledDeltaTime*.15f);
        }
        public void Click(bool closing=false)
        {
            if(!isActiveAndEnabled||!feedback.SoundEnabled||paused||!focused||Time.unscaledTime<nextClick)return;
            nextClick=Time.unscaledTime+.075f;ui.Stop();ui.clip=closing?close:click;ui.volume=uiVolume;ui.Play();ClicksPlayed++;
        }
        void Silence(){ambience.Pause();ambience.volume=0;ui.Stop();if(music){music.Pause();music.volume=0;}}
        void OnApplicationPause(bool value){paused=value;if(value)Silence();}
        void OnApplicationFocus(bool value){focused=value;if(!value)Silence();}
        void OnDisable(){if(ambience)ambience.Stop();if(ui)ui.Stop();if(music)music.Stop();}
    }
}
