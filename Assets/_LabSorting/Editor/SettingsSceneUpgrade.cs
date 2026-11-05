using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using LabSorting.UI;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class SettingsSceneUpgrade
    {
        [MenuItem("Tools/Lab Sorting/Add Tutorial and Settings")]
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);var hud=Object.FindFirstObjectByType<SortingHUD>();if(hud.GetComponent<SettingsTutorialPanel>()){hud.GetComponent<SettingsTutorialPanel>().sensitivitySlider.handleRect.sizeDelta=new Vector2(32,0);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("SETTINGS_LAYOUT_UPDATED");return;}
            var panel=hud.gameObject.AddComponent<SettingsTutorialPanel>();panel.session=hud.session;panel.feedback=hud.session.GetComponent<SortingFeedback>();var safe=hud.transform.Find("SafeArea");
            panel.openButton=Button("SettingsButton",safe,new Vector2(0,1),new Vector2(145,-265),new Vector2(240,46),"SETTINGS  [F1]",panel.OpenSettings);
            var root=Rect("SettingsTutorialModal",safe,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;root.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.8f);panel.root=root.gameObject;
            var card=Rect("Card",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(700,490));card.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.05f,.12f,.17f,1);
            panel.title=Label("Title",card,new Vector2(0,197),new Vector2(580,45),25);panel.closeButton=Button("Close",card,new Vector2(.5f,.5f),new Vector2(305,204),new Vector2(52,42),"X",panel.CloseRequested);
            panel.settingsContent=Rect("Settings",card,new Vector2(.5f,.5f),Vector2.zero,new Vector2(680,390)).gameObject;
            var content=panel.settingsContent.transform;
            panel.sensitivityLabel=Label("SensitivityLabel",content,new Vector2(0,130),new Vector2(600,40),22);
            var sliderRect=Rect("Sensitivity",content,new Vector2(.5f,.5f),new Vector2(0,75),new Vector2(500,42));panel.sensitivitySlider=sliderRect.gameObject.AddComponent<UnityEngine.UI.Slider>();panel.sensitivitySlider.minValue=.5f;panel.sensitivitySlider.maxValue=2;panel.sensitivitySlider.value=1;
            var track=Rect("Track",sliderRect,new Vector2(.5f,.5f),Vector2.zero,new Vector2(500,10));track.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.15f,.29f,.34f);var handleArea=Rect("HandleArea",sliderRect,new Vector2(.5f,.5f),Vector2.zero,new Vector2(470,42));var handle=Rect("Handle",handleArea,new Vector2(.5f,.5f),Vector2.zero,new Vector2(32,0));var handleImage=handle.gameObject.AddComponent<UnityEngine.UI.Image>();handleImage.color=new Color(.25f,.9f,.74f);panel.sensitivitySlider.handleRect=handle;panel.sensitivitySlider.targetGraphic=handleImage;UnityEventTools.AddPersistentListener(panel.sensitivitySlider.onValueChanged,panel.ChangeSensitivity);
            Label("SliderRange",content,new Vector2(0,35),new Vector2(600,26),16).text="0.50x slow                         1.00x default                         2.00x fast";
            panel.soundButton=Button("Sound",content,new Vector2(.5f,.5f),new Vector2(-150,-25),new Vector2(270,52),"",panel.ToggleSound);panel.soundLabel=panel.soundButton.GetComponentInChildren<UnityEngine.UI.Text>();
            panel.hapticsButton=Button("Haptics",content,new Vector2(.5f,.5f),new Vector2(150,-25),new Vector2(270,52),"",panel.ToggleHaptics);panel.hapticsLabel=panel.hapticsButton.GetComponentInChildren<UnityEngine.UI.Text>();
            panel.replayButton=Button("ReplayTutorial",content,new Vector2(.5f,.5f),new Vector2(-150,-100),new Vector2(270,52),"REPLAY TUTORIAL",panel.ReplayTutorial);panel.resetButton=Button("ResetSettings",content,new Vector2(.5f,.5f),new Vector2(150,-100),new Vector2(270,52),"RESET SETTINGS",panel.ResetSettings);
            Label("Help",content,new Vector2(0,-170),new Vector2(600,44),16).text="Changes save automatically. Close this panel to try your look speed.\nReset changes controls and feedback only; sorting progress stays safe.";
            panel.tutorialContent=Rect("Tutorial",card,new Vector2(.5f,.5f),Vector2.zero,new Vector2(680,390)).gameObject;content=panel.tutorialContent.transform;
            panel.tutorialText=Label("Instructions",content,new Vector2(0,20),new Vector2(590,255),23);panel.pageLabel=Label("Page",content,new Vector2(0,-133),new Vector2(100,32),18);
            panel.backButton=Button("Back",content,new Vector2(.5f,.5f),new Vector2(-220,-190),new Vector2(160,48),"BACK",panel.Back);panel.skipButton=Button("Skip",content,new Vector2(.5f,.5f),new Vector2(0,-190),new Vector2(160,48),"SKIP",panel.SkipTutorial);panel.nextButton=Button("Next",content,new Vector2(.5f,.5f),new Vector2(220,-190),new Vector2(160,48),"NEXT",panel.Next);panel.nextLabel=panel.nextButton.GetComponentInChildren<UnityEngine.UI.Text>();
            panel.root.SetActive(false);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("SETTINGS_TUTORIAL_WIRED");
        }
        static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=position;r.sizeDelta=size;return r;}
        static UnityEngine.UI.Text Label(string name,Transform parent,Vector2 position,Vector2 size,int font){var r=Rect(name,parent,new Vector2(.5f,.5f),position,size);var t=r.gameObject.AddComponent<UnityEngine.UI.Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=font;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;return t;}
        static UnityEngine.UI.Button Button(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,string label,UnityEngine.Events.UnityAction action){var r=Rect(name,parent,anchor,position,size);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.1f,.3f,.36f);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;UnityEventTools.AddPersistentListener(b.onClick,action);Label("Label",r,Vector2.zero,size,19).text=label;return b;}
    }
}

