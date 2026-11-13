using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        public static void ApplyMargins()
        {
            Load();var h=Object.FindFirstObjectByType<SortingHUD>();var safe=h.transform.Find("SafeArea");
            var supply=h.GetComponent<SupplyPanel>();var card=(RectTransform)supply.title.transform.parent;
            var art=Rect("OfferArtwork",card,card.sizeDelta);Art(art,card.GetComponent<UnityEngine.UI.Image>().sprite,false);art.SetAsFirstSibling();art.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;supply.offerArtwork=art;card.GetComponent<UnityEngine.UI.Image>().color=Color.clear;
            Pos(supply.title.rectTransform,Vector2.one*.5f,new Vector2(-10,card.sizeDelta.y*.38f),new Vector2(300,32));
            var list=h.inventoryPanel.transform;Pos((RectTransform)list.Find("CapacityPlus"),new Vector2(1,1),new Vector2(-36,-30),Vector2.one*32);
            Pos(h.inventorySummary.rectTransform,new Vector2(.5f,1),new Vector2(-25,-30),new Vector2(210,28));h.inventorySummary.resizeTextMaxSize=18;
            for(int i=0;i<h.inventoryButtons.Length;i++)
            {
                var row=h.inventoryButtons[i];var layout=Ensure<UnityEngine.UI.LayoutElement>(row);layout.preferredHeight=60;layout.minHeight=60;row.image.pixelsPerUnitMultiplier=2.4f;
                Pos(h.inventoryIcons[i].rectTransform,new Vector2(0,.5f),new Vector2(25,0),new Vector2(34,38));
                Pos(h.inventoryLabels[i].rectTransform,new Vector2(0,.5f),new Vector2(120,0),new Vector2(142,48));h.inventoryLabels[i].resizeTextMaxSize=17;
                Pos(h.inventoryCounts[i].rectTransform,new Vector2(1,.5f),new Vector2(-24,0),new Vector2(30,28));
            }
            var tutorial=h.GetComponent<GuidedTutorial>();tutorial.bubble.anchoredPosition=new Vector2(-330,-185);
            var task=h.GetComponent<TaskCardLayout>();task.card.anchoredPosition=new Vector2(-330,-185);
            var rewards=h.GetComponent<SortingRewardsHUD>();var track=(RectTransform)rewards.shortGoalFill.parent;track.sizeDelta=new Vector2(240,12);
            var trackImage=track.GetComponent<UnityEngine.UI.Image>();trackImage.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");trackImage.type=UnityEngine.UI.Image.Type.Sliced;trackImage.pixelsPerUnitMultiplier=1;trackImage.color=new Color(.02f,.10f,.22f);
            var fill=rewards.shortGoalFill.GetComponent<UnityEngine.UI.Image>();fill.sprite=trackImage.sprite;fill.type=UnityEngine.UI.Image.Type.Sliced;fill.pixelsPerUnitMultiplier=1;fill.color=new Color(.3f,.9f,.7f);rewards.shortGoalFill.offsetMin=new Vector2(2,2);rewards.shortGoalFill.offsetMax=new Vector2(-2,-2);
            var selected=(RectTransform)safe.Find("SelectedItemCard");selected.sizeDelta=new Vector2(440,76);h.active.rectTransform.sizeDelta=new Vector2(330,52);h.active.resizeTextMaxSize=20;h.activeIcon.rectTransform.sizeDelta=Vector2.one*36;
            var progress=h.GetComponent<ItemsProgressPanel>();((RectTransform)progress.closeButton.transform).anchoredPosition=new Vector2(-68,-100);
            var menus=h.GetComponent<DesignerMenus>();var shop=(RectTransform)menus.storeRoot.transform.Find("Card");shop.sizeDelta=new Vector2(1020,590);
            for(int i=0;i<menus.purchases.Length;i++)
            {
                var tile=menus.purchases[i].transform.parent;
                Pos((RectTransform)tile.Find("Name"),Vector2.one*.5f,new Vector2(0,105),new Vector2(156,40));
                Pos((RectTransform)menus.purchases[i].transform,Vector2.one*.5f,new Vector2(0,-100),new Vector2(152,44));
                menus.prices[i].rectTransform.sizeDelta=new Vector2(132,32);menus.prices[i].resizeTextMaxSize=19;
            }
            ((RectTransform)shop.Find("ProductsViewport")).anchoredPosition=new Vector2(0,26);
            ((RectTransform)shop.Find("ProductsScrollbar")).anchoredPosition=new Vector2(0,-154);
            Pos((RectTransform)menus.cashAdButton.transform,Vector2.one*.5f,new Vector2(0,-210),new Vector2(340,46));
            Pos(menus.storeStatus.rectTransform,Vector2.one*.5f,new Vector2(0,-258),new Vector2(870,28));
            // Keep text inside curved title ends and improve service-panel contrast.
            foreach(var text in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var background=text.transform.parent.Find("DesignerTitle_"+text.name);
                if(background){var r=(RectTransform)background;var t=text.rectTransform;t.sizeDelta=new Vector2(Mathf.Min(t.sizeDelta.x,r.sizeDelta.x-64),Mathf.Min(t.sizeDelta.y,r.sizeDelta.y-16));}
                if(text.text=="RETRY")text.color=new Color(.08f,.06f,.02f);
            }
            var service=h.GetComponent<ServicePresentation>();if(service){var offline=service.offlineRoot.transform.Find("Card");offline.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=1.15f;}
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene);EditorSceneManager.SaveScene(h.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
