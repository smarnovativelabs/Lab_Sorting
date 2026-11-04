using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KinematicCharacterController;
namespace LabSorting.Editor
{
    public static class PlayerHeightUpgrade
    {
        const string PlayerPath="Assets/_LabSorting/Prefabs/Player/LabPlayer.prefab";
        [MenuItem("Tools/Lab Sorting/Apply 170cm Player Height")]
        public static void Apply()
        {
            var root=PrefabUtility.LoadPrefabContents(PlayerPath);
            try {Configure(root);PrefabUtility.SaveAsPrefabAsset(root,PlayerPath);}
            finally {PrefabUtility.UnloadPrefabContents(root);}
            var scene=EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            var session=Object.FindFirstObjectByType<LabSorting.Sorting.SortingSession>();
            if(!session) throw new System.InvalidOperationException("SortingSession missing");
            Configure(session.player.gameObject);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_HEIGHT_OK: prefab and SortingTest height=1.70 centerY=0.85 radius=0.30 eye=1.60");
        }
        static void Configure(GameObject player)
        {
            var motor=player.GetComponent<KinematicCharacterMotor>();if(!motor) throw new System.InvalidOperationException("Player motor missing");
            var camera=player.GetComponentInChildren<Camera>(true);
            if(!camera||Mathf.Abs(player.transform.InverseTransformPoint(camera.transform.position).y-1.6f)>.001f) throw new System.InvalidOperationException("Expected existing 1.60m eye level");
            motor.SetCapsuleDimensions(motor.Capsule.radius,1.7f,.85f);
            EditorUtility.SetDirty(motor);EditorUtility.SetDirty(motor.Capsule);
            if(PrefabUtility.IsPartOfPrefabInstance(player)){PrefabUtility.RecordPrefabInstancePropertyModifications(motor);PrefabUtility.RecordPrefabInstancePropertyModifications(motor.Capsule);}
        }
    }
}
