using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.Sorting;
using LabSorting.Prototype;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class StressSceneUpgrade
    {
        public const string ScenePath="Assets/_LabSorting/Prototype/Scenes/SortingStressTest.unity";
        [MenuItem("Tools/Lab Sorting/Create or Refresh Stress Scene")]
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);var session=Object.FindFirstObjectByType<SortingSession>();
            var hud=Object.FindFirstObjectByType<SortingHUD>();if(!hud.GetComponent<LabResponsiveLayout>())hud.gameObject.AddComponent<LabResponsiveLayout>();
            var batch=session.GetComponent<ItemBatchRenderer>();if(!batch)batch=session.gameObject.AddComponent<ItemBatchRenderer>();batch.session=session;batch.targetCamera=session.player.view;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            const string folder="Assets/_LabSorting/Prototype/StressAssets";Directory.CreateDirectory(folder);
            string texturePath=folder+"/ItemAtlas.png";
            if(!File.Exists(texturePath))
            {
                var texture=new Texture2D(1024,256,TextureFormat.RGB24,false);var colors=new Color[1024*256];for(int y=0;y<256;y++)for(int x=0;x<1024;x++)colors[y*1024+x]=session.items[Mathf.Min(4,x*5/1024)].definition.color;texture.SetPixels(colors);texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(texturePath);var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            string materialPath=folder+"/ItemAtlas.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.enableInstancing=true;AssetDatabase.CreateAsset(material,materialPath);}
            var meshes=new Mesh[5];for(int i=0;i<5;i++)
            {
                string path=folder+"/AtlasShape"+i+".asset";meshes[i]=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(meshes[i])continue;
                var mesh=Object.Instantiate(session.items[i].visual.GetComponent<MeshFilter>().sharedMesh);mesh.name="AtlasShape"+i;var uv=mesh.uv;for(int j=0;j<uv.Length;j++)uv[j]=new Vector2(i*.2f+.015f+uv[j].x*.17f,.08f+uv[j].y*.84f);mesh.uv=uv;AssetDatabase.CreateAsset(mesh,path);meshes[i]=mesh;
            }
            EditorSceneManager.SaveScene(scene,ScenePath);session.persistence=false;session.saveName="sorting-stress-disposable.json";
            session.GetComponent<ItemRecovery>().automatic=false;Object.FindFirstObjectByType<SettingsTutorialPanel>().autoTutorial=false;Object.FindFirstObjectByType<LabCompletionPanel>().autoShow=false;
            var catalog=session.GetComponent<StressCatalog>();if(!catalog)catalog=session.gameObject.AddComponent<StressCatalog>();catalog.session=session;catalog.templates=session.items.Take(5).ToArray();catalog.atlasMeshes=meshes;catalog.atlasMaterial=material;catalog.itemCount=3500;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="StressFloor";floor.transform.position=new Vector3(0,-.2f,18);floor.transform.localScale=new Vector3(65,.3f,45);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);Debug.Log("STRESS_SCENE_WIRED");
        }
    }
}

