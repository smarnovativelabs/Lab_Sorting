using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class ControlWelcomeUpgrade
    {
        static void Row(UnityEngine.UI.Slider slider,UnityEngine.UI.Text label,float x,float y)
        {
            var r=(RectTransform)slider.transform;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(250,42);
            ((RectTransform)r.Find("Track")).sizeDelta=new Vector2(250,10);
            ((RectTransform)r.Find("HandleArea")).sizeDelta=new Vector2(220,42);
            label.rectTransform.anchoredPosition=new Vector2(x,y+45);label.rectTransform.sizeDelta=new Vector2(290,40);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=14;label.resizeTextMaxSize=20;
        }
        public static void Apply()
        {
            var h=Object.FindFirstObjectByType<SortingHUD>();var s=h.GetComponent<SettingsTutorialPanel>();var g=h.GetComponent<GuidedTutorial>();var a=h.GetComponent<ContextActionHUD>();var e=h.GetComponent<EconomyHUD>();
            s.controlSizes=h.GetComponent<ControlSizes>()??h.gameObject.AddComponent<ControlSizes>();
            s.controlSizes.joystick=g.joystick;s.controlSizes.actions=new[]{(RectTransform)a.pick.transform,(RectTransform)a.place.transform,(RectTransform)a.drop.transform,(RectTransform)e.searchButton.transform,(RectTransform)e.magnetButton.transform};
            ((RectTransform)a.drop.transform).anchoredPosition=new Vector2(-180,190);
            ((RectTransform)e.searchButton.transform).anchoredPosition=new Vector2(-72,-355);
            ((RectTransform)e.magnetButton.transform).anchoredPosition=new Vector2(-72,-460);
            if(!s.joystickSizeSlider){s.joystickSizeSlider=Object.Instantiate(s.moveSpeedSlider,s.settingsContent.transform);s.joystickSizeSlider.name="JoystickSize";s.joystickSizeSlider.onValueChanged=new UnityEngine.UI.Slider.SliderEvent();UnityEventTools.AddPersistentListener(s.joystickSizeSlider.onValueChanged,s.ChangeJoystickSize);s.joystickSizeLabel=Object.Instantiate(s.moveSpeedLabel,s.settingsContent.transform);s.joystickSizeLabel.name="JoystickSizeLabel";}
            if(!s.actionSizeSlider){s.actionSizeSlider=Object.Instantiate(s.moveSpeedSlider,s.settingsContent.transform);s.actionSizeSlider.name="ActionSize";s.actionSizeSlider.onValueChanged=new UnityEngine.UI.Slider.SliderEvent();UnityEventTools.AddPersistentListener(s.actionSizeSlider.onValueChanged,s.ChangeActionSize);s.actionSizeLabel=Object.Instantiate(s.moveSpeedLabel,s.settingsContent.transform);s.actionSizeLabel.name="ActionSizeLabel";}
            foreach(var slider in new[]{s.joystickSizeSlider,s.actionSizeSlider}){slider.minValue=.85f;slider.maxValue=1.15f;slider.SetValueWithoutNotify(1);}
            s.joystickSizeLabel.text="JOYSTICK SIZE  100%";s.actionSizeLabel.text="ACTION SIZE  100%";
            Row(s.sensitivitySlider,s.sensitivityLabel,-150,140);Row(s.moveSpeedSlider,s.moveSpeedLabel,150,140);
            Row(s.joystickSizeSlider,s.joystickSizeLabel,-150,20);Row(s.actionSizeSlider,s.actionSizeLabel,150,20);
            s.settingsContent.transform.Find("SliderRange").gameObject.SetActive(false);
            var welcome=h.GetComponent<WelcomeBackSummary>()??h.gameObject.AddComponent<WelcomeBackSummary>();welcome.session=h.session;welcome.presentation=h.GetComponent<ServicePresentation>();welcome.tutorial=g;welcome.tour=h.GetComponent<FirstLaunchTour>();
            var collection=LocalizationEditorSettings.GetStringTableCollection("LabUI");string[] en={"JOYSTICK SIZE","ACTION SIZE","WELCOME BACK!","sorted","remaining"};string[] es={"TAMAÑO DEL JOYSTICK","TAMAÑO DE BOTONES","¡BIENVENIDO DE NUEVO!","ordenados","restantes"};
            foreach(var table in collection.StringTables){for(int i=0;i<en.Length;i++)table.AddEntry(en[i],table.LocaleIdentifier.Code=="es"?es[i]:en[i]);EditorUtility.SetDirty(table);}EditorUtility.SetDirty(collection.SharedData);EditorUtility.SetDirty(collection);LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene);EditorSceneManager.SaveScene(h.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
