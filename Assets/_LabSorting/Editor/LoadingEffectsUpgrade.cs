using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
using LabSorting.Services;
namespace LabSorting.Editor
{
    public static class LoadingEffectsUpgrade
    {
        public static void Apply()
        {
            var boot=Object.FindFirstObjectByType<LabStartup>();var canvas=boot.loadingLabel.GetComponentInParent<Canvas>();
            var p=boot.GetComponent<LoadingPresentation>();if(!p)p=boot.gameObject.AddComponent<LoadingPresentation>();p.startup=boot;p.logo=(RectTransform)canvas.transform.Find("DesignerLoadingLogo");p.background=(RectTransform)canvas.transform.Find("DesignerLoadingBackground");
            p.background.anchorMin=p.background.anchorMax=Vector2.one*.5f;p.background.anchoredPosition=Vector2.zero;p.background.GetComponent<UnityEngine.UI.Image>().preserveAspect=true;
            if(!p.tip){var go=new GameObject("LoadingTip",typeof(RectTransform),typeof(UnityEngine.UI.Text));go.transform.SetParent(canvas.transform.Find("SafeArea"),false);p.tip=go.GetComponent<UnityEngine.UI.Text>();p.tip.font=boot.loadingLabel.font;p.tip.alignment=TextAnchor.MiddleCenter;p.tip.fontSize=19;p.tip.resizeTextForBestFit=true;p.tip.resizeTextMinSize=16;p.tip.resizeTextMaxSize=19;p.tip.raycastTarget=false;p.tip.color=Color.white;p.tip.rectTransform.anchoredPosition=new Vector2(0,-248);p.tip.rectTransform.sizeDelta=new Vector2(650,54);}
            var track=(RectTransform)canvas.transform.Find("DesignerLoadingTrack");if(!p.shine){var go=new GameObject("ProgressShine",typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(track,false);p.shine=(RectTransform)go.transform;p.shine.sizeDelta=new Vector2(9,track.sizeDelta.y-8);var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(1,1,1,.23f);image.raycastTarget=false;}
            p.musicClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_LabSorting/Audio/Presentation/LabMusic.wav");if(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==0)boot.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);EditorSceneManager.SaveScene(boot.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
