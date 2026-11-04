using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class InteractionUpgrade
    {
        [MenuItem("Tools/Lab Sorting/Apply Hold Controls")]
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            var session=Object.FindFirstObjectByType<SortingSession>();
            foreach(var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(button.name!="PICK"&&button.name!="PLACE")continue;
                Configure(button,session,button.name=="PLACE");
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("HOLD_CONTROLS_WIRED");
        }
        public static void Configure(UnityEngine.UI.Button button,SortingSession session,bool place)
        {
            button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
            var hold=button.GetComponent<SortingHoldButton>();if(!hold)hold=button.gameObject.AddComponent<SortingHoldButton>();hold.session=session;hold.place=place;EditorUtility.SetDirty(button);EditorUtility.SetDirty(hold);
        }
    }
}
