using System.Collections;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class WelcomeBackSummary:MonoBehaviour
    {
        public SortingSession session;
        public ServicePresentation presentation;
        public GuidedTutorial tutorial;
        public FirstLaunchTour tour;
        static bool evaluatedThisLaunch;
        bool pending,paused,focused=true;
        float readyTime;
        public bool Shown {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLaunch(){evaluatedThisLaunch=false;}
        IEnumerator Start(){while(!session.Initialized)yield return null;QueueSavedProgress();}
        public void QueueSavedProgress()
        {
            if(evaluatedThisLaunch)return;evaluatedThisLaunch=true;
            int sorted=session.SortedCount;
            pending=session.LoadedAtStartup&&sorted>0&&sorted<session.items.Length;
        }
        void Update()
        {
            if(!pending)return;
            if(paused||!focused||session.player.blocked||(tutorial&&tutorial.IsRunning)||(tour&&tour.IsRunning)||presentation.ToastBusy){readyTime=0;return;}
            readyTime+=Time.unscaledDeltaTime;if(readyTime<.8f)return;
            pending=false;Shown=true;int sorted=session.SortedCount;
            presentation.ShowToast("WELCOME BACK!\n"+sorted+" sorted  |  "+(session.items.Length-sorted)+" remaining");
        }
        void OnApplicationPause(bool value){paused=value;readyTime=0;}
        void OnApplicationFocus(bool value){focused=value;readyTime=0;}
    }
}
