using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class TutorialToolsUpgrade
    {
        public static void Apply()
        {
            var hud=Object.FindFirstObjectByType<SortingHUD>();var tutorial=hud.GetComponent<GuidedTutorial>();
            tutorial.capacityButton=(RectTransform)hud.inventoryPanel.transform.Find("CapacityPlus");
            if(!tutorial.capacityButton)throw new System.Exception("Capacity plus missing");
            var collection=LocalizationEditorSettings.GetStringTableCollection("LabUI");
            string[] english={"SEARCH highlights loose items of your selected type.","MAGNET collects your selected type until your hands are full.","Tap + beside Collected Items to add slots with cash or a reward video.","NEXT"};
            string[] spanish={"SEARCH resalta los objetos sueltos del tipo seleccionado.","MAGNET recoge el tipo seleccionado hasta llenar tus manos.","Pulsa + junto al inventario para añadir espacios con dinero o un vídeo.","SIGUIENTE"};
            foreach(var table in collection.StringTables){for(int i=0;i<english.Length;i++)table.AddEntry(english[i],table.LocaleIdentifier.Code=="es"?spanish[i]:english[i]);EditorUtility.SetDirty(table);}
            EditorUtility.SetDirty(collection.SharedData);EditorUtility.SetDirty(collection);LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
