using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class StoreTouchUpgrade
    {
        public static void Apply()
        {
            var hud=Object.FindFirstObjectByType<SortingHUD>();var menus=hud.GetComponent<DesignerMenus>();var scroll=menus.storeRoot.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);var guard=scroll.GetComponent<StoreScrollGuard>()??scroll.gameObject.AddComponent<StoreScrollGuard>();guard.scroll=scroll;menus.storeScrollGuard=guard;scroll.decelerationRate=.08f;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            foreach(var button in menus.purchases){var relay=button.GetComponent<StoreScrollGuard>()??button.gameObject.AddComponent<StoreScrollGuard>();relay.owner=guard;var icon=(RectTransform)button.transform.parent.Find("DesignerIcon");icon.anchoredPosition=new Vector2(0,38);icon.sizeDelta=Vector2.one*116;}
            foreach(var text in menus.prices){text.rectTransform.sizeDelta=new Vector2(172,30);text.resizeTextForBestFit=true;text.resizeTextMinSize=14;text.resizeTextMaxSize=20;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.alignByGeometry=true;}
            var ad=(RectTransform)menus.cashAdButton.transform;ad.sizeDelta=new Vector2(330,44);var image=ad.Find("RewardVideoIcon") as RectTransform;if(!image){image=new GameObject("RewardVideoIcon",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();image.SetParent(ad,false);}image.anchorMin=image.anchorMax=image.pivot=Vector2.one*.5f;image.sizeDelta=new Vector2(34,28);image.anchoredPosition=new Vector2(-133,0);var art=image.GetComponent<UnityEngine.UI.Image>();art.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/_LabSorting/Art/UI/Lab sorting game mockup and sprites 1/Gameplay/Magnet count panel sprite/ChatGPT Image Nov 12, 2026, 12_05_03 PM-3.png").OfType<Sprite>().First();art.preserveAspect=true;art.raycastTarget=false;
            var label=menus.cashAdLabel.rectTransform;label.anchorMin=Vector2.zero;label.anchorMax=Vector2.one;label.offsetMin=new Vector2(48,5);label.offsetMax=new Vector2(-12,-5);menus.cashAdLabel.alignment=TextAnchor.MiddleCenter;
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);
        }
    }
}
