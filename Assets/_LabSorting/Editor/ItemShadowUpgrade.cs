using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class ItemShadowUpgrade
    {
        const string Folder="Assets/_LabSorting/Materials/ItemsNoShadows";
        static Material WithoutShadows(Material original)
        {
            if(!original||!original.HasProperty("_ReceiveShadows"))return original;
            if(original.GetFloat("_ReceiveShadows")==0&&original.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF"))return original;
            string source=AssetDatabase.GetAssetPath(original);if(string.IsNullOrEmpty(source))throw new System.Exception("Item material must be an asset: "+original.name);
            string path=Folder+"/"+AssetDatabase.AssetPathToGUID(source)+".mat";var variant=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!variant){variant=new Material(original);variant.name=original.name+" Items No Shadows";variant.SetFloat("_ReceiveShadows",0);variant.EnableKeyword("_RECEIVE_SHADOWS_OFF");AssetDatabase.CreateAsset(variant,path);}
            return variant;
        }
        static void ApplyItem(SortItem item)
        {
            item.DisableShadows();
            foreach(var renderer in item.GetComponentsInChildren<Renderer>(true))
            {var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=WithoutShadows(materials[i]);renderer.sharedMaterials=materials;EditorUtility.SetDirty(renderer);}
        }
        [MenuItem("Tools/Lab Sorting/Disable All Item Shadows")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_LabSorting"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset.GetComponentsInChildren<SortItem>(true).Length==0)continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try{foreach(var item in root.GetComponentsInChildren<SortItem>(true))ApplyItem(item);PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            foreach(var path in new[]{PrototypeBuilder.ScenePath,StressSceneUpgrade.ScenePath})
            {var scene=EditorSceneManager.OpenScene(path);foreach(var item in Object.FindObjectsByType<SortItem>(FindObjectsInactive.Include,FindObjectsSortMode.None))ApplyItem(item);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);Debug.Log("ITEM_SHADOWS_DISABLED");
        }
        public static void Validate()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_LabSorting"}))ValidateItems(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponentsInChildren<SortItem>(true));
            foreach(var path in new[]{PrototypeBuilder.ScenePath,StressSceneUpgrade.ScenePath}){EditorSceneManager.OpenScene(path);ValidateItems(Object.FindObjectsByType<SortItem>(FindObjectsInactive.Include,FindObjectsSortMode.None));}
            Debug.Log("ITEM_SHADOW_POLICY_OK");
        }
        static void ValidateItems(SortItem[] items)
        {
            foreach(var item in items)foreach(var renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.Off||renderer.receiveShadows)throw new System.Exception("Item shadows enabled: "+item.name);
                foreach(var material in renderer.sharedMaterials)if(material&&material.HasProperty("_ReceiveShadows")&&(material.GetFloat("_ReceiveShadows")!=0||!material.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF")))throw new System.Exception("Item material receives shadows: "+material.name);
            }
        }
    }
}
