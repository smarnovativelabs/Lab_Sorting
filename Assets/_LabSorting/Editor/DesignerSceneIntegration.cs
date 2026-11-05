using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LabSorting.Player;
using LabSorting.Sorting;
using LabSorting.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LabSorting.Editor
{
    public static class DesignerSceneIntegration
    {
        const string ScenePath="Assets/Scenes/Lab_Sorting.unity";
        const string Root="Assets/_LabSorting/Art/Items/DesignerBatch01";
        const string Generated="Assets/_LabSorting/Designer";
        public static void Run(){Apply();DesignerStartupSetup.Apply();DesignerSceneInspection.Run();}
        static string Slug(string name)=>Regex.Replace(name.Trim().ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-');
        static T Save<T>(T value,string path) where T:Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old){Object.DestroyImmediate(value);return old;}
            AssetDatabase.CreateAsset(value,path);return value;
        }
        static Material Indicator(string name,Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.color=color;
            return Save(m,Generated+"/Materials/"+name+".mat");
        }
        static List<float> ShelfHeights(GameObject rack)
        {
            var levels=new Dictionary<int,float>();
            foreach(var filter in rack.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;
                var matrix=rack.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int i=0;i<t.Length;i+=3)
                {
                    var a=matrix.MultiplyPoint3x4(v[t[i]]);var b=matrix.MultiplyPoint3x4(v[t[i+1]]);var c=matrix.MultiplyPoint3x4(v[t[i+2]]);
                    var cross=Vector3.Cross(b-a,c-a);float height=(a.y+b.y+c.y)/3;
                    if(cross.y<=0||cross.normalized.y<.98f||height<0||height>1.6f)continue;
                    int key=Mathf.RoundToInt(height*100);levels.TryGetValue(key,out var area);levels[key]=area+cross.magnitude*.5f;
                }
            }
            var found=levels.Where(p=>p.Value>.25f).OrderBy(p=>p.Key).Select(p=>p.Key*.01f).ToList();
            var result=new List<float>();foreach(float y in found){if(result.Count>0&&y-result[result.Count-1]<.07f)result[result.Count-1]=y;else result.Add(y);}
            if(result.Count<2)throw new InvalidOperationException("Could not identify physical rack shelves: "+string.Join(",",found));
            return result;
        }
        static SortItem MakeItem(Renderer source,Dictionary<string,Material> materials)
        {
            string id=Slug(source.name),path=Generated+"/Prefabs/"+id+".prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing)return existing.GetComponent<SortItem>();
            var original=source.GetComponent<MeshFilter>().sharedMesh;
            if(original.subMeshCount!=1||source.sharedMaterials.Length!=1)throw new InvalidOperationException("Review multi-material item: "+source.name);
            string atlas=source.sharedMaterial.name.Replace(" ","_").ToLowerInvariant();
            if(!materials.TryGetValue(atlas,out var material))throw new InvalidOperationException("Missing atlas for "+source.name+": "+atlas);
            var mesh=Object.Instantiate(original);mesh.name=id;
            var vertices=mesh.vertices;var matrix=source.localToWorldMatrix;
            var bounds=new Bounds(matrix.MultiplyPoint3x4(vertices[0]),Vector3.zero);
            for(int i=0;i<vertices.Length;i++){vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);bounds.Encapsulate(vertices[i]);}
            float scale=Mathf.Min(1,.42f/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));
            var pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-pivot)*scale;
            mesh.vertices=vertices;var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;
            for(int i=0;i<normals.Length;i++)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;
            mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();
            mesh=Save(mesh,Generated+"/Meshes/"+id+".asset");
            var definition=ScriptableObject.CreateInstance<ItemDefinition>();definition.id="lab-"+id;
            definition.displayName=source.name.Trim().Replace("Battries","Batteries").Replace("Vilamin","Vitamin").Replace("Therometer","Thermometer").Replace('_',' ');
            definition=Save(definition,Generated+"/Definitions/"+id+".asset");
            var go=new GameObject(definition.displayName);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var box=go.AddComponent<BoxCollider>();box.center=mesh.bounds.center;box.size=mesh.bounds.size;
            var item=go.AddComponent<SortItem>();item.definition=definition;item.originalScale=Vector3.one;item.hitbox=box;item.visual=renderer;
            item.outline=go.AddComponent<Outline>();item.outline.enabled=false;
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);return prefab.GetComponent<SortItem>();
        }
        public static void Apply()
        {
            var destination=EditorSceneManager.OpenScene(ScenePath);
            if(destination.GetRootGameObjects().Any(g=>g.name=="LabGameplay"))throw new InvalidOperationException("Designer gameplay already wired; refusing duplicate setup.");
            if(!File.Exists("Logs/Lab_Sorting.beforeGameplay.unity"))File.Copy(ScenePath,"Logs/Lab_Sorting.beforeGameplay.unity");
            foreach(var name in new[]{"Meshes","Materials","Definitions","Prefabs"})Directory.CreateDirectory(Generated+"/"+name);
            AssetDatabase.Refresh();
            foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))camera.enabled=false;
            foreach(var listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include,FindObjectsSortMode.None))listener.enabled=false;
            var sourceScene=EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath,OpenSceneMode.Additive);
            var session=sourceScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SortingSession>(true)).Single();
            var keep=sourceScene.GetRootGameObjects().Where(g=>g.GetComponent<LabPlayerInput>()||g.GetComponent<SortingSession>()||g.GetComponent<Canvas>()||g.GetComponent<EventSystem>()).ToArray();
            foreach(var go in keep)SceneManager.MoveGameObjectToScene(go,destination);
            session.items=Array.Empty<SortItem>();session.racks=Array.Empty<SortRack>();
            EditorSceneManager.CloseScene(sourceScene,true);SceneManager.SetActiveScene(destination);
            var root=new GameObject("LabGameplay");foreach(var go in keep)go.transform.SetParent(root.transform,true);
            session.saveName="lab-sorting-designer-v1.json";session.persistence=true;
            // KCC's cached transform is initialized only in Play Mode.
            session.player.transform.SetPositionAndRotation(new Vector3(0,.03f,-8.1f),Quaternion.identity);
            session.player.look.SetYawPitch(new Vector2(0,10));session.player.view.enabled=true;
            var floor=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Single(r=>r.name=="05_Continuous_Epoxy_Floor");
            foreach(var c in floor.GetComponents<Collider>())c.enabled=false;
            var floorObject=new GameObject("AlignedFloorCollision");floorObject.transform.SetParent(root.transform);floorObject.transform.position=floor.bounds.center;
            floorObject.AddComponent<BoxCollider>().size=floor.bounds.size;
            foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(r.transform.parent&&r.transform.parent.name=="Colliders")r.enabled=false;
            var materials=new Dictionary<string,Material>();
            for(int i=1;i<=4;i++)
            {
                string texturePath=Root+"/Textures/Atlas_"+i+(i==3?".jpg":".png");
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);if(!texture)throw new FileNotFoundException(texturePath);
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name="Atlas_"+i;
                material.SetTexture("_BaseMap",texture);material.SetFloat("_Smoothness",.15f);material.SetFloat("_ReceiveShadows",0);material.EnableKeyword("_RECEIVE_SHADOWS_OFF");material.enableInstancing=true;
                materials.Add("atlas_"+i,Save(material,Generated+"/Materials/Atlas_"+i+".mat"));
            }
            var models=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Lab_Items.fbx").GetComponentsInChildren<Renderer>(true).OrderBy(r=>r.name,StringComparer.Ordinal).ToArray();
            var prefabs=models.Select(r=>MakeItem(r,materials)).ToArray();
            var rackAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LabSorting/Art/Fbx/Lab_Rack.fbx");
            var neutral=Indicator("RackNeutral",new Color(.1f,.23f,.28f));var valid=Indicator("RackValid",new Color(.1f,.8f,.35f));var invalid=Indicator("RackInvalid",new Color(.9f,.15f,.1f));
            var racks=new List<SortRack>();
            for(int i=0;i<prefabs.Length;i++)
            {
                var rackRoot=new GameObject("Rack "+(i+1));rackRoot.transform.SetParent(root.transform);rackRoot.transform.position=new Vector3((i%9-4)*2.05f,0,-6.1f+(i/9)*2.6f);
                var art=(GameObject)PrefabUtility.InstantiatePrefab(rackAsset);art.transform.SetParent(rackRoot.transform,false);art.SetActive(true);
                foreach(var filter in art.GetComponentsInChildren<MeshFilter>()){var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
                var rack=rackRoot.AddComponent<SortRack>();rack.id="designer-rack-"+i;rack.neutral=neutral;rack.valid=valid;rack.invalid=invalid;
                var slots=new List<Transform>();foreach(float height in ShelfHeights(rackRoot))for(int column=0;column<3;column++)
                {var slot=new GameObject("Slot "+slots.Count).transform;slot.SetParent(rackRoot.transform,false);slot.localPosition=new Vector3((column-1)*.49f,height+.015f,0);slots.Add(slot);}
                rack.slots=slots.ToArray();
                var indicator=GameObject.CreatePrimitive(PrimitiveType.Cube);indicator.name="PlacementIndicator";indicator.transform.SetParent(rackRoot.transform,false);indicator.transform.localPosition=new Vector3(0,1.8f,-.32f);indicator.transform.localScale=new Vector3(1.55f,.17f,.025f);rack.indicator=indicator.GetComponent<Renderer>();rack.indicator.sharedMaterial=neutral;
                var label=new GameObject("RackLabel").AddComponent<TextMesh>();label.transform.SetParent(rackRoot.transform,false);label.transform.localPosition=new Vector3(0,1.8f,-.34f);label.characterSize=.022f;label.fontSize=40;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=Color.white;rack.label=label;rack.Initialize();racks.Add(rack);
            }
            session.racks=racks.ToArray();var items=new List<SortItem>();
            for(int copy=0;copy<3;copy++)for(int type=0;type<prefabs.Length;type++)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[type].gameObject);go.transform.SetParent(root.transform,true);
                go.transform.position=new Vector3((type-12.5f)*.59f,.018f,-7.25f+copy*2.6f);
                var item=go.GetComponent<SortItem>();item.id=item.definition.id+"-"+copy;items.Add(item);
            }
            session.items=items.ToArray();session.RefreshSortingColliders();
            var recovery=session.GetComponent<ItemRecovery>();
            var volume=new GameObject("PlayableBounds");volume.transform.SetParent(root.transform);volume.transform.position=new Vector3(0,3,0);
            recovery.playableBounds=volume.AddComponent<BoxCollider>();recovery.playableBounds.isTrigger=true;recovery.playableBounds.size=new Vector3(23.5f,9,17.5f);recovery.minimumY=-.05f;
            var points=new List<Transform>();for(int i=0;i<6;i++){var point=new GameObject("RecoveryPoint "+i).transform;point.SetParent(root.transform);point.position=new Vector3(i%2==0?-9.7f:9.7f,.5f,-7.2f+(i/2)*2.6f);points.Add(point);}recovery.safePoints=points.ToArray();
            foreach(var old in root.GetComponentsInChildren<Crystal.SafeArea>(true)){var go=old.gameObject;Object.DestroyImmediate(old);if(!go.GetComponent<SafeAreaPanel>())go.AddComponent<SafeAreaPanel>();}
            Physics.SyncTransforms();var report=IntegrationValidator.Validate(session);IntegrationValidator.Export(report);
            if(report.errors>0)throw new InvalidOperationException("Designer integration errors: "+string.Join(";",report.issues.Where(i=>i.severity=="Error").Select(i=>i.code+" "+i.message)));
            EditorSceneManager.MarkSceneDirty(destination);if(!EditorSceneManager.SaveScene(destination))throw new IOException("Could not save designer scene");
            AssetDatabase.SaveAssets();Debug.Log("DESIGNER_GAMEPLAY_WIRED items="+items.Count+" categories="+prefabs.Length+" racks="+racks.Count+" slots="+racks.Sum(r=>r.slots.Length));
        }
    }
}
