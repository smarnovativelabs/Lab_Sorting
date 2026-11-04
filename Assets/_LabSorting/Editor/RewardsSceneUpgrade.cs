using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class RewardsSceneUpgrade
    {
        [MenuItem("Tools/Lab Sorting/Add Milestones and Ability Feedback")]
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);var hud=Object.FindFirstObjectByType<SortingHUD>();var safe=(RectTransform)hud.transform.Find("SafeArea");
            var rewards=hud.GetComponent<SortingRewardsHUD>();if(rewards){Debug.Log("REWARDS_UI_ALREADY_WIRED");return;}
            rewards=hud.gameObject.AddComponent<SortingRewardsHUD>();rewards.session=hud.session;rewards.safeArea=safe;
            rewards.milestone=Label("NextMilestone",safe,new Vector2(.5f,1),new Vector2(0,-98),new Vector2(520,44),17);
            rewards.milestone.color=new Color(.65f,1,.85f);
            rewards.toast=Label("MilestoneReward",safe,new Vector2(.5f,1),new Vector2(0,-145),new Vector2(500,40),21);
            rewards.ability=Label("AbilityFeedback",safe,new Vector2(.5f,0),new Vector2(0,238),new Vector2(500,36),20);
            var background=new GameObject("SelectedItemCard",typeof(RectTransform),typeof(UnityEngine.UI.Image));var rect=(RectTransform)background.transform;rect.SetParent(safe,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,173);rect.sizeDelta=new Vector2(400,65);background.GetComponent<UnityEngine.UI.Image>().color=new Color(.04f,.12f,.16f,.94f);background.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;rect.SetSiblingIndex(hud.active.transform.GetSiblingIndex());
            var swatch=new GameObject("SelectedTypeColor",typeof(RectTransform),typeof(UnityEngine.UI.Image));var sw=(RectTransform)swatch.transform;sw.SetParent(rect,false);sw.anchoredPosition=new Vector2(-175,0);sw.sizeDelta=new Vector2(20,36);rewards.selectedSwatch=swatch.GetComponent<UnityEngine.UI.Image>();rewards.selectedSwatch.raycastTarget=false;
            hud.active.rectTransform.anchoredPosition=new Vector2(10,173);hud.active.rectTransform.sizeDelta=new Vector2(345,62);hud.active.fontSize=19;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("REWARDS_UI_WIRED");
        }
        static UnityEngine.UI.Text Label(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,int font)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Text));var rect=(RectTransform)go.transform;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=pos;rect.sizeDelta=size;var text=go.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=font;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;return text;
        }
    }
}
