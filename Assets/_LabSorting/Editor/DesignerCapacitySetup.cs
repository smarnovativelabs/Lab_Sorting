using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LabSorting.Sorting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using Random=System.Random;

namespace LabSorting.Editor
{
    // Size-based rack points, Batch 02 import and loose item piles for the designer lab.
    // Works on the scene open in the Editor. Safe to run again: racks are re-measured and loose items rebuilt.
    public static class DesignerCapacitySetup
    {
        public const string ScenePath="Assets/Scenes/Lab_Sorting.unity";
        public const string SaveName="lab-sorting-designer-v2.json";
        public const string Generated="Assets/_LabSorting/Designer";
        const string Batch02Folder="Assets/_LabSorting/Art/Items/DesignerBatch02";
        const string Batch02Model=Batch02Folder+"/Medical Props.fbx";
        const string ReportPath="Logs/DesignerCapacityReport.json";
        const string BackupPath="Logs/Lab_Sorting.beforeRackPoints.unity";
        // Boards above eye height stay empty: the rack label sits in front of them and players cannot see onto them.
        public const float MaxShelfHeight=1.6f;
        const float MaxItemSize=.42f;
        const float RackClearance=.45f, PileSpacing=2.6f, PileRadius=1.1f, CellSize=.25f;
        const int Seed=20261110, Mask=~(1<<2);

        [Serializable] public class TypeRow { public string id,name,sizeCm; public int perRack,spawned; }
        [Serializable] public class Report
        {
            public string scene; public float maxShelfHeight=MaxShelfHeight;
            public int itemTypes,racks,shelvesPerRack,items,piles,floorCells,unplaced,validationErrors,validationWarnings;
            public List<TypeRow> types=new List<TypeRow>();
            public List<string> imported=new List<string>(),warnings=new List<string>();
        }

        [MenuItem("Tools/Lab Sorting/Designer/Rebuild Rack Points and Item Piles")]
        public static void RunMenu()
        {
            try
            {
                var r=RunAll();
                EditorUtility.DisplayDialog("Lab Sorting",$"{r.itemTypes} item types, {r.racks} racks, {r.items:N0} items in {r.piles} piles.\n\nDetails: {ReportPath}","OK");
            }
            catch(Exception e){Debug.LogException(e);EditorUtility.DisplayDialog("Lab Sorting",e.Message,"OK");}
        }

