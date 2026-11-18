using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    // New-game spawn (requested 2026-11-18): the old spot at z -8.1 stood inside the gap between Rack 5 and
    // Rack 6, with the player's capsule overlapping Rack 6's collider (physics pushed the player out on the first
    // tick). 1.3m forward keeps the same opening view of the lab, overlaps nothing and keeps full hand clearance.
    // Saved games keep their own position; the tour and tutorial rack turn are computed from wherever the player stands.
    public static class SpawnUpgrade
    {
        const string ScenePath="Assets/Scenes/Lab_Sorting.unity";
        static readonly Vector3 OldSpawn=new Vector3(0,0,-8.1f);
        // x -0.2 is where physics used to settle the player; at x 0 a battery pack and Rat 02 overlap the capsule.
        static readonly Vector3 NewSpawn=new Vector3(-.2f,0,-6.8f);
        [MenuItem("Tools/Lab Sorting/Designer/Move Spawn Out Of Rack Gap")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Stop play mode first");
            var session=Object.FindFirstObjectByType<SortingSession>();if(!session||session.gameObject.scene.path!=ScenePath)throw new System.Exception("Open "+ScenePath+" first");
            var root=session.player.character.Motor.transform;var p=root.position;
            // Only move the original spawn, so a later hand-placed spawn is never overwritten.
            if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(OldSpawn.x,OldSpawn.z))>.6f){Debug.Log("Spawn already moved: "+p);return;}
            var target=new Vector3(NewSpawn.x,p.y,NewSpawn.z);Physics.SyncTransforms();
            if(Physics.CheckCapsule(target+Vector3.up*.42f,target+Vector3.up*1.38f,.34f,~(1<<2),QueryTriggerInteraction.Ignore))throw new System.Exception("New spawn "+target+" overlaps scene geometry; not moved");
            Undo.RecordObject(root,"Move spawn");root.position=target;
            EditorUtility.SetDirty(root);PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log("Spawn moved "+p+" -> "+target);
        }
    }
}
