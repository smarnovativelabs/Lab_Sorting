using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LabSorting.Editor
{
    public static class DesignerSceneInspection
    {
        [Serializable] public class Entry
        {
            public string path,source;public Vector3 position,scale,center,size;
            public string[] materials;public int vertices,submeshes;
        }
        [Serializable] public class Report {public Entry[] scene,items,rack;public string[] colliders;}
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static Entry Describe(Renderer r)
        {
            var filter=r.GetComponent<MeshFilter>();var mesh=filter?filter.sharedMesh:null;
            return new Entry{path=PathOf(r.transform),source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r),position=r.transform.position,scale=r.transform.lossyScale,
                center=r.bounds.center,size=r.bounds.size,materials=r.sharedMaterials.Select(m=>m?m.name:"MISSING").ToArray(),vertices=mesh?mesh.vertexCount:0,submeshes=mesh?mesh.subMeshCount:0};
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");
            var report=new Report{scene=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Select(Describe).ToArray(),
                colliders=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Select(c=>PathOf(c.transform)+" "+c.GetType().Name+" "+c.bounds).ToArray()};
            var items=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_LabSorting/Art/Items/DesignerBatch01/Lab_Items.fbx");
            var rack=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LabSorting/Art/Fbx/Lab_Rack.fbx");
            report.items=items.GetComponentsInChildren<Renderer>(true).Select(Describe).ToArray();
            report.rack=rack.GetComponentsInChildren<Renderer>(true).Select(Describe).ToArray();
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DesignerSceneInspection.json",JsonUtility.ToJson(report,true));
            Debug.Log("DESIGNER_INSPECTION_OK items="+report.items.Length+" renderers="+report.scene.Length+" colliders="+report.colliders.Length);
        }
    }
}
