using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.Services;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class GameplayPolishUpgrade
    {
        public static void Apply()
        {
            var config=AssetDatabase.LoadAssetAtPath<LabServiceSettings>("Assets/_LabSorting/Settings/LabServiceSettings.asset");
            const string prefabPath="Assets/_LabSorting/Prefabs/LabServices.prefab";
            var root=new GameObject("Lab Services");var bootstrap=root.AddComponent<LabServiceRoot>();
            bootstrap.commerce=root.AddComponent<SupplyCommerce>();bootstrap.commerce.settings=config;
            bootstrap.telemetry=root.AddComponent<LabTelemetry>();bootstrap.telemetry.settings=config;
            bootstrap.internet=root.AddComponent<InternetConnectivity>();root.AddComponent<LabConsent>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);Object.DestroyImmediate(root);
            foreach(var path in new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/Lab_Sorting.unity","Assets/_LabSorting/Prototype/Scenes/SortingTest.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path);
                foreach(var old in Object.FindObjectsByType<LabServiceRoot>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(old.gameObject);
                foreach(var old in Object.FindObjectsByType<SupplyCommerce>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(old);
                foreach(var old in Object.FindObjectsByType<LabTelemetry>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(old);
                foreach(var old in Object.FindObjectsByType<LabConsent>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(old);
                foreach(var old in Object.FindObjectsByType<InternetConnectivity>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(old);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var commerce=instance.GetComponent<SupplyCommerce>();
                foreach(var panel in Object.FindObjectsByType<SupplyPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None))panel.commerce=commerce;
                foreach(var panel in Object.FindObjectsByType<ServicesPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None))panel.commerce=commerce;
                var hud=Object.FindFirstObjectByType<EconomyHUD>();if(hud){hud.marker.rectTransform.sizeDelta=new Vector2(720,64);hud.marker.resizeTextForBestFit=true;hud.marker.resizeTextMinSize=14;hud.marker.resizeTextMaxSize=19;
                    if(!hud.markerBackdrop){var card=new GameObject("SearchDirectionBackdrop",typeof(RectTransform),typeof(UnityEngine.UI.Image));card.transform.SetParent(hud.marker.transform.parent,false);card.transform.SetSiblingIndex(hud.marker.transform.GetSiblingIndex());hud.markerBackdrop=card.GetComponent<UnityEngine.UI.Image>();hud.markerBackdrop.color=new Color(.025f,.055f,.08f,.94f);hud.markerBackdrop.raycastTarget=false;card.SetActive(false);}
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");
        }
    }
}
