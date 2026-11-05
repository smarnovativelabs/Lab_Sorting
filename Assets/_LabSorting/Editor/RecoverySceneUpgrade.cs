using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class RecoverySceneUpgrade
    {
        [MenuItem("Tools/Lab Sorting/Add Prototype Recovery Area")]
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);var s=Object.FindFirstObjectByType<SortingSession>();if(s.GetComponent<ItemRecovery>()){Debug.Log("RECOVERY_ALREADY_WIRED");return;}
            var recovery=s.gameObject.AddComponent<ItemRecovery>();recovery.session=s;
            var volume=new GameObject("PlayableBounds");volume.transform.position=new Vector3(0,3,0);var box=volume.AddComponent<BoxCollider>();box.size=new Vector3(18,8,16);box.isTrigger=true;recovery.playableBounds=box;
            var area=new GameObject("RecoveryPoints");recovery.safePoints=new Transform[25];for(int i=0;i<25;i++){var point=new GameObject("SafePoint "+i).transform;point.SetParent(area.transform);point.position=new Vector3((i%5-2)*.7f,1,-4.6f-(i/5)*.65f);recovery.safePoints[i]=point;}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("RECOVERY_WIRED");
        }
    }
}
