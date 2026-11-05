using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class InventorySceneUpgrade
    {
        [MenuItem("Tools/Lab Sorting/Polish Inventory Controls")]
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);var hud=Object.FindFirstObjectByType<SortingHUD>();
            for(int i=0;i<hud.inventoryButtons.Length;i++)
            {
                var rect=(RectTransform)hud.inventoryButtons[i].transform;rect.sizeDelta=new Vector2(106,44);rect.anchoredPosition=new Vector2((i%5-2)*112,62+(i/5)*48);
                hud.inventoryLabels[i].fontSize=14;hud.inventoryLabels[i].resizeTextForBestFit=true;hud.inventoryLabels[i].resizeTextMinSize=11;hud.inventoryLabels[i].resizeTextMaxSize=14;
            }
            if(!hud.previousPage)
            {
                hud.previousPage=Button("InventoryPrevious",hud.transform.Find("SafeArea"),new Vector2(-310,85),"<");
                hud.nextPage=Button("InventoryNext",hud.transform.Find("SafeArea"),new Vector2(310,85),">");
                var label=new GameObject("InventoryPage",typeof(RectTransform),typeof(UnityEngine.UI.Text));label.transform.SetParent(hud.transform.Find("SafeArea"),false);hud.pageLabel=label.GetComponent<UnityEngine.UI.Text>();hud.pageLabel.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");hud.pageLabel.fontSize=12;hud.pageLabel.alignment=TextAnchor.MiddleCenter;hud.pageLabel.color=Color.white;hud.pageLabel.raycastTarget=false;var r=hud.pageLabel.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.anchoredPosition=new Vector2(310,45);r.sizeDelta=new Vector2(100,20);
            }
            hud.status.rectTransform.sizeDelta=new Vector2(760,32);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("INVENTORY_POLISH_WIRED");
        }
        static UnityEngine.UI.Button Button(string name,Transform parent,Vector2 position,string text)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(44,44);go.GetComponent<UnityEngine.UI.Image>().color=new Color(.1f,.3f,.32f);
            var label=new GameObject("Label",typeof(RectTransform),typeof(UnityEngine.UI.Text));label.transform.SetParent(go.transform,false);var t=label.GetComponent<UnityEngine.UI.Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=24;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;t.rectTransform.sizeDelta=rect.sizeDelta;
            return go.GetComponent<UnityEngine.UI.Button>();
        }
    }
}

