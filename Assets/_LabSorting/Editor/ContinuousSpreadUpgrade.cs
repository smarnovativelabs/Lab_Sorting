using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class ContinuousSpreadUpgrade
    {
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Lab_Sorting.unity"||scene.isDirty)throw new Exception("Open saved designer scene");
            System.IO.File.Copy(scene.path,"Logs/LabBeforeContinuousSpread.unity",true);
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var recovery=s.GetComponent<ItemRecovery>();
            var before=s.items.Select(i=>new Pose(i.transform.position,i.transform.rotation)).ToArray();var states=s.items.Select(i=>i.hitbox.enabled).ToArray();bool success=false;
            try
            {
                foreach(var item in s.items)item.hitbox.enabled=false;Physics.SyncTransforms();
                var cells=(IEnumerable)typeof(DesignerCapacitySetup).GetMethod("FloorCells",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{s,s.racks.ToList(),recovery});
                var points=new List<Vector3>();foreach(var cell in cells)points.Add((Vector3)cell.GetType().GetField("point").GetValue(cell));
                if(points.Count==0)throw new Exception("No usable floor");
                var occupied=new Dictionary<Vector3Int,List<Bounds>>();var rng=new System.Random(20261112);int count=0;
                const float cellSize=.4f;
                foreach(var item in s.items.OrderByDescending(i=>Mathf.Max(i.definition.size.x*i.definition.size.z,i.definition.size.y*Mathf.Min(i.definition.size.x,i.definition.size.z))))
                {
                    bool placed=false;
                    for(int attempt=0;attempt<5000&&!placed;attempt++)
                    {
                        var floor=points[rng.Next(points.Count)]+new Vector3(((float)rng.NextDouble()-.5f)*.24f,0,((float)rng.NextDouble()-.5f)*.24f);
                        var rotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
                        if(attempt<3000&&item.definition.size.y>1.6f*Mathf.Max(item.definition.size.x,item.definition.size.z)&&rng.NextDouble()<.35)rotation*=Quaternion.Euler(0,0,90);
                        var b=SortRack.RotatedBounds(item.definition,rotation);var center=floor+Vector3.up*(b.extents.y+.004f);var position=center-b.center;var world=new Bounds(center,b.size+Vector3.one*.003f);
                        if(!recovery.Contains(position)||!recovery.Contains(world.min)||!recovery.Contains(world.max))continue;
                        int x0=Mathf.FloorToInt(world.min.x/cellSize),x1=Mathf.FloorToInt(world.max.x/cellSize),z0=Mathf.FloorToInt(world.min.z/cellSize),z1=Mathf.FloorToInt(world.max.z/cellSize),level=Mathf.RoundToInt(floor.y);
                        bool blocked=false;
                        for(int x=x0;x<=x1&&!blocked;x++)for(int z=z0;z<=z1&&!blocked;z++)if(occupied.TryGetValue(new Vector3Int(x,level,z),out var boxes))foreach(var box in boxes)if(box.Intersects(world)){blocked=true;break;}
                        if(blocked||Physics.CheckBox(center,b.extents-Vector3.one*.001f,Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                        foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})
                        {
                            if(!Physics.Raycast(floor+new Vector3(x*b.extents.x,.06f,z*b.extents.z),Vector3.down,out var support,.1f,~(1<<2),QueryTriggerInteraction.Ignore)||support.normal.y<.95f||Mathf.Abs(support.point.y-floor.y)>.015f||support.collider.GetComponentInParent<SortRack>())blocked=true;
                        }
                        if(blocked)continue;
                        Undo.RecordObject(item.transform,"Continuous floor spread");item.transform.SetPositionAndRotation(position,rotation);PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                        for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++){var key=new Vector3Int(x,level,z);if(!occupied.TryGetValue(key,out var list))occupied[key]=list=new List<Bounds>();list.Add(world);}
                        placed=true;count++;
                    }
                    if(!placed)throw new Exception("No flat position for "+item.id+" after "+count);
                }
                success=true;
                System.IO.File.WriteAllText("Logs/ContinuousSpreadReport.txt","Items="+count+"\nUsable floor samples="+points.Count+"\nLayoutRevision=2\nSingle layer; no pile centres; all bottom corners floor-supported.\n"+string.Join("\n",s.items.GroupBy(i=>Mathf.RoundToInt(i.transform.position.y)).Select(g=>"Floor "+g.Key+": "+g.Count())));
                Debug.Log("CONTINUOUS_SPREAD_OK "+count);
            }
            finally
            {
                for(int i=0;i<s.items.Length;i++){if(!success){s.items[i].transform.SetPositionAndRotation(before[i].position,before[i].rotation);PrefabUtility.RecordPrefabInstancePropertyModifications(s.items[i].transform);}s.items[i].hitbox.enabled=states[i];}
                Physics.SyncTransforms();
            }
            s.layoutRevision=2;EditorUtility.SetDirty(s);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
