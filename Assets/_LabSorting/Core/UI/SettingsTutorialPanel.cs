using UnityEngine;
using UnityEngine.InputSystem;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class SettingsTutorialPanel : MonoBehaviour
    {
        public SortingSession session;
        public SortingFeedback feedback;
        public GameObject root,settingsContent,tutorialContent;
        public UnityEngine.UI.Text title,sensitivityLabel,soundLabel,hapticsLabel,tutorialText,pageLabel,nextLabel;
        public UnityEngine.UI.Slider sensitivitySlider;
        public UnityEngine.UI.Slider moveSpeedSlider;
        public UnityEngine.UI.Text moveSpeedLabel;
        public ControlSizes controlSizes;
        public UnityEngine.UI.Slider joystickSizeSlider,actionSizeSlider;
        public UnityEngine.UI.Text joystickSizeLabel,actionSizeLabel;
        public float MoveSpeedMultiplier {get;private set;}=1;
        public UnityEngine.UI.Button openButton,closeButton,nextButton,backButton,skipButton,replayButton,resetButton,soundButton,hapticsButton;
        public string preferencesPrefix="LabSorting.";
        public bool autoTutorial=true;
        public bool IsOpen {get;private set;}
        public bool ShowingTutorial {get;private set;}
        public int TutorialPage {get;private set;}
        public bool TutorialCompleted=>PlayerPrefs.GetInt(preferencesPrefix+"TutorialDone",0)==1;
        float saveAt=-1;
        bool previousBlocked,previousVisible;
        CursorLockMode previousCursor;
        static readonly string[] cards={
            "MOVE & LOOK\n\nUse the left joystick to move. Drag the right side to look around.\n\nOn desktop: WASD to move, drag to look. Adjust look sensitivity in Settings.",
            "PICK & DROP\n\nAim at an outlined item. Tap PICK for one item, or hold to collect more of that type.\n\nHands start with 10 slots. DROP drops one selected item per tap.",
            "SORT YOUR RACKS\n\nTap an inventory category to select it. Aim at a green rack and tap or hold PLACE.\n\nThe first item assigns the whole rack. Pick all its items back up to reset it.",
            "TOOLS & REWARDS\n\nSEARCH and MAGNET use your selected category anywhere in the lab. Full hands never charge for magnet.\n\nSmall milestone rewards help you progress. Get tools with cash, optional reward videos or the store. ITEM PROGRESS shows what remains."
        };
        // Scenes with a GuidedTutorial use it on first launch; the cards stay available as "How to play".
        GuidedTutorial Guided=>GetComponent<GuidedTutorial>();
        void Start()
        {
            root.SetActive(false);LoadPreferences();if(!autoTutorial)return;
            var guided=Guided;
            if(guided){if(!guided.Completed)guided.Begin(false);}
            else if(!TutorialCompleted)OpenTutorial(false);
        }
        void Update()
        {
            if(saveAt>=0&&Time.unscaledTime>=saveAt)FlushSettings();
            var key=Keyboard.current;if(key!=null){if(key.f1Key.wasPressedThisFrame){if(IsOpen)CloseRequested();else OpenSettings();}else if(IsOpen&&key.escapeKey.wasPressedThisFrame)CloseRequested();}
            if(IsOpen&&!ShowingTutorial)RefreshSettings();
        }
        public void LoadPreferences()
        {
            float sensitivity=PlayerPrefs.GetFloat(preferencesPrefix+"LookSensitivity",1);if(float.IsNaN(sensitivity)||float.IsInfinity(sensitivity))sensitivity=1;
            session.player.look.SetSensitivityMultiplier(sensitivity);
            float speed=PlayerPrefs.GetFloat(preferencesPrefix+"MoveSpeed",1);ApplyMoveSpeed(speed);
            if(controlSizes)controlSizes.Apply(PlayerPrefs.GetFloat(preferencesPrefix+"JoystickSize",1),PlayerPrefs.GetFloat(preferencesPrefix+"ActionSize",1));
            feedback.SetSound(PlayerPrefs.GetInt(preferencesPrefix+"Sound",1)!=0,false);feedback.SetHaptics(PlayerPrefs.GetInt(preferencesPrefix+"Haptics",1)!=0,false);
            sensitivitySlider.SetValueWithoutNotify(session.player.look.SensitivityMultiplier);RefreshSettings();
        }
        void Show()
        {
            if(IsOpen||session.player.TutorialBlocked)return;
            var progress=GetComponent<ItemsProgressPanel>();if(progress&&progress.IsOpen)progress.Close();
            previousBlocked=session.player.blocked;previousCursor=Cursor.lockState;previousVisible=Cursor.visible;
            session.CancelHold();session.player.SetBlocked(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            IsOpen=true;root.SetActive(true);root.transform.SetAsLastSibling();
        }
        public void OpenSettings(){if(session.player.TutorialBlocked)return;var completion=GetComponent<LabCompletionPanel>();if(completion&&completion.IsOpen)return;Show();ShowingTutorial=false;title.text="SETTINGS";settingsContent.SetActive(true);tutorialContent.SetActive(false);RefreshSettings();var commerce=UnityEngine.Object.FindFirstObjectByType<LabSorting.Services.SupplyCommerce>();if(commerce)commerce.TryInterstitialAtBreak();}
        public void ReplayTutorial(){var guided=Guided;if(guided){Close();guided.Begin(true);}else OpenTutorial(true);}
        public void OpenHowToPlay(){OpenTutorial(true);}
        public void OpenTutorial(bool restart)
        {
            var completion=GetComponent<LabCompletionPanel>();if(completion&&completion.IsOpen)return;
            Show();ShowingTutorial=true;settingsContent.SetActive(false);tutorialContent.SetActive(true);title.text="WELCOME TO LAB SORTING";
            TutorialPage=restart?0:Mathf.Clamp(PlayerPrefs.GetInt(preferencesPrefix+"TutorialPage",0),0,cards.Length-1);RefreshTutorial();
        }
        void RefreshTutorial(){tutorialText.text=cards[TutorialPage];pageLabel.text=(TutorialPage+1)+" / "+cards.Length;backButton.interactable=TutorialPage>0;nextLabel.text=TutorialPage==cards.Length-1?"LET'S SORT":"NEXT";}
        void SavePage(){PlayerPrefs.SetInt(preferencesPrefix+"TutorialPage",TutorialPage);PlayerPrefs.Save();RefreshTutorial();}
        public void Next(){if(!IsOpen||!ShowingTutorial)return;if(TutorialPage==cards.Length-1){SkipTutorial();return;}TutorialPage++;SavePage();}
        public void Back(){if(!IsOpen||!ShowingTutorial)return;TutorialPage=Mathf.Max(0,TutorialPage-1);SavePage();}
        public void SkipTutorial(){PlayerPrefs.SetInt(preferencesPrefix+"TutorialDone",1);PlayerPrefs.Save();Close();}
        public void CloseRequested(){if(ShowingTutorial&&IsOpen)SkipTutorial();else Close();}
        public void Close()
        {
            FlushSettings();if(!IsOpen)return;IsOpen=false;ShowingTutorial=false;root.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=previousCursor;Cursor.visible=previousVisible;
        }
        public void ChangeSensitivity(float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))value=1;
            value=Mathf.Round(Mathf.Clamp(value,.5f,2f)*20)/20;session.player.look.SetSensitivityMultiplier(value);sensitivitySlider.SetValueWithoutNotify(value);
            PlayerPrefs.SetFloat(preferencesPrefix+"LookSensitivity",value);saveAt=Time.unscaledTime+.3f;RefreshSettings();
        }
        void ApplyMoveSpeed(float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))value=1;
            MoveSpeedMultiplier=Mathf.Round(Mathf.Clamp(value,.75f,1.2f)*20)/20;
            session.player.character.MaxStableMoveSpeed=3.2f*MoveSpeedMultiplier;
            if(moveSpeedSlider)moveSpeedSlider.SetValueWithoutNotify(MoveSpeedMultiplier);
        }
        public void ChangeMoveSpeed(float value){ApplyMoveSpeed(value);PlayerPrefs.SetFloat(preferencesPrefix+"MoveSpeed",MoveSpeedMultiplier);saveAt=Time.unscaledTime+.3f;RefreshSettings();}
        public void ChangeJoystickSize(float value){if(!controlSizes)return;controlSizes.Apply(value,controlSizes.ActionScale);PlayerPrefs.SetFloat(preferencesPrefix+"JoystickSize",controlSizes.JoystickScale);saveAt=Time.unscaledTime+.3f;RefreshSettings();}
        public void ChangeActionSize(float value){if(!controlSizes)return;controlSizes.Apply(controlSizes.JoystickScale,value);PlayerPrefs.SetFloat(preferencesPrefix+"ActionSize",controlSizes.ActionScale);saveAt=Time.unscaledTime+.3f;RefreshSettings();}
        public void ToggleSound(){feedback.SetSound(!feedback.SoundEnabled,false);PlayerPrefs.SetInt(preferencesPrefix+"Sound",feedback.SoundEnabled?1:0);PlayerPrefs.Save();RefreshSettings();}
        public void ToggleHaptics(){feedback.SetHaptics(!feedback.HapticsEnabled,false);PlayerPrefs.SetInt(preferencesPrefix+"Haptics",feedback.HapticsEnabled?1:0);PlayerPrefs.Save();RefreshSettings();}
        public void ResetSettings(){ChangeSensitivity(1);ChangeMoveSpeed(1);ChangeJoystickSize(1);ChangeActionSize(1);feedback.SetSound(true,false);feedback.SetHaptics(true,false);PlayerPrefs.SetInt(preferencesPrefix+"Sound",1);PlayerPrefs.SetInt(preferencesPrefix+"Haptics",1);PlayerPrefs.Save();RefreshSettings();}
        void RefreshSettings(){if(controlSizes){if(joystickSizeLabel)joystickSizeLabel.text="JOYSTICK SIZE  "+Mathf.RoundToInt(controlSizes.JoystickScale*100)+"%";if(actionSizeLabel)actionSizeLabel.text="ACTION SIZE  "+Mathf.RoundToInt(controlSizes.ActionScale*100)+"%";if(joystickSizeSlider)joystickSizeSlider.SetValueWithoutNotify(controlSizes.JoystickScale);if(actionSizeSlider)actionSizeSlider.SetValueWithoutNotify(controlSizes.ActionScale);}if(moveSpeedLabel)moveSpeedLabel.text="MOVE SPEED  "+MoveSpeedMultiplier.ToString("0.00")+"x";sensitivityLabel.text="LOOK SENSITIVITY  "+session.player.look.SensitivityMultiplier.ToString("0.00")+"x";soundLabel.text="SOUND: "+(feedback.SoundEnabled?"ON":"OFF");hapticsLabel.text="HAPTICS: "+(feedback.HapticsEnabled?"ON":"OFF");}
        void FlushSettings(){if(saveAt<0)return;saveAt=-1;PlayerPrefs.Save();}
        void OnApplicationPause(bool paused){if(paused)FlushSettings();}
        void OnApplicationQuit(){FlushSettings();}
        void OnDisable(){FlushSettings();if(IsOpen)Close();}
    }
}


