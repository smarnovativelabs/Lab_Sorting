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
    public static class BulkPileUpgrade
    {
        sealed class Pile{public Vector3 floor;public float[,] height=new float[40,40];public int count;}
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Lab_Sorting.unity"||scene.isDirty)throw new Exception("Open saved designer scene first");
            System.IO.File.Copy(scene.path,"Logs/LabBeforeBulkPiles.unity",true);
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var recovery=s.GetComponent<ItemRecovery>();
            var positions=s.items.Select(i=>i.transform.position).ToArray();var rotations=s.items.Select(i=>i.transform.rotation).ToArray();var enabled=s.items.Select(i=>i.hitbox.enabled).ToArray();bool success=false;
            try
            {
                foreach(var item in s.items)item.hitbox.enabled=false;Physics.SyncTransforms();
                var method=typeof(DesignerCapacitySetup).GetMethod("FloorCells",BindingFlags.Static|BindingFlags.NonPublic);
                var cells=(IEnumerable)method.Invoke(null,new object[]{s,s.racks.ToList(),recovery});var points=new List<Vector3>();
                foreach(var cell in cells)points.Add((Vector3)cell.GetType().GetField("point").GetValue(cell));
                var rng=new System.Random(20261111);var piles=new List<Pile>();
                // Spread centres across every supported floor. Preserve rack fronts and spawn/walkway exclusions.
                foreach(var p in points.OrderBy(_=>rng.Next()))
                {
                    if(piles.Any(v=>Mathf.Abs(v.floor.y-p.y)<1&&Vector2.Distance(new Vector2(v.floor.x,v.floor.z),new Vector2(p.x,p.z))<3.3f))continue;
                    if(Physics.CheckBox(p+Vector3.up*.5f,new Vector3(1.05f,.47f,1.05f),Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                    bool supported=true;foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})if(!Physics.Raycast(p+new Vector3(x,.08f,z),Vector3.down,.12f,~(1<<2),QueryTriggerInteraction.Ignore))supported=false;
                    if(supported)piles.Add(new Pile{floor=p});
                }
                if(piles.Count<4)throw new Exception("Insufficient safe bulk areas: "+piles.Count);
                int index=0;
                // Largest footprints first make stable lower layers; each pile still contains mixed types.
                foreach(var item in s.items.OrderByDescending(i=>i.definition.size.x*i.definition.size.z))
                {
                    bool placed=false;
                    for(int pileIndex=0;pileIndex<piles.Count&&!placed;pileIndex++)
                    {
                        var pile=piles[(index+pileIndex)%piles.Count];float best=float.MaxValue;int bx=0,bz=0,bw=0,bd=0;float height=0;Vector3 bestPosition=default;Quaternion bestRotation=default;
                        for(int attempt=0;attempt<80;attempt++)
                        {
                        var rotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
                        if(item.definition.size.y>1.5f*Mathf.Max(item.definition.size.x,item.definition.size.z)&&rng.NextDouble()<.75)rotation*=Quaternion.Euler(0,0,90);
                        var b=SortRack.RotatedBounds(item.definition,rotation);int w=Mathf.CeilToInt((b.size.x+.012f)/.05f),d=Mathf.CeilToInt((b.size.z+.012f)/.05f);if(w>=40||d>=40)continue;
                        int x=rng.Next(0,40-w),z=rng.Next(0,40-d);float top=0;
                        for(int a=x;a<x+w;a++)for(int c=z;c<z+d;c++)top=Mathf.Max(top,pile.height[a,c]);
                        if(top+b.size.y>1.05f||top+b.size.y>=best)continue;
                        var center=pile.floor+new Vector3(-1+(x+w*.5f)*.05f,top+.006f+b.extents.y,-1+(z+d*.5f)*.05f);var position=center-b.center;
                        if(!recovery.Contains(position)||!recovery.Contains(center+b.extents)||!recovery.Contains(center-b.extents))continue;
                        if(Physics.CheckBox(center,b.extents,Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                        best=top+b.size.y;bx=x;bz=z;bw=w;bd=d;height=best+.012f;bestPosition=position;bestRotation=rotation;
                        }
                        if(best==float.MaxValue)continue;
                        Undo.RecordObject(item.transform,"Bulk pile distribution");item.transform.SetPositionAndRotation(bestPosition,bestRotation);PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                        for(int a=bx;a<bx+bw;a++)for(int c=bz;c<bz+bd;c++)pile.height[a,c]=height;
                        pile.count++;placed=true;index++;
                    }
                    if(!placed)throw new Exception("Could not safely pile "+item.id+" after "+index+" items across "+piles.Count+" piles");
                }
                success=true;System.IO.File.WriteAllText("Logs/BulkPileReport.txt","Items: "+s.items.Length+"\nPiles: "+piles.Count+"\n"+string.Join("\n",piles.Select(p=>p.floor+" items="+p.count)));
                Debug.Log("BULK_PILES_OK items="+s.items.Length+" piles="+piles.Count);
            }
            finally
            {
                for(int i=0;i<s.items.Length;i++){if(!success){s.items[i].transform.SetPositionAndRotation(positions[i],rotations[i]);PrefabUtility.RecordPrefabInstancePropertyModifications(s.items[i].transform);}s.items[i].hitbox.enabled=enabled[i];}
                Physics.SyncTransforms();
            }
            s.layoutRevision=1;EditorUtility.SetDirty(s);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
