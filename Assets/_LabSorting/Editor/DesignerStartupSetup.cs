using System;
using System.IO;
using System.Linq;
using LabSorting.Services;
using LabSorting.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LabSorting.Editor
{
    public static class DesignerStartupSetup
    {
        const string Boot="Assets/Scenes/MainMenu.unity";
        const string Gameplay="Assets/Scenes/Lab_Sorting.unity";

        // Explicit editor operation, not an InitializeOnLoad mutation.
        public static void Apply()
        {
            if(EditorSceneManager.GetSceneManagerSetup().Any(s=>s.isLoaded&&SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Save open scenes before startup setup.");
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(Gameplay))throw new FileNotFoundException(Gameplay);
            if(File.Exists(Boot))throw new InvalidOperationException("MainMenu already exists; inspect it before updating.");
            var previous=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var canvas=new GameObject("LoadingCanvas",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
                canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
                var background=new GameObject("Background",typeof(RectTransform),typeof(UnityEngine.UI.Image));
                background.transform.SetParent(canvas.transform,false);
                Stretch(background.GetComponent<RectTransform>());
                background.GetComponent<UnityEngine.UI.Image>().color=new Color(.035f,.065f,.08f);
                var safe=new GameObject("SafeArea",typeof(RectTransform),typeof(SafeAreaPanel));
                safe.transform.SetParent(canvas.transform,false);Stretch(safe.GetComponent<RectTransform>());
                var label=new GameObject("Loading",typeof(RectTransform),typeof(UnityEngine.UI.Text));
                label.transform.SetParent(safe.transform,false);Stretch(label.GetComponent<RectTransform>());
                var text=label.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.text="Loading lab…";text.fontSize=30;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
                var boot=new GameObject("LabStartup").AddComponent<LabStartup>();boot.loadingLabel=text;boot.gameplayScenePath=Gameplay;
                EditorSceneManager.SaveScene(scene,Boot);
                var others=EditorBuildSettings.scenes.Where(s=>s.path!=Boot&&s.path!=Gameplay).ToArray();
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Boot,true),new EditorBuildSettingsScene(Gameplay,true)}.Concat(others).ToArray();
                Directory.CreateDirectory("Assets/_LabSorting/Settings");
                const string settings="Assets/_LabSorting/Settings/LabServiceSettings.asset";
                if(!AssetDatabase.LoadAssetAtPath<LabServiceSettings>(settings))
                    AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LabServiceSettings>(),settings);
                AssetDatabase.SaveAssets();
                Debug.Log("LAB_STARTUP_SCENE_READY: MainMenu -> Lab_Sorting, no Play button");
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(previous);}
        }
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
