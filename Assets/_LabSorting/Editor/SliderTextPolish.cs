using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class SliderTextPolish
    {
        public static void Apply()
        {
            foreach(var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                foreach(var text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    if(text.transform.parent!=button.transform||text.name!="Label")continue;
                    // Product descriptions and inventory rows deliberately retain their own layout.
                    if(button.GetComponentsInChildren<UnityEngine.UI.Image>().Length>1)continue;
                    var r=text.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=Vector2.one*.5f;r.offsetMin=new Vector2(12,5);r.offsetMax=new Vector2(-12,-5);
                    text.alignment=TextAnchor.MiddleCenter;text.alignByGeometry=true;
                }
            }
            foreach(var text in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(text.alignment==TextAnchor.MiddleCenter&&!text.text.Contains("\n"))text.alignByGeometry=true;
            var h=Object.FindFirstObjectByType<SortingHUD>();
            if(h)
            {
                var supply=h.GetComponent<SupplyPanel>();var card=(RectTransform)supply.title.transform.parent;
                card.Find("DesignerTitle_Title").gameObject.SetActive(false);
                var title=supply.title.rectTransform;title.anchorMin=title.anchorMax=Vector2.one*.5f;title.pivot=Vector2.one*.5f;title.anchoredPosition=new Vector2(-10,card.rect.height*.38f);title.sizeDelta=new Vector2(300,36);supply.title.alignment=TextAnchor.MiddleCenter;supply.title.alignByGeometry=true;supply.title.resizeTextMinSize=18;supply.title.resizeTextMaxSize=26;
                foreach(var label in new[]{supply.cashLabel,supply.adLabel}){var r=label.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(12,5);r.offsetMax=new Vector2(-12,-5);label.alignment=TextAnchor.MiddleCenter;label.alignByGeometry=true;}
                foreach(var button in new[]{supply.cashButton,supply.adButton})((RectTransform)button.transform).anchoredPosition=new Vector2(0,-85);
                foreach(var slider in h.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
                {
                    var track=(RectTransform)slider.transform.Find("Track");if(!track)continue;
                    track.sizeDelta=new Vector2(250,24);var image=track.GetComponent<UnityEngine.UI.Image>();image.pixelsPerUnitMultiplier=image.sprite.rect.height/24;
                    var area=slider.transform.Find("ValueFillArea") as RectTransform;
                    if(!area){area=new GameObject("ValueFillArea",typeof(RectTransform)).GetComponent<RectTransform>();area.SetParent(slider.transform,false);}
                    area.anchorMin=area.anchorMax=Vector2.one*.5f;area.sizeDelta=new Vector2(230,14);area.anchoredPosition=Vector2.zero;area.SetSiblingIndex(track.GetSiblingIndex()+1);
                    var fill=area.Find("Fill") as RectTransform;if(!fill){fill=new GameObject("Fill",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();fill.SetParent(area,false);}
                    fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
                    var fillImage=fill.GetComponent<UnityEngine.UI.Image>();fillImage.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");fillImage.type=UnityEngine.UI.Image.Type.Sliced;fillImage.color=new Color(1,.78f,.04f);fillImage.raycastTarget=false;slider.fillRect=fill;fill.anchorMax=new Vector2(slider.normalizedValue,1);slider.handleRect.sizeDelta=Vector2.one*36;
                }
            }
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
