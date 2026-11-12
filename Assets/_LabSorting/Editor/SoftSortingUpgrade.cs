using System;
using System.IO;
using System.Linq;
using LabSorting.Sorting;
using LabSorting.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace LabSorting.Editor
{
    public static class SoftSortingUpgrade
    {
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/Lab_Sorting.unity")throw new InvalidOperationException("Open the designer lab first");
            if(!File.Exists("Logs/LabBeforeSoftDrop.unity"))File.Copy(scene.path,"Logs/LabBeforeSoftDrop.unity");
            var session=UnityEngine.Object.FindFirstObjectByType<SortingSession>();
            var actions=UnityEngine.Object.FindFirstObjectByType<ContextActionHUD>();
            if(actions&&actions.drop){var text=actions.drop.GetComponentInChildren<UnityEngine.UI.Text>(true);Undo.RecordObject(text,"Soft drop label");text.text="DROP";EditorUtility.SetDirty(text);}
            var feedback=session.GetComponent<SortingFeedback>();
            if(feedback&&feedback.dust)
            {
                Undo.RecordObject(feedback.dust,"Subtle drop dust");var main=feedback.dust.main;
                main.maxParticles=24;main.startLifetime=.22f;main.startSpeed=.06f;main.startSize=.025f;main.startColor=new Color(.8f,.8f,.78f,.22f);
                var size=feedback.dust.sizeOverLifetime;size.enabled=false;EditorUtility.SetDirty(feedback.dust);
            }
            // Reposition three existing models, preserving item IDs/counts and compatibility with existing saves.
            int placed=0;var origin=session.player.transform.position;var starterPositions=new System.Collections.Generic.List<Vector3>();
            var samples=session.items.Where(i=>i.location==ItemLocation.Loose&&i.definition.size.y>=.12f&&i.definition.size.y<=.3f&&i.definition.size.x<.3f&&i.definition.size.z<.3f)
                .GroupBy(i=>i.definition).OrderBy(g=>g.Key.id).Take(3).Select(g=>g.First()).ToArray();
            foreach(var item in samples)
            {
                bool done=false;
                var candidates=Enumerable.Range(0,9).SelectMany(z=>Enumerable.Range(0,11).Select(x=>origin+new Vector3((x-5)*.3f,1,.65f+z*.25f))).OrderBy(p=>(p-(origin+new Vector3(0,1,1.4f))).sqrMagnitude);
                foreach(var point in candidates)
                {
                    if(!Physics.Raycast(point,Vector3.down,out var ground,2,~(1<<2),QueryTriggerInteraction.Ignore)||ground.normal.y<.95f||ground.collider.GetComponentInParent<SortItem>()||ground.collider.GetComponentInParent<SortRack>())continue;
                    var position=ground.point+Vector3.up*.015f;
                    if(starterPositions.Any(p=>Vector3.Distance(p,position)<.45f))continue;
                    var hits=Physics.OverlapBox(position+item.definition.center,item.definition.size*.5f+Vector3.one*.01f,Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore);
                    if(hits.Any(h=>!h.transform.IsChildOf(item.transform)))continue;
                    if(Physics.Linecast(session.player.view.transform.position,position+item.definition.center,out var blocker,~(1<<2),QueryTriggerInteraction.Ignore)&&!blocker.collider.transform.IsChildOf(item.transform))continue;
                    Undo.RecordObject(item.transform,"Tutorial starter item");item.transform.SetPositionAndRotation(position,Quaternion.identity);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);Physics.SyncTransforms();starterPositions.Add(position);placed++;done=true;break;
                }
                if(!done)Debug.LogWarning("Starter spacing occupied for "+item.definition.displayName+"; existing position preserved");
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("SOFT_SORTING_UPGRADE starterItems="+placed+" total="+session.items.Length);
        }
    }
}
