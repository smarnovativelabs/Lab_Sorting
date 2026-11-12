using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class RackHintsUpgrade
    {
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Exit Play Mode");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.isDirty)throw new System.Exception("Save scene first");
            var s=Object.FindFirstObjectByType<SortingSession>();
            const string path="Assets/_LabSorting/Designer/CompatibleRackHint.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(s.racks[0].neutral);material.name="CompatibleRackHint";material.color=new Color(.15f,.40f,.34f,1);AssetDatabase.CreateAsset(material,path);}
            foreach(var rack in s.racks){rack.compatible=material;EditorUtility.SetDirty(rack);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
    }
}
