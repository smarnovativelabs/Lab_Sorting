using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LabSorting.Sorting;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LabSorting.Editor
{
    // Puts every item type on every point of every rack (virtually) and reports anything wrong:
    // hitting the rack frame, a wall or another rack, floating or sinking, hanging off a board, or touching a neighbour.
    public static class DesignerRackCheck
    {
        const string ReportPath="Logs/RackPlacementCheck.json";
        const string PreviewName="Rack Fill Preview (not saved)";
        const int Mask=~(1<<2), MaxListed=300;
        [Serializable] public class Issue { public string rack,item,problem,detail; public int point; }
        [Serializable] public class Capacity { public string item,sizeCm; public int pointsPerRack; }
        [Serializable] public class Result
        {
            public int racks,types,pointsChecked,problems;
            public List<string> problemCounts=new List<string>();
            public List<Capacity> capacity=new List<Capacity>();
            public List<Issue> issues=new List<Issue>();
        }

        [MenuItem("Tools/Lab Sorting/Designer/Check Every Rack Point")]
        public static void Menu()
        {
            var r=Run();
            EditorUtility.DisplayDialog("Rack placement check",r.problems==0?$"All {r.pointsChecked:N0} points are fine ({r.racks} racks x {r.types} item types).":$"{r.problems:N0} problems found.\n\n{string.Join("\n",r.problemCounts)}\n\nDetails: {ReportPath}","OK");
        }

        public static Result Run()
        {
            var session=Object.FindFirstObjectByType<SortingSession>();
            if(!session) throw new InvalidOperationException("Open the lab scene first.");
            var racks=session.racks.Where(r=>r).ToArray();
            var types=session.items.Where(i=>i&&i.definition).Select(i=>i.definition).Distinct().OrderBy(d=>d.id,StringComparer.Ordinal).ToList();
            Physics.SyncTransforms();
            var result=new Result{racks=racks.Length,types=types.Count};
            var counts=new Dictionary<string,int>();
            void Report(SortRack rack,ItemDefinition type,int point,string problem,string detail)
            {
                counts[problem]=counts.TryGetValue(problem,out var c)?c+1:1;
                if(result.issues.Count<MaxListed)result.issues.Add(new Issue{rack=rack.name,item=type.displayName,point=point,problem=problem,detail=detail});
            }
            foreach(var type in types)
            {
                var s=type.size*100;
                result.capacity.Add(new Capacity{item=type.displayName,sizeCm=$"{s.x:0}x{s.y:0}x{s.z:0}",pointsPerRack=racks.Length>0?racks.Min(r=>r.CapacityFor(type)):0});
            }
            var overlaps=new Collider[16];var layoutsChecked=new HashSet<string>();
            for(int r=0;r<racks.Length;r++)
            {
                var rack=racks[r];
                // The neighbour check depends only on the shelf layout, so run it once per distinct layout.
                bool checkNeighbours=layoutsChecked.Add(string.Join("|",(rack.shelves??Array.Empty<RackShelf>()).Select(s=>$"{s.height:F4},{s.clearance:F4},{s.x},{s.z}"))+rack.itemGap);
                EditorUtility.DisplayProgressBar("Rack placement check",rack.name,r/(float)racks.Length);
                try
                {
                    foreach(var type in types)
                    {
                        int capacity=rack.CapacityFor(type);
                        if(capacity==0){Report(rack,type,-1,"NO_POINTS","Item does not fit any shelf.");continue;}
                        var boxes=new List<Bounds>(capacity);
                        for(int i=0;i<capacity;i++)
                        {
                            var pose=rack.SlotPoseFor(type,i);result.pointsChecked++;
                            var center=pose.position+pose.rotation*type.center;var half=type.size*.5f;
                            // 1. Nothing solid inside the item (rack frame, posts, walls, other racks, loose items).
                            int n=Physics.OverlapBoxNonAlloc(center,half-Vector3.one*.004f,overlaps,pose.rotation,Mask,QueryTriggerInteraction.Ignore);
                            for(int k=0;k<n;k++)Report(rack,type,i,overlaps[k].GetComponentInParent<SortItem>()?"LOOSE_ITEM_IN_THE_WAY":"BLOCKED",PathOf(overlaps[k].transform));
                            // 2. Resting on this rack's board at every bottom corner: not floating, not sinking, not hanging off.
                            for(int corner=0;corner<5;corner++)
                            {
                                var offset=corner==4?Vector3.zero:new Vector3((corner&1)==0?-1:1,0,(corner&2)==0?-1:1);
                                var local=type.center+new Vector3(offset.x*Mathf.Max(0,half.x-.01f),-half.y,offset.z*Mathf.Max(0,half.z-.01f));
                                var bottom=pose.position+pose.rotation*local;
                                if(!Physics.Raycast(bottom+Vector3.up*.03f,Vector3.down,out var hit,.06f,Mask,QueryTriggerInteraction.Ignore)||!hit.collider.transform.IsChildOf(rack.transform))
                                {Report(rack,type,i,"NOT_SUPPORTED","Corner "+corner+" has no shelf under it.");break;}
                                float gap=bottom.y-hit.point.y;
                                if(gap>.004f){Report(rack,type,i,"FLOATING",$"{gap*1000:0.0} mm above the shelf");break;}
                                if(gap<-.001f){Report(rack,type,i,"SINKING",$"{-gap*1000:0.0} mm into the shelf");break;}
                            }
                            // Rack-local box for the neighbour check.
                            var localRotation=Quaternion.Inverse(rack.transform.rotation)*pose.rotation;
                            var lb=SortRack.RotatedBounds(type,localRotation);lb.center+=rack.transform.InverseTransformPoint(pose.position);boxes.Add(lb);
                        }
                        // 3. Items on the same rack never touch each other.
                        if(checkNeighbours)for(int a=0;a<boxes.Count;a++)for(int b=a+1;b<boxes.Count;b++)
                        {
                            var A=boxes[a];var B=boxes[b];
                            float ox=Mathf.Min(A.max.x,B.max.x)-Mathf.Max(A.min.x,B.min.x),oy=Mathf.Min(A.max.y,B.max.y)-Mathf.Max(A.min.y,B.min.y),oz=Mathf.Min(A.max.z,B.max.z)-Mathf.Max(A.min.z,B.min.z);
                            if(ox>.001f&&oy>.001f&&oz>.001f)Report(rack,type,a,"ITEMS_OVERLAP","Overlaps point "+b);
                        }
                    }
                }
                finally{EditorUtility.ClearProgressBar();}
            }
            result.problems=counts.Values.Sum();
            result.problemCounts=counts.OrderByDescending(p=>p.Value).Select(p=>p.Key+": "+p.Value).ToList();
            Directory.CreateDirectory("Logs");File.WriteAllText(ReportPath,JsonUtility.ToJson(result,true));
            Debug.Log($"RACK_CHECK racks={result.racks} types={result.types} points={result.pointsChecked} problems={result.problems} {string.Join(", ",result.problemCounts)}");
            return result;
        }

        // Fills each rack with a different item type so the layout can be inspected in the Scene view. Never saved.
        [MenuItem("Tools/Lab Sorting/Designer/Preview: Fill Every Rack")]
        public static int PreviewFill()
        {
            ClearPreview();
            var session=Object.FindFirstObjectByType<SortingSession>();
            var samples=session.items.Where(i=>i&&i.definition&&i.visual).GroupBy(i=>i.definition).OrderBy(g=>g.Key.id,StringComparer.Ordinal).Select(g=>g.First()).ToList();
            var root=new GameObject(PreviewName){hideFlags=HideFlags.DontSave};int count=0;
            for(int r=0;r<session.racks.Length&&samples.Count>0;r++)
            {
                var rack=session.racks[r];var sample=samples[r%samples.Count];var type=sample.definition;
                var mesh=sample.visual.GetComponent<MeshFilter>().sharedMesh;var materials=sample.visual.sharedMaterials;
                var visualOffset=sample.transform.worldToLocalMatrix*sample.visual.transform.localToWorldMatrix;
                for(int i=0;i<rack.CapacityFor(type);i++)
                {
                    var pose=rack.SlotPoseFor(type,i);
                    var go=new GameObject(type.displayName){hideFlags=HideFlags.DontSave};go.transform.SetParent(root.transform,false);
                    var world=Matrix4x4.TRS(pose.position,pose.rotation,sample.originalScale)*visualOffset;
                    go.transform.SetPositionAndRotation(world.GetColumn(3),world.rotation);go.transform.localScale=world.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;count++;
                }
            }
            Debug.Log("RACK_PREVIEW items="+count+" (not saved; Tools > Lab Sorting > Designer > Preview: Clear)");
            return count;
        }
        [MenuItem("Tools/Lab Sorting/Designer/Preview: Clear")]
        public static void ClearPreview()
        {
            foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>().Where(g=>g.name==PreviewName&&g.scene.IsValid()).ToArray())Object.DestroyImmediate(go);
        }
        static string PathOf(Transform t){string path=t.name;while(t.parent){t=t.parent;path=t.name+"/"+path;}return path;}
    }
}
