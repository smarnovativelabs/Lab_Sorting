using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class SpanishRackUpgrade
    {
        public static void Apply()
        {
            var collection=LocalizationEditorSettings.GetStringTableCollection("LabUI");
            var en=collection.StringTables.First(t=>t.LocaleIdentifier.Code=="en");
            var es=collection.StringTables.First(t=>t.LocaleIdentifier.Code=="es");
            foreach(var line in File.ReadAllLines("Assets/_LabSorting/Localization/SpanishCompletion.txt"))
            {
                var p=line.Split('|');if(p.Length!=2)continue;
                en.AddEntry(p[0],p[0]);es.AddEntry(p[0],p[1]);
            }
            EditorUtility.SetDirty(en);EditorUtility.SetDirty(es);EditorUtility.SetDirty(collection.SharedData);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);
            const string materialPath="Assets/_LabSorting/Settings/PlacementGhost.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!mat){mat=new Material(Shader.Find("LabSorting/PlacementGhost"));AssetDatabase.CreateAsset(mat,materialPath);}
            foreach(var path in new[]{"Assets/Scenes/Lab_Sorting.unity","Assets/_LabSorting/Prototype/Scenes/SortingTest.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path);
                var s=Object.FindFirstObjectByType<SortingSession>();
                var preview=s.GetComponent<RackPlacementPreview>()??s.gameObject.AddComponent<RackPlacementPreview>();preview.previewMaterial=mat;
                foreach(var rack in s.racks)if(!rack.GetComponent<RackTargetFeedback>())rack.gameObject.AddComponent<RackTargetFeedback>();
                foreach(var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                {
                    if(!t.GetComponent<LabLocalizedLabel>())t.gameObject.AddComponent<LabLocalizedLabel>();
                    if(t.text=="+"||t.text=="<"||t.text==">"||t.text=="X")continue;
                    t.resizeTextForBestFit=true;t.resizeTextMaxSize=Mathf.Max(t.fontSize,16);t.resizeTextMinSize=Mathf.Min(t.resizeTextMaxSize,14);
                    t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;
                }
                // Long translated item names need their own line, not a crowded one-line row.
                var progress=Object.FindFirstObjectByType<ItemsProgressPanel>(FindObjectsInactive.Include);
                if(progress)foreach(var row in progress.rows){row.resizeTextMaxSize=18;row.resizeTextMinSize=14;}
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");
        }
    }
}
