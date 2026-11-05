using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using LabSorting.Sorting;
using LabSorting.Player;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class SettingsChecks
    {
        static IEnumerator Wait(float seconds){float end=Time.unscaledTime+seconds,deadline=Time.realtimeSinceStartup+120;while(Time.unscaledTime<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Settings test frame clock stopped");yield return null;}}
        public static IEnumerator Run(SortingSession s,Action<bool,string> check,Action<SortingSession,string> capture)
        {
            var p=UnityEngine.Object.FindFirstObjectByType<SettingsTutorialPanel>();var progress=UnityEngine.Object.FindFirstObjectByType<ItemsProgressPanel>();var initial=s.Capture();var look=s.player.look;float oldSensitivity=look.SensitivityMultiplier;bool oldSound=p.feedback.SoundEnabled,oldHaptics=p.feedback.HapticsEnabled;
            var originalInput=InputSystem.settings;InputSettings testInput=null;Keyboard keyboard=null;
            try
            {
                check(p&&p.root&&p.sensitivitySlider&&p.nextButton,"Tutorial and settings controls wired in scene");check(p.preferencesPrefix=="LabSorting.AutomatedTutorial.","Tests isolate settings from player preferences");
                p.autoTutorial=true;p.SendMessage("Start");check(p.IsOpen&&p.ShowingTutorial&&p.TutorialPage==0&&s.player.blocked,"First launch opens tutorial and blocks gameplay");
                check(!s.TryPick(s.items[0])&&!s.TryMagnet(),"Tutorial blocks sorting and paid tools");yield return Wait(.1f);capture(s,"Logs/TutorialHUD.png");
                p.nextButton.onClick.Invoke();check(p.TutorialPage==1&&PlayerPrefs.GetInt(p.preferencesPrefix+"TutorialPage")==1,"Next saves tutorial position");p.Close();check(!p.TutorialCompleted,"Interrupted tutorial remains incomplete");p.SendMessage("Start");check(p.TutorialPage==1&&p.IsOpen,"Incomplete tutorial resumes saved page");p.backButton.onClick.Invoke();check(p.TutorialPage==0&&!p.backButton.interactable,"Back clamps at first tutorial page");
                for(int i=0;i<4;i++){yield return Wait(.05f);check(p.tutorialText.preferredHeight<=p.tutorialText.rectTransform.rect.height,"Tutorial card "+(i+1)+" fits without clipped text");capture(s,"Logs/TutorialPage"+(i+1)+".png");p.nextButton.onClick.Invoke();}check(!p.IsOpen&&p.TutorialCompleted&&!s.player.blocked,"Finish records completion and resumes gameplay");p.SendMessage("Start");check(!p.IsOpen,"Completed tutorial does not repeat on launch");
                p.openButton.onClick.Invoke();check(p.sensitivitySlider.handleRect.rect.height<=44,"Slider handle fits its touch track without overlapping labels");p.replayButton.onClick.Invoke();check(p.ShowingTutorial&&p.TutorialPage==0,"Settings replay starts tutorial at first page");p.skipButton.onClick.Invoke();check(!p.IsOpen&&p.TutorialCompleted&&!s.player.blocked,"Skip dismisses tutorial and preserves completion");
                s.player.SetMove(Vector2.one);p.openButton.onClick.Invoke();check(p.IsOpen&&!p.ShowingTutorial&&s.player.blocked&&s.player.Move==Vector2.zero,"Settings clears movement and blocks gameplay");
                int cash=s.Cash,count=s.carried.Count;check(!s.TrySearch()&&!s.TryUpgradeCapacity()&&s.Cash==cash&&s.carried.Count==count,"Settings prevents paid or inventory actions");
                p.sensitivitySlider.value=2;check(Mathf.Approximately(look.SensitivityMultiplier,2)&&Mathf.Approximately(PlayerPrefs.GetFloat(p.preferencesPrefix+"LookSensitivity"),2),"Slider applies and persists sensitivity");
                look.SetYawPitch(Vector2.zero);look.ApplyLookInput(new Vector2(10,0),LabLookInputMode.Delta,.01f);float high=look.Yaw;p.ChangeSensitivity(.5f);look.SetYawPitch(Vector2.zero);look.ApplyLookInput(new Vector2(10,0),LabLookInputMode.Delta,.01f);check(Mathf.Abs(high-look.Yaw*4)<.001f,"Sensitivity scales delta look response fourfold across range");
                look.SetYawPitch(Vector2.zero);look.ApplyLookInput(new Vector2(1,0),LabLookInputMode.Rate,.01f);float lowRate=look.Yaw;p.ChangeSensitivity(2);look.SetYawPitch(Vector2.zero);look.ApplyLookInput(new Vector2(1,0),LabLookInputMode.Rate,.01f);check(Mathf.Abs(look.Yaw-lowRate*4)<.001f,"Sensitivity scales gamepad rate look consistently");
                p.ChangeSensitivity(99);check(look.SensitivityMultiplier==2,"Sensitivity upper bound clamps");p.ChangeSensitivity(-1);check(look.SensitivityMultiplier==.5f,"Sensitivity lower bound clamps");p.ChangeSensitivity(float.NaN);check(look.SensitivityMultiplier==1,"Invalid sensitivity safely falls back to default");p.ChangeSensitivity(1.5f);
                p.soundButton.onClick.Invoke();p.hapticsButton.onClick.Invoke();check(!p.feedback.SoundEnabled&&!p.feedback.HapticsEnabled,"Settings switches control shared feedback");p.LoadPreferences();check(look.SensitivityMultiplier==1.5f&&!p.feedback.SoundEnabled&&!p.feedback.HapticsEnabled,"Reload restores look and feedback preferences");yield return Wait(.1f);capture(s,"Logs/SettingsHUD.png");
                p.resetButton.onClick.Invoke();check(look.SensitivityMultiplier==1&&p.feedback.SoundEnabled&&p.feedback.HapticsEnabled&&s.Cash==cash&&s.carried.Count==count&&p.TutorialCompleted,"Reset settings preserves sorting progress and tutorial completion");
                p.closeButton.onClick.Invoke();check(!p.IsOpen&&!s.player.blocked,"Settings close restores gameplay input");s.player.SetBlocked(true);p.OpenSettings();p.Close();check(s.player.blocked,"Closing settings preserves a pre-existing input block");s.player.SetBlocked(false);
                progress.Open();p.OpenSettings();check(!progress.IsOpen&&p.IsOpen&&s.player.blocked,"Settings transfers modal ownership from progress panel");progress.Open();check(!progress.IsOpen,"Progress panel cannot nest inside settings");p.Close();check(!s.player.blocked,"Modal transfer closes without stuck input block");
                p.OpenSettings();p.enabled=false;check(!p.IsOpen&&!s.player.blocked,"Disabling settings releases its input block");p.enabled=true;
                testInput=UnityEngine.Object.Instantiate(originalInput);InputSystem.settings=testInput;testInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                testInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F1));yield return Wait(.1f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(.1f);check(p.IsOpen&&!p.ShowingTutorial,"F1 opens settings via keyboard input");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return Wait(.1f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Wait(.1f);check(!p.IsOpen&&!s.player.blocked,"Escape closes settings without stuck controls");
            }
            finally
            {
                if(keyboard!=null)InputSystem.RemoveDevice(keyboard);InputSystem.settings=originalInput;if(testInput)UnityEngine.Object.Destroy(testInput);p.Close();p.autoTutorial=false;s.player.SetBlocked(false);look.SetSensitivityMultiplier(oldSensitivity);p.feedback.SetSound(oldSound,false);p.feedback.SetHaptics(oldHaptics,false);s.Restore(initial);
                foreach(var key in new[]{"LookSensitivity","Sound","Haptics","TutorialDone","TutorialPage"})PlayerPrefs.DeleteKey(p.preferencesPrefix+key);PlayerPrefs.Save();
            }
        }
    }
}


