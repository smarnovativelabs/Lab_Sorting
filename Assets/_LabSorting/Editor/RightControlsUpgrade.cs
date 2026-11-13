using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class RightControlsUpgrade
    {
        static void Position(Component c,Vector2 anchor,Vector2 position){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=position;}
        public static void Apply()
        {
            var h=Object.FindFirstObjectByType<SortingHUD>();var e=h.GetComponent<EconomyHUD>();var a=h.GetComponent<ContextActionHUD>();
            h.GetComponent<TaskCardLayout>().card.anchoredPosition=new Vector2(-238,-185);
            h.GetComponent<GuidedTutorial>().bubble.anchoredPosition=new Vector2(-238,-185);
            Position(e.searchButton,new Vector2(1,1),new Vector2(-72,-330));Position(e.magnetButton,new Vector2(1,1),new Vector2(-72,-435));
            Position(a.pick,new Vector2(1,0),new Vector2(-76,80));Position(a.place,new Vector2(1,0),new Vector2(-180,80));Position(a.drop,new Vector2(1,0),new Vector2(-284,80));
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene);EditorSceneManager.SaveScene(h.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
