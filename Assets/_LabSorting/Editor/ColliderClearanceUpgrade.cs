using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    // Colliders.fbx was refitted to the visible mezzanine edge, railing and pillars (2026-11-19). Items the
    // continuous spread had placed on the old, oversized colliders now hang past the floor edge or sit inside a
    // moved collider. This moves only those items to the nearest flat, clear spot on the same floor level and
    // records their previous poses in SortingSession.layoutFixes, so untouched items in existing saves follow.
    public static class ColliderClearanceUpgrade
    {
        const float Penetration=.006f,Lift=.004f,MaxRadius=5f,Step=.05f;
        static readonly RaycastHit[] hits=new RaycastHit[32];
        static readonly Collider[] overlaps=new Collider[64];
        static bool Floor(RaycastHit hit)=>hit.normal.y>.95f&&!hit.collider.GetComponentInParent<SortItem>()&&!hit.collider.GetComponentInParent<SortRack>();
        static bool Supported(Vector3 point){int n=Physics.RaycastNonAlloc(point,Vector3.down,hits,.09f,~(1<<2),QueryTriggerInteraction.Ignore);for(int i=0;i<n;i++)if(Floor(hits[i]))return true;return false;}
        // Same rules as ReferenceCorrectionChecks (bottom supported by floor) plus full footprint and no level-collider penetration.
        public static string Problem(SortItem item)
        {
            if(!item||!item.hitbox||item.location!=ItemLocation.Loose||(item.definition&&item.definition.isRat))return null;
            var b=item.hitbox.bounds;
            if(!Supported(new Vector3(b.center.x,b.min.y+.02f,b.center.z)))return "floating";
            int corners=0;foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})if(Supported(new Vector3(b.center.x+x*b.extents.x*.9f,b.min.y+.02f,b.center.z+z*b.extents.z*.9f)))corners++;
            if(corners<4)return "hanging "+corners+"/4";
            int count=Physics.OverlapBoxNonAlloc(b.center,b.extents,overlaps,Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var other=overlaps[i];if(other==item.hitbox||other.GetComponentInParent<SortItem>()||other.attachedRigidbody)continue;
                if(Physics.ComputePenetration(item.hitbox,item.hitbox.transform.position,item.hitbox.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)&&depth>Penetration)return "inside "+other.name+" "+depth.ToString("F3");
            }
            return null;
        }
        public static List<SortItem> Invalid(SortingSession s){Physics.SyncTransforms();return s.items.Where(i=>Problem(i)!=null).ToList();}
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Lab_Sorting.unity"||scene.isDirty)throw new Exception("Open saved designer scene");
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var recovery=s.GetComponent<ItemRecovery>();
            if(!recovery||!recovery.playableBounds)throw new Exception("ItemRecovery bounds missing");
            var bad=Invalid(s);var report=new List<string>();
            foreach(var item in bad)report.Add("BEFORE "+item.id+" "+Problem(item)+" at "+item.transform.position.ToString("F3"));
            if(bad.Count==0){Debug.Log("COLLIDER_CLEARANCE_OK 0");return;}
            System.IO.File.Copy(scene.path,"Logs/LabBeforeColliderClearance.unity",true);
            var before=bad.Select(i=>new Pose(i.transform.position,i.transform.rotation)).ToArray();
            var states=s.items.Select(i=>i.hitbox.enabled).ToArray();var fixes=new List<SortingSession.LayoutFix>();bool success=false;
            try
            {
                // The items to move are taken out of the world first, so they never block each other's search.
                var sizes=bad.ToDictionary(i=>i,i=>i.hitbox.bounds);
                foreach(var item in bad)item.hitbox.enabled=false;Physics.SyncTransforms();
                foreach(var item in bad.OrderByDescending(i=>sizes[i].size.x*sizes[i].size.z))
                {
                    var b=sizes[item];float floorY=b.min.y-Lift;
                    // Same floor first; an item that hung past the mezzanine edge may otherwise land on the floor below it.
                    bool sameFloor=FindSpot(item,b,floorY,true,recovery,out var center);
                    if(!sameFloor&&!FindSpot(item,b,floorY,false,recovery,out center))throw new Exception("No clear floor spot for "+item.id+" near "+b.center);
                    var delta=center-b.center;var position=item.transform.position+delta;
                    Undo.RecordObject(item.transform,"Collider clearance");item.transform.position=position;PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                    item.hitbox.enabled=true;Physics.SyncTransforms();
                    fixes.Add(new SortingSession.LayoutFix{id=item.id,previous=before[bad.IndexOf(item)].position});
                    report.Add("MOVED "+item.id+" by "+delta.ToString("F3")+" ("+delta.magnitude.ToString("F2")+" m)"+(sameFloor?"":" DOWN TO FLOOR BELOW")+" to "+position.ToString("F3"));
                }
                for(int i=0;i<s.items.Length;i++)s.items[i].hitbox.enabled=states[i];
                var left=Invalid(s);
                if(left.Count>0)throw new Exception("Still invalid after move: "+string.Join(", ",left.Select(i=>i.id+" "+Problem(i))));
                success=true;
            }
            finally
            {
                for(int i=0;i<s.items.Length;i++)s.items[i].hitbox.enabled=states[i];
                if(!success)for(int i=0;i<bad.Count;i++){bad[i].transform.SetPositionAndRotation(before[i].position,before[i].rotation);PrefabUtility.RecordPrefabInstancePropertyModifications(bad[i].transform);}
                Physics.SyncTransforms();
            }
            Undo.RecordObject(s,"Collider clearance fixes");
            s.layoutFixes=(s.layoutFixes??new SortingSession.LayoutFix[0]).Concat(fixes).ToArray();
            EditorUtility.SetDirty(s);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            System.IO.File.WriteAllText("Logs/ColliderClearanceReport.txt","Moved="+fixes.Count+"\nLayoutFixes="+s.layoutFixes.Length+"\n"+string.Join("\n",report));
            Debug.Log("COLLIDER_CLEARANCE_OK "+fixes.Count);
        }
        // Nearest point (expanding rings) where the item's footprint is flat floor, clear of level colliders and of
        // other items' actual (rotated) boxes, and inside the playable bounds. sameFloor keeps the item's own level;
        // otherwise the first floor found below it is used. The item's own hitbox is disabled while this runs.
        static bool FindSpot(SortItem item,Bounds b,float floorY,bool sameFloor,ItemRecovery recovery,out Vector3 center)
        {
            center=default;var e=b.extents;var box=item.hitbox as BoxCollider;var t=item.hitbox.transform;
            var boxCenter=box?t.TransformPoint(box.center):b.center;var rotation=box?t.rotation:Quaternion.identity;
            var half=box?Vector3.Scale(box.size,t.lossyScale)*.5f:e;half=new Vector3(Mathf.Abs(half.x),Mathf.Abs(half.y),Mathf.Abs(half.z))+Vector3.one*.002f;
            float rayStart=floorY+.5f,rayLength=sameFloor?1f:floorY+1f;
            for(float radius=0;radius<=MaxRadius;radius+=Step)
            {
                int steps=radius<=0?1:Mathf.CeilToInt(2*Mathf.PI*radius/.06f);
                for(int k=0;k<steps;k++)
                {
                    float angle=k*2*Mathf.PI/steps;var x=b.center.x+Mathf.Cos(angle)*radius;var z=b.center.z+Mathf.Sin(angle)*radius;
                    if(!Physics.Raycast(new Vector3(x,rayStart,z),Vector3.down,out var floor,rayLength,~(1<<2),QueryTriggerInteraction.Ignore)||!Floor(floor))continue;
                    if(sameFloor?Mathf.Abs(floor.point.y-floorY)>.05f:floor.point.y>floorY+.05f)continue;
                    bool flat=true;
                    foreach(float cx in new[]{-1f,1f})foreach(float cz in new[]{-1f,1f})
                        if(!Physics.Raycast(new Vector3(x+cx*e.x*.9f,floor.point.y+.06f,z+cz*e.z*.9f),Vector3.down,out var support,.1f,~(1<<2),QueryTriggerInteraction.Ignore)||!Floor(support)||Mathf.Abs(support.point.y-floor.point.y)>.015f)flat=false;
                    if(!flat)continue;
                    var candidate=new Vector3(x,floor.point.y+e.y+Lift,z);var world=new Bounds(candidate,b.size+Vector3.one*.003f);
                    if(!recovery.Contains(world.min)||!recovery.Contains(world.max))continue;
                    // One query covers level colliders and every placed item, using the real rotated box.
                    if(Physics.CheckBox(boxCenter+(candidate-b.center),half,rotation,~(1<<2),QueryTriggerInteraction.Ignore))continue;
                    center=candidate;return true;
                }
            }
            return false;
        }
    }
}
