using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using LabSorting.Sorting;

namespace LabSorting.UI
{
    [DefaultExecutionOrder(11000)]
    public sealed class FirstLaunchTour : MonoBehaviour
    {
        public SortingSession session;
        public SettingsTutorialPanel settings;
        public CanvasGroup gameplayUI;
        public GameObject overlay;
        public CanvasGroup curtain;
        public CinemachineBrain brain;
        public CinemachineCamera tourCamera;
        public Transform[] shots;
        public bool autoPlay=true;
        public string completionKey="LabSorting.EnvironmentTourDone.v1";
        [Min(1)] public float duration=10;
        [Min(.1f)] public float handoffDuration=1.2f;
        public bool IsRunning {get;private set;}
        public bool Completed=>PlayerPrefs.GetInt(completionKey,0)==1;
        public float Elapsed=>elapsed;
        bool tutorialAfterTour,settingsEnabled,progressEnabled,lookEnabled,handVisible,paused,focused=true,skipping;
        bool oldInteractable,oldRaycasts,oldCursorVisible,cameraRestored;
        float oldAlpha,oldFov,elapsed,skipTime;
        Vector3 localPosition;
        Quaternion localRotation;
        CursorLockMode oldCursor;
        ItemsProgressPanel progress;

        void Awake()
        {
            overlay.SetActive(false);brain.enabled=false;tourCamera.gameObject.SetActive(false);
            tutorialAfterTour=settings.autoTutorial;
            if(autoPlay&&!Completed)settings.autoTutorial=false;
        }
        IEnumerator Start()
        {
            if(!autoPlay||Completed)yield break;
            while(!session.Initialized||session.player.NetworkBlocked)yield return null;
            if(!BeginTour())ResumeTutorial();
        }
        public bool BeginTour()
        {
            if(IsRunning||Completed||!session.Initialized||session.player.blocked||shots==null||shots.Length<2)return false;
            foreach(var shot in shots)if(!shot)return false;
            var camera=session.player.view;
            localPosition=camera.transform.localPosition;localRotation=camera.transform.localRotation;oldFov=camera.fieldOfView;
            oldCursor=Cursor.lockState;oldCursorVisible=Cursor.visible;
            oldAlpha=gameplayUI.alpha;oldInteractable=gameplayUI.interactable;oldRaycasts=gameplayUI.blocksRaycasts;
            settingsEnabled=settings.enabled;progress=GetComponent<ItemsProgressPanel>();progressEnabled=progress&&progress.enabled;
            settings.enabled=false;if(progress)progress.enabled=false;
            lookEnabled=session.player.look.enabled;session.player.look.enabled=false;
            handVisible=session.hand.gameObject.activeSelf;session.hand.gameObject.SetActive(false);
            session.CancelHold();session.player.SetBlocked(true);
            gameplayUI.alpha=0;gameplayUI.interactable=false;gameplayUI.blocksRaycasts=false;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            elapsed=skipTime=0;skipping=false;cameraRestored=false;IsRunning=true;overlay.SetActive(true);overlay.transform.Find("SafeArea").gameObject.SetActive(true);curtain.alpha=1;
            tourCamera.transform.SetPositionAndRotation(shots[0].position,shots[0].rotation);
            tourCamera.Lens.FieldOfView=oldFov;tourCamera.PreviousStateIsValid=false;
            tourCamera.gameObject.SetActive(true);brain.enabled=true;brain.ManualUpdate();
            return true;
        }
        public void Skip(){if(IsRunning&&!skipping&&elapsed<duration){skipping=true;skipTime=0;}}
        void LateUpdate()
        {
            if(!IsRunning||paused||!focused||session.player.NetworkBlocked)return;
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Skip();
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if(skipping)
            {
                skipTime+=dt;Transition(skipTime);
                return;
            }
            elapsed+=dt;
            if(elapsed>=duration){Transition(elapsed-duration);return;}
            curtain.alpha=1-Mathf.Clamp01(elapsed/.5f);
            SamplePose(elapsed,out var position,out var rotation);
            tourCamera.transform.SetPositionAndRotation(position,rotation);brain.ManualUpdate();
        }
        void Transition(float time)
        {
            overlay.transform.Find("SafeArea").gameObject.SetActive(false);
            float half=Mathf.Max(.1f,handoffDuration*.5f);
            if(time<half){curtain.alpha=Mathf.SmoothStep(0,1,time/half);return;}
            if(!cameraRestored)
            {
                curtain.alpha=1;RestoreCamera();cameraRestored=true;
                gameplayUI.alpha=oldAlpha;session.hand.gameObject.SetActive(handVisible);
            }
            curtain.alpha=1-Mathf.SmoothStep(0,1,(time-half)/half);
            if(time>=half*2)Finish(true);
        }
        void RestoreCamera()
        {
            brain.enabled=false;tourCamera.gameObject.SetActive(false);
            var camera=session.player.view;camera.transform.localPosition=localPosition;camera.transform.localRotation=localRotation;camera.fieldOfView=oldFov;
        }
        public void SamplePose(float time,out Vector3 position,out Quaternion rotation)
        {
            if(time>=duration)
            {
                var last=shots[shots.Length-1];position=last.position;rotation=last.rotation;return;
            }
            float progress=Mathf.Clamp01(time/duration)*(shots.Length-1);int index=Mathf.Min((int)progress,shots.Length-2);
            float u=Mathf.SmoothStep(0,1,progress-index);
            position=Vector3.Lerp(shots[index].position,shots[index+1].position,u);
            rotation=Quaternion.Slerp(shots[index].rotation,shots[index+1].rotation,u);
        }
        void Finish(bool remember)
        {
            if(!IsRunning)return;
            IsRunning=false;RestoreCamera();
            session.player.look.enabled=lookEnabled;session.hand.gameObject.SetActive(handVisible);
            gameplayUI.alpha=oldAlpha;gameplayUI.interactable=oldInteractable;gameplayUI.blocksRaycasts=oldRaycasts;
            settings.enabled=settingsEnabled;if(progress)progress.enabled=progressEnabled;
            overlay.SetActive(false);session.player.SetBlocked(false);session.player.Clear();
            Cursor.lockState=oldCursor;Cursor.visible=oldCursorVisible;
            if(remember){PlayerPrefs.SetInt(completionKey,1);PlayerPrefs.Save();ResumeTutorial();}
            else settings.autoTutorial=tutorialAfterTour;
        }
        void ResumeTutorial()
        {
            settings.autoTutorial=tutorialAfterTour;
            var guided=GetComponent<GuidedTutorial>();
            if(tutorialAfterTour&&guided&&!guided.Completed&&!guided.IsRunning)guided.Begin(false);
        }
        void OnApplicationPause(bool value){paused=value;}
        void OnApplicationFocus(bool value){focused=value;}
        void OnDisable(){Finish(false);}
    }
}