        public static Report RunAll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath) throw new InvalidOperationException("Open "+ScenePath+" first.");
            var session=Object.FindObjectsByType<SortingSession>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(s=>s.gameObject.scene==scene);
            Directory.CreateDirectory("Logs");
            if(!File.Exists(BackupPath)) File.Copy(ScenePath,BackupPath);
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Lab Sorting rack points and piles");
            var report=new Report{scene=scene.path};
            try
            {
                ImportBatch02(report); AssetDatabase.SaveAssets();
                var prefabs=DesignerPrefabs(); MeasureItems(prefabs); AssetDatabase.SaveAssets();
                var racks=SetUpRacks(session,report);
                SpawnPiles(session,racks,prefabs,report);
            }
            finally{EditorUtility.ClearProgressBar();Undo.CollapseUndoOperations(group);}
            SaveScene(session,report);
            Debug.Log($"DESIGNER_RACK_POINTS_OK types={report.itemTypes} racks={report.racks} items={report.items} piles={report.piles}");
            return report;
        }

        // ---------- Batch 02 import ----------
        public static void ImportBatch02(Report report)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Batch02Model);
            if(!model) throw new FileNotFoundException("Batch 02 model not found",Batch02Model);
            foreach(var folder in new[]{"Meshes","Materials","Definitions","Prefabs"}) Directory.CreateDirectory(Generated+"/"+folder);
            var textures=new Dictionary<string,Texture2D>();
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Batch02Folder}))
            {var path=AssetDatabase.GUIDToAssetPath(guid);textures[Key(Path.GetFileNameWithoutExtension(path))]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
            var materials=new Dictionary<Material,Material>();
            foreach(var source in model.GetComponentsInChildren<MeshRenderer>(true).OrderBy(r=>r.name.Trim(),StringComparer.OrdinalIgnoreCase))
            {
                var filter=source.GetComponent<MeshFilter>();
                if(!filter||!filter.sharedMesh){report.warnings.Add("Skipped "+source.name+": no mesh");continue;}
                var shared=source.sharedMaterials.Select(m=>SharedMaterial(m,textures,materials)).ToArray();
                MakeItem(source,filter.sharedMesh,shared,report);
            }
        }
        static Material SharedMaterial(Material source,Dictionary<string,Texture2D> textures,Dictionary<Material,Material> cache)
        {
            if(!source) throw new InvalidOperationException("Batch 02 model has an empty material slot.");
            if(cache.TryGetValue(source,out var done)) return done;
            if(!textures.TryGetValue(Key(source.name),out var texture)) texture=source.mainTexture as Texture2D;
            if(!texture) throw new FileNotFoundException("No atlas texture matches material "+source.name);
            string path=Generated+"/Materials/Batch02_"+Slug(texture.name)+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material)
            {
                // Same setup as the Batch 01 atlas materials: shared, instanced, no shadows.
                material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=Path.GetFileNameWithoutExtension(path)};
                material.SetTexture("_BaseMap",texture);material.SetFloat("_Smoothness",.15f);material.SetFloat("_ReceiveShadows",0);
                material.EnableKeyword("_RECEIVE_SHADOWS_OFF");material.enableInstancing=true;
                AssetDatabase.CreateAsset(material,path);
            }
            cache[source]=material;return material;
        }
        static void MakeItem(MeshRenderer source,Mesh original,Material[] materials,Report report)
        {
            string id=Slug(source.name),path=Generated+"/Prefabs/"+id+".prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            // Bake the model transform, scale big models down to MaxItemSize and put the pivot at the bottom centre.
            var mesh=Object.Instantiate(original);mesh.name=id;
            var vertices=mesh.vertices;var matrix=source.transform.localToWorldMatrix;
            var bounds=new Bounds(matrix.MultiplyPoint3x4(vertices[0]),Vector3.zero);
            for(int i=0;i<vertices.Length;i++){vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);bounds.Encapsulate(vertices[i]);}
            float largest=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            if(largest<=0) throw new InvalidOperationException(source.name+" has an empty mesh.");
            float scale=Mathf.Min(1,MaxItemSize/largest);
            var pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-pivot)*scale;
            mesh.vertices=vertices;
            var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;
            for(int i=0;i<normals.Length;i++)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;
            mesh.normals=normals;mesh.RecalculateBounds();if(mesh.uv!=null&&mesh.uv.Length>0)mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh,Generated+"/Meshes/"+id+".asset");
            string definitionPath=Generated+"/Definitions/"+id+".asset";
            var definition=AssetDatabase.LoadAssetAtPath<ItemDefinition>(definitionPath);
            if(!definition)
            {
                definition=ScriptableObject.CreateInstance<ItemDefinition>();definition.id="lab-"+id;definition.displayName=DisplayName(source.name);
                AssetDatabase.CreateAsset(definition,definitionPath);
            }
            var go=new GameObject(id);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var box=go.AddComponent<BoxCollider>();box.center=mesh.bounds.center;box.size=mesh.bounds.size;
            var item=go.AddComponent<SortItem>();item.definition=definition;item.originalScale=Vector3.one;item.hitbox=box;item.visual=renderer;
            item.outline=go.AddComponent<Outline>();item.outline.enabled=false;
            PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);
            float size=largest*scale;
            report.imported.Add(definition.displayName+" ("+id+") "+(size*100).ToString("0")+" cm"+(scale<1?", scaled x"+scale.ToString("0.00"):""));
            if(size<.03f) report.warnings.Add(definition.displayName+" is only "+(size*100).ToString("0.0")+" cm; check the model units.");
            if(materials.Length>1) report.warnings.Add(definition.displayName+" uses "+materials.Length+" materials; it renders unbatched.");
        }

        // ---------- Item sizes ----------
        public static List<SortItem> DesignerPrefabs()=>AssetDatabase.FindAssets("t:Prefab",new[]{Generated+"/Prefabs"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(p=>p)
            .Select(p=>p.GetComponent<SortItem>()).Where(i=>i&&i.definition).OrderBy(i=>i.definition.id,StringComparer.Ordinal).ToList();
        public static void MeasureItems(List<SortItem> prefabs)
        {
            foreach(var item in prefabs)
            {
                var filter=item.visual?item.visual.GetComponent<MeshFilter>():null;
                if(!filter||!filter.sharedMesh) throw new InvalidOperationException(item.name+" has no visual mesh to measure.");
                // Bounds in the item's root space at its normal (rack) scale.
                var toRoot=Matrix4x4.Scale(item.originalScale)*item.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var b=filter.sharedMesh.bounds;var e=b.extents;var measured=new Bounds(toRoot.MultiplyPoint3x4(b.center),Vector3.zero);
                for(int i=0;i<8;i++)measured.Encapsulate(toRoot.MultiplyPoint3x4(b.center+new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z)));
                var definition=item.definition;
                if(definition.size==measured.size&&definition.center==measured.center) continue;
                Undo.RecordObject(definition,"Measure item");definition.size=measured.size;definition.center=measured.center;EditorUtility.SetDirty(definition);
            }
        }

        // ---------- Racks ----------
        public static List<SortRack> SetUpRacks(SortingSession session,Report report)
        {
            var scene=session.gameObject.scene;
            var racks=Object.FindObjectsByType<SortRack>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.gameObject.scene==scene)
                .OrderBy(r=>Number(r.name)).ThenBy(r=>r.name,StringComparer.Ordinal).ToList();
            var ids=new HashSet<string>();
            foreach(var rack in racks)
            {
                var shelves=MeasureShelves(rack);
                if(shelves.Length==0) throw new InvalidOperationException("No shelf boards found under "+rack.name+".");
                Undo.RecordObject(rack,"Size-based rack points");
                rack.shelves=shelves;rack.slots=Array.Empty<Transform>();
                foreach(var child in rack.transform.Cast<Transform>().Where(c=>Regex.IsMatch(c.name,@"^Slot \d+$")).ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
                if(string.IsNullOrWhiteSpace(rack.id)||!ids.Add(rack.id))
                {string old=rack.id;rack.id="designer-rack-n"+Number(rack.name);ids.Add(rack.id);report.warnings.Add(rack.name+": id '"+old+"' was empty or repeated, now "+rack.id);}
                rack.Initialize();EditorUtility.SetDirty(rack);
            }
            Undo.RecordObject(session,"Register racks");session.racks=racks.ToArray();EditorUtility.SetDirty(session);
            report.racks=racks.Count;report.shelvesPerRack=racks.Count>0?racks.Min(r=>r.shelves.Length):0;
            return racks;
        }
        sealed class Face { public float y,area,minX,maxX,minZ,maxZ; }
        // Reads the rack mesh: every large upward board below MaxShelfHeight becomes a shelf; the board above sets its height limit.
        public static RackShelf[] MeasureShelves(SortRack rack)
        {
            var up=new List<Face>();var down=new List<Face>();float top=0;
            var toRack=rack.transform.worldToLocalMatrix;
            foreach(var filter in rack.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh||filter.GetComponentInParent<SortItem>()||(rack.indicator&&filter.gameObject==rack.indicator.gameObject)) continue;
                var mesh=filter.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;var m=toRack*filter.transform.localToWorldMatrix;
                for(int i=0;i<t.Length;i+=3)
                {
                    var a=m.MultiplyPoint3x4(v[t[i]]);var b=m.MultiplyPoint3x4(v[t[i+1]]);var c=m.MultiplyPoint3x4(v[t[i+2]]);
                    top=Mathf.Max(top,Mathf.Max(a.y,Mathf.Max(b.y,c.y)));
                    var n=Vector3.Cross(b-a,c-a);float area=n.magnitude*.5f;if(area<1e-6f)continue;n/=area*2;
                    if(Mathf.Abs(n.y)<.98f)continue;
                    (n.y>0?up:down).Add(new Face{y=(a.y+b.y+c.y)/3,area=area,minX=Mathf.Min(a.x,Mathf.Min(b.x,c.x)),maxX=Mathf.Max(a.x,Mathf.Max(b.x,c.x)),minZ=Mathf.Min(a.z,Mathf.Min(b.z,c.z)),maxZ=Mathf.Max(a.z,Mathf.Max(b.z,c.z))});
                }
            }
            var boards=Merge(up,true).Where(f=>f.area>.25f&&f.y>.05f&&f.y<=MaxShelfHeight).OrderBy(f=>f.y).ToList();
            var undersides=Merge(down,false).Where(f=>f.area>.25f).Select(f=>f.y).ToList();
            var shelves=new List<RackShelf>();
            foreach(var board in boards)
            {
                float ceiling=undersides.Where(y=>y>board.y+.001f).DefaultIfEmpty(Mathf.Min(top,board.y+.5f)).Min();
                shelves.Add(new RackShelf{height=board.y,clearance=ceiling-board.y-.005f,x=new Vector2(board.minX+.005f,board.maxX-.005f),z=new Vector2(board.minZ+.005f,board.maxZ-.005f)});
            }
            return shelves.ToArray();
        }
        static List<Face> Merge(List<Face> faces,bool upward)
        {
            var merged=new List<Face>();
            foreach(var f in faces.OrderBy(f=>f.y))
            {
                var last=merged.Count>0?merged[merged.Count-1]:null;
                if(last!=null&&f.y-last.y<.003f)
                {last.y=upward?Mathf.Max(last.y,f.y):last.y;last.area+=f.area;last.minX=Mathf.Min(last.minX,f.minX);last.maxX=Mathf.Max(last.maxX,f.maxX);last.minZ=Mathf.Min(last.minZ,f.minZ);last.maxZ=Mathf.Max(last.maxZ,f.maxZ);}
                else merged.Add(new Face{y=f.y,area=f.area,minX=f.minX,maxX=f.maxX,minZ=f.minZ,maxZ=f.maxZ});
            }
            return merged;
        }

        // ---------- Loose item piles ----------
        sealed class Cell { public Vector3 point; public int placed; }
        public static void SpawnPiles(SortingSession session,List<SortRack> racks,List<SortItem> prefabs,Report report)
        {
            var scene=session.gameObject.scene;
            if(prefabs.Count>racks.Count) throw new InvalidOperationException($"{prefabs.Count} item types but only {racks.Count} racks; every type needs its own rack.");
            // One full rack per type, so every rack can be completed.
            var plan=new List<(SortItem prefab,int count)>();
            foreach(var prefab in prefabs)
            {
                int perRack=racks.Min(r=>r.CapacityFor(prefab.definition));
                if(perRack<=0) throw new InvalidOperationException(prefab.definition.displayName+" does not fit on the rack shelves.");
                plan.Add((prefab,perRack));
                var s=prefab.definition.size*100;
                report.types.Add(new TypeRow{id=prefab.definition.id,name=prefab.definition.displayName,sizeCm=$"{s.x:0}x{s.y:0}x{s.z:0}",perRack=perRack});
            }
            report.itemTypes=plan.Count;

            foreach(var old in Object.FindObjectsByType<SortItem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(i=>i.gameObject.scene==scene).ToArray())
                Undo.DestroyObjectImmediate(old.gameObject);
            var gameplay=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="LabGameplay");
            var parentRoot=gameplay?gameplay.transform:session.transform.root;
            var previous=parentRoot.Find("Loose Items");if(previous)Undo.DestroyObjectImmediate(previous.gameObject);
            var parent=new GameObject("Loose Items").transform;Undo.RegisterCreatedObjectUndo(parent.gameObject,"Loose items");parent.SetParent(parentRoot,false);
            Physics.SyncTransforms();

            var recovery=session.GetComponent<ItemRecovery>();
            if(!recovery||!recovery.Configured) throw new InvalidOperationException("ItemRecovery playable bounds are not configured.");
            var cells=FloorCells(session,racks,recovery);report.floorCells=cells.Count;
            var rng=new Random(Seed);
            // Pile centres: shuffled floor cells with open space around them, kept apart so walkways stay clear.
            var piles=new List<Cell>();
            foreach(var cell in cells.OrderBy(_=>rng.Next()))
            {
                if(Physics.CheckBox(cell.point+Vector3.up*.35f,new Vector3(.6f,.3f,.6f),Quaternion.identity,Mask,QueryTriggerInteraction.Ignore))continue;
                if(piles.All(p=>Mathf.Abs(p.point.y-cell.point.y)>1||Flat(p.point-cell.point)>=PileSpacing))piles.Add(cell);
            }
            if(piles.Count==0) throw new InvalidOperationException("No open floor found for item piles.");
            report.piles=piles.Count;

            var queue=new List<(SortItem prefab,int index)>();
            foreach(var (prefab,count) in plan)for(int i=0;i<count;i++)queue.Add((prefab,i));
            for(int i=queue.Count-1;i>0;i--){int j=rng.Next(i+1);(queue[i],queue[j])=(queue[j],queue[i]);}
            var pending=new LinkedList<(SortItem prefab,int index)>(queue);
            var placed=new List<SortItem>(queue.Count);
            int target=Mathf.CeilToInt(queue.Count/(float)piles.Count);
            for(int pass=0;pass<3&&pending.Count>0;pass++)
            {
                float radius=PileRadius*(1+pass*.3f);int limit=pass==0?target:int.MaxValue;
                for(int p=0;p<piles.Count&&pending.Count>0;p++)
                {
                    var pile=piles[p];int misses=0;
                    EditorUtility.DisplayProgressBar("Lab Sorting","Placing item piles",placed.Count/(float)queue.Count);
                    while(pending.Count>0&&pile.placed<limit&&misses<6)
                    {
                        var next=pending.First.Value;pending.RemoveFirst();
                        var item=TryDrop(next.prefab,next.index,pile,radius,rng,recovery,parent);
                        if(item){placed.Add(item);pile.placed++;misses=0;}
                        else{pending.AddLast(next);misses++;}
                    }
                }
            }
            report.unplaced=pending.Count;
            if(pending.Count>0) throw new InvalidOperationException($"{pending.Count} items did not fit on the floor; scene left unsaved. Undo (Ctrl+Z) to restore.");

            Undo.RecordObject(session,"Register items");
            session.items=placed.OrderBy(i=>i.definition.id,StringComparer.Ordinal).ThenBy(i=>Number(i.id)).ToArray();
            session.saveName=SaveName;session.persistence=true;
            session.RefreshSortingColliders();EditorUtility.SetDirty(session);
            report.items=placed.Count;
            foreach(var row in report.types)row.spawned=placed.Count(i=>i.definition.id==row.id);
        }
        static List<Cell> FloorCells(SortingSession session,List<SortRack> racks,ItemRecovery recovery)
        {
            var area=recovery.playableBounds.bounds;
            var levels=racks.Select(r=>r.transform.position.y).Append(session.player.transform.position.y).Distinct().ToArray();
            var zones=racks.Select(r=>{var b=WorldBounds(r);b.Expand(new Vector3(RackClearance*2,0,RackClearance*2));return b;}).ToArray();
            var spawn=session.player.transform.position;
            var hits=new RaycastHit[32];var cells=new List<Cell>();
            for(float x=area.min.x+CellSize;x<area.max.x-CellSize;x+=CellSize)
            for(float z=area.min.z+CellSize;z<area.max.z-CellSize;z+=CellSize)
            {
                int n=Physics.RaycastNonAlloc(new Vector3(x,area.max.y,z),Vector3.down,hits,area.size.y+1,Mask,QueryTriggerInteraction.Ignore);
                for(int i=0;i<n;i++)
                {
                    var h=hits[i];var p=h.point;
                    if(!IsFloor(h.collider,h.normal,session)||!levels.Any(l=>Mathf.Abs(p.y-l)<.08f))continue;
                    if(Physics.Raycast(p+Vector3.up*.05f,Vector3.up,2f,Mask,QueryTriggerInteraction.Ignore))continue;
                    if(zones.Any(b=>p.y>b.min.y-1&&p.y<b.max.y&&b.Contains(new Vector3(p.x,b.center.y,p.z))))continue;
                    if(Mathf.Abs(p.y-spawn.y)<1&&Flat(p-spawn)<1.5f)continue;
                    if(recovery.safePoints.Any(s=>s&&Mathf.Abs(p.y-s.position.y)<1&&Flat(p-s.position)<.8f))continue;
                    if(!recovery.Contains(p+Vector3.up*.3f))continue;
                    if(Physics.CheckBox(p+Vector3.up*.3f,new Vector3(.3f,.25f,.3f),Quaternion.identity,Mask,QueryTriggerInteraction.Ignore))continue;
                    cells.Add(new Cell{point=p});
                }
            }
            return cells;
        }
        static SortItem TryDrop(SortItem prefab,int index,Cell pile,float radius,Random rng,ItemRecovery recovery,Transform parent)
        {
            var type=prefab.definition;
            bool tall=type.size.y>1.6f*Mathf.Max(type.size.x,type.size.z);
            for(int attempt=0;attempt<30;attempt++)
            {
                double angle=rng.NextDouble()*Math.PI*2;float r=radius*Mathf.Sqrt((float)rng.NextDouble());
                var at=pile.point+new Vector3((float)Math.Cos(angle)*r,0,(float)Math.Sin(angle)*r);
                // Messy: random facing, and some tall items knocked over onto their side.
                var rotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
                if(tall&&rng.NextDouble()<.4)rotation*=Quaternion.Euler(0,0,90);
                var b=SortRack.RotatedBounds(type,rotation);
                var position=new Vector3(at.x-b.center.x,pile.point.y+.003f-b.min.y,at.z-b.center.z);
                var center=position+rotation*type.center;var half=type.size*.5f;
                // Pivot and whole box inside the play area (recovery would otherwise move the item at start).
                if(!recovery.Contains(position)||!recovery.Contains(position+b.min)||!recovery.Contains(position+b.max))continue;
                if(Physics.CheckBox(center,half-Vector3.one*.002f,rotation,Mask,QueryTriggerInteraction.Ignore))continue;
                if(!Supported(position+b.center,b.extents,pile.point.y))continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject,parent);
                Undo.RegisterCreatedObjectUndo(go,"Loose item");
                go.transform.SetPositionAndRotation(position,rotation);
                var item=go.GetComponent<SortItem>();item.id=type.id+"-"+index;item.location=ItemLocation.Loose;item.rack=null;item.slot=-1;
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                Physics.SyncTransforms();
                return item;
            }
            return null;
        }
        // Every bottom corner must rest on flat floor at the pile's height (no overhang off a catwalk edge).
        static bool Supported(Vector3 center,Vector3 extents,float floor)
        {
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var corner=new Vector3(center.x+x*Mathf.Max(0,extents.x-.01f),floor+.05f,center.z+z*Mathf.Max(0,extents.z-.01f));
                if(!Physics.Raycast(corner,Vector3.down,out var hit,.1f,Mask,QueryTriggerInteraction.Ignore))return false;
                if(hit.normal.y<.95f||Mathf.Abs(hit.point.y-floor)>.02f||hit.collider.GetComponentInParent<SortItem>()||hit.collider.GetComponentInParent<SortRack>())return false;
            }
            return true;
        }
        static bool IsFloor(Collider collider,Vector3 normal,SortingSession session)=>normal.y>=.95f&&!collider.GetComponentInParent<SortItem>()&&!collider.GetComponentInParent<SortRack>()&&!collider.attachedRigidbody&&!collider.transform.IsChildOf(session.player.transform);
        static Bounds WorldBounds(SortRack rack)
        {
            var b=new Bounds(rack.transform.position,Vector3.zero);
            foreach(var c in rack.GetComponentsInChildren<Collider>(true))b.Encapsulate(c.bounds);
            return b;
        }

        // ---------- Save ----------
        static void SaveScene(SortingSession session,Report report)
        {
            Physics.SyncTransforms();
            var validation=IntegrationValidator.Validate(session);IntegrationValidator.Export(validation);
            report.validationErrors=validation.errors;report.validationWarnings=validation.warnings;
            File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
            if(validation.errors>0)
                throw new InvalidOperationException($"Validation found {validation.errors} errors; scene left unsaved (Ctrl+Z restores it). First: "+string.Join("; ",validation.issues.Where(i=>i.severity=="Error").Take(5).Select(i=>i.code+" "+i.objectPath)));
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            if(!EditorSceneManager.SaveScene(session.gameObject.scene)) throw new IOException("Could not save "+ScenePath);
            AssetDatabase.SaveAssets();
        }

        static float Flat(Vector3 v)=>new Vector2(v.x,v.z).magnitude;
        static int Number(string name){var m=Regex.Match(name??"",@"(\d+)$");if(!m.Success)m=Regex.Match(name??"",@"\d+");return m.Success?int.Parse(m.Groups[m.Groups.Count>1?1:0].Value):int.MaxValue;}
        static string Key(string s)=>Regex.Replace(s.ToLowerInvariant(),"[^a-z0-9]","");
        static string Slug(string s)=>Regex.Replace(s.Trim().ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-');
        static string DisplayName(string raw)
        {
            var s=Regex.Replace(raw.Replace('_',' ').Trim(),@"\s+"," ");
            foreach(var (from,to) in new[]{("Battries","Batteries"),("Vilamin","Vitamin"),("Therometer","Thermometer"),("Mediccal","Medical"),("BLood","Blood")})
                s=Regex.Replace(s,from,to,RegexOptions.IgnoreCase);
            return string.Join(" ",s.Split(' ').Select(w=>w.Equals("iv",StringComparison.OrdinalIgnoreCase)?"IV":w.Length>0?char.ToUpperInvariant(w[0])+w.Substring(1):w));
        }
    }
}
