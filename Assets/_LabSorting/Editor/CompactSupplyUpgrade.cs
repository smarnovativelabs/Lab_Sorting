using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        public static void ApplyCompactSupply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Stop play mode first");
            Load();var supply=Object.FindFirstObjectByType<SupplyPanel>();var card=(RectTransform)supply.title.transform.parent;
            var art=MockupSprite(Game+"Magnet count panel sprite/Blank Glossy Blue Game UI Panel.png","Mockup_OfferPanel",Vector4.zero);
            float width=560,height=width*art.rect.height/art.rect.width;
            Pos(card,new Vector2(1,.5f),new Vector2(-410,-35),new Vector2(width,height));Art(card,art,false);
            var old=card.Find("DesignerTitle_"+supply.title.name);if(old)old.gameObject.SetActive(false);
            Pos(supply.title.rectTransform,Vector2.one*.5f,new Vector2(-10,height*.38f),new Vector2(330,40));supply.title.resizeTextMinSize=20;supply.title.resizeTextMaxSize=26;
            supply.description.gameObject.SetActive(false);supply.owned.gameObject.SetActive(false);
            Pos((RectTransform)supply.closeButton.transform,Vector2.one*.5f,new Vector2(248,height*.37f),Vector2.one*42);
            for(int i=0;i<2;i++)
            {
                var tile=(RectTransform)card.Find(i==0?"CashOfferCard":"AdOfferCard");Pos(tile,Vector2.one*.5f,new Vector2(i==0?-136:106,-35),new Vector2(226,260));tile.GetComponent<UnityEngine.UI.Image>().enabled=false;
                var icon=(RectTransform)tile.Find("DesignerIcon");Pos(icon,Vector2.one*.5f,new Vector2(-45,82),Vector2.one*54);
                var reward=i==0?supply.cashRewardIcon:supply.adRewardIcon;Pos(reward.rectTransform,Vector2.one*.5f,new Vector2(45,82),Vector2.one*54);reward.preserveAspect=true;
                var detail=i==0?supply.cashOfferDetails:supply.adOfferDetails;Pos(detail.rectTransform,Vector2.one*.5f,new Vector2(0,5),new Vector2(210,98));detail.resizeTextMinSize=18;detail.resizeTextMaxSize=23;
                var button=i==0?supply.cashButton:supply.adButton;Pos((RectTransform)button.transform,Vector2.one*.5f,new Vector2(0,-79),new Vector2(208,51));
                // The supplied artwork already contains the gold buttons.
                var bg=button.GetComponent<UnityEngine.UI.Image>();bg.color=Color.clear;button.transition=UnityEngine.UI.Selectable.Transition.None;
                var label=i==0?supply.cashLabel:supply.adLabel;Pos(label.rectTransform,Vector2.one*.5f,Vector2.zero,new Vector2(196,42));label.resizeTextMinSize=17;label.resizeTextMaxSize=22;
            }
            Pos(supply.status.rectTransform,Vector2.one*.5f,new Vector2(-12,-height*.5f-24),new Vector2(520,44));supply.status.resizeTextMinSize=16;supply.status.resizeTextMaxSize=20;
            var dismiss=Ensure<SupplyBackdropDismiss>(supply.root.transform);dismiss.panel=supply;dismiss.card=card;
            supply.root.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;card.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            EditorSceneManager.MarkSceneDirty(supply.gameObject.scene);EditorSceneManager.SaveScene(supply.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
