using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    // Stores each rack's body mesh before static batching replaces it, for the guide outline.
    public static class RackOutlineUpgrade
    {
        public static string Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Exit Play Mode");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.isDirty)throw new System.Exception("Save scene first");
            int set=0;var skipped=new System.Collections.Generic.List<string>();
            foreach(var rack in Object.FindObjectsByType<SortRack>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var frame=rack.GetComponentsInChildren<MeshRenderer>(true).Where(rack.IsFrameRenderer).ToArray();
                var filter=frame.Length==1?frame[0].GetComponent<MeshFilter>():null;
                if(!filter||!filter.sharedMesh){skipped.Add(rack.name+" ("+frame.Length+" body renderers)");continue;}
                if(rack.outlineMesh==filter.sharedMesh)continue;
                rack.outlineMesh=filter.sharedMesh;EditorUtility.SetDirty(rack);set++;
            }
            if(set>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
            return "set "+set+(skipped.Count>0?", skipped: "+string.Join(", ",skipped):"");
        }
    }
}
