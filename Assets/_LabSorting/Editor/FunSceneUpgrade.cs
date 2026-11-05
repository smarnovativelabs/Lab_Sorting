using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class FunSceneUpgrade
    {
        [MenuItem("Tools/Lab Sorting/Apply Short Goals and Context Controls")]
        public static void Apply()
        {
            foreach(var path in new[]{PrototypeBuilder.ScenePath,StressSceneUpgrade.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);var hud=Object.FindFirstObjectByType<SortingHUD>();
                var ui=hud.GetComponent<ContextActionHUD>();if(!ui)ui=hud.gameObject.AddComponent<ContextActionHUD>();ui.session=hud.session;
                var buttons=hud.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                ui.pick=Group(buttons.Single(b=>b.name=="PICK"));ui.place=Group(buttons.Single(b=>b.name=="PLACE"));ui.drop=Group(buttons.Single(b=>b.name=="THROW"));
                ui.pickHold=ui.pick.GetComponent<SortingHoldButton>();ui.placeHold=ui.place.GetComponent<SortingHoldButton>();
                ui.pick.alpha=ui.place.alpha=ui.drop.alpha=0;ui.pick.blocksRaycasts=ui.place.blocksRaycasts=ui.drop.blocksRaycasts=false;
                var rewards=hud.GetComponent<SortingRewardsHUD>();rewards.milestone.fontSize=16;
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);Debug.Log("FUN_CONTROLS_WIRED");
        }
        static CanvasGroup Group(UnityEngine.UI.Button button){var group=button.GetComponent<CanvasGroup>();return group?group:button.gameObject.AddComponent<CanvasGroup>();}
    }
}
