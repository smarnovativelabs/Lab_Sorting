using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class RapidFlowUpgrade
    {
        public static void Apply()
        {
            foreach(var path in new[]{"Assets/Scenes/Lab_Sorting.unity","Assets/_LabSorting/Prototype/Scenes/SortingTest.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path);var hud=Object.FindFirstObjectByType<SortingRewardsHUD>();
                hud.milestone.rectTransform.anchoredPosition=new Vector2(0,-90);hud.milestone.rectTransform.sizeDelta=new Vector2(620,44);hud.milestone.resizeTextForBestFit=true;hud.milestone.resizeTextMaxSize=18;hud.milestone.resizeTextMinSize=14;
                var existing=hud.safeArea.Find("ShortGoalTrack");var track=existing?(RectTransform)existing:new GameObject("ShortGoalTrack",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
                track.SetParent(hud.safeArea,false);track.anchorMin=track.anchorMax=new Vector2(.5f,1);track.anchoredPosition=new Vector2(0,-118);track.sizeDelta=new Vector2(360,5);var bg=track.GetComponent<UnityEngine.UI.Image>();bg.color=new Color(.08f,.18f,.22f,.95f);bg.raycastTarget=false;
                var child=track.Find("Fill");var fill=child?(RectTransform)child:new GameObject("Fill",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();fill.SetParent(track,false);fill.anchorMin=Vector2.zero;fill.anchorMax=new Vector2(0,1);fill.offsetMin=fill.offsetMax=Vector2.zero;var image=fill.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.3f,.95f,.7f);image.raycastTarget=false;hud.shortGoalFill=fill;
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");
        }
    }
}
