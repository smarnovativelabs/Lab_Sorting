using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class RackLabelIconSetup
    {
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Rack icons must be authored outside Play Mode.");
            var session=Object.FindFirstObjectByType<SortingSession>();
            foreach(var rack in session.racks)
            {
                if(!rack.label)throw new System.Exception("Rack label missing: "+rack.name);
                var label=rack.label.transform;var icon=rack.transform.Find("RackItemIcon");
                if(!icon){icon=new GameObject("RackItemIcon").transform;icon.SetParent(rack.transform,false);}
                var renderer=icon.GetComponent<SpriteRenderer>();if(!renderer)renderer=icon.gameObject.AddComponent<SpriteRenderer>();
                rack.labelIcon=renderer;renderer.color=Color.white;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                label.localPosition=new Vector3(.17f,label.localPosition.y,label.localPosition.z);rack.label.characterSize=.020f;
                icon.localPosition=new Vector3(-.60f,label.localPosition.y,label.localPosition.z-.015f);icon.localRotation=label.localRotation;renderer.sortingOrder=0;
                rack.Refresh();EditorUtility.SetDirty(rack);
            }
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);if(!EditorSceneManager.SaveScene(session.gameObject.scene))throw new System.Exception("Rack icon scene save failed");
        }
    }
}
