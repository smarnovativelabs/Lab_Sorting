using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
using LabSorting.Sorting;
using LabSorting.Services;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        public static void ApplyOffersAndReplay()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Stop Play Mode before authoring");
            Load();var hud=Object.FindFirstObjectByType<SortingHUD>();var s=hud.session;var supply=hud.GetComponent<SupplyPanel>();var menus=hud.GetComponent<DesignerMenus>();var economy=hud.GetComponent<EconomyHUD>();var rewards=hud.GetComponent<SortingRewardsHUD>();
            s.bundleEconomy=true;s.capacity=10;s.maxCapacity=60;s.searchCost=6;s.magnetCost=30;s.upgradeBaseCost=38;s.upgradeStepCost=0;
            var config=Object.FindFirstObjectByType<SupplyCommerce>().settings;config.rewardedSlotAmount=2;config.rewardedSearchAmount=3;config.rewardedMagnetAmount=2;config.rewardedCashAmount=50;EditorUtility.SetDirty(config);
            economy.upgradeButton.gameObject.SetActive(false);Wire(economy.wallet.GetComponentInParent<UnityEngine.UI.Button>(true),menus.OpenStore);
            supply.twoOfferLayout=true;supply.searchSprite=sprites["search"];supply.magnetSprite=sprites["magnet"];supply.slotSprite=sprites["slots"];
            var card=(RectTransform)supply.title.transform.parent;card.sizeDelta=new Vector2(680,470);
            Pos(supply.title.rectTransform,Vector2.one*.5f,new Vector2(0,208),new Vector2(350,42));BackTitle(supply.title);
            Pos((RectTransform)supply.closeButton.transform,Vector2.one*.5f,new Vector2(300,202),Vector2.one*48);
            Pos(supply.description.rectTransform,Vector2.one*.5f,new Vector2(0,155),new Vector2(590,32));
            Pos(supply.owned.rectTransform,Vector2.one*.5f,new Vector2(0,119),new Vector2(590,32));
            for(int i=0;i<2;i++)
            {
                var tile=Rect(i==0?"CashOfferCard":"AdOfferCard",card,new Vector2(290,265));tile.anchoredPosition=new Vector2(i==0?-155:155,-37);Panel(tile);
                Icon(tile,i==0?"cash":"video",54,new Vector2(-52,87));var reward=Rect("RewardIcon",tile,Vector2.one*54);reward.anchoredPosition=new Vector2(52,87);var image=Skin(reward,"magnet");
                var details=Label("Details",tile,"",new Vector2(0,7),new Vector2(260,100),23);
                var button=i==0?supply.cashButton:supply.adButton;button.transform.SetParent(tile,false);Pos((RectTransform)button.transform,Vector2.one*.5f,new Vector2(0,-91),new Vector2(252,52));button.targetGraphic=Skin(button.transform,"gold",true,true);
                var oldIcon=button.transform.Find("DesignerIcon");if(oldIcon)oldIcon.gameObject.SetActive(false);
                var label=i==0?supply.cashLabel:supply.adLabel;Pos(label.rectTransform,Vector2.one*.5f,Vector2.zero,new Vector2(230,42));label.color=Color.black;
                if(i==0){supply.cashOfferDetails=details;supply.cashRewardIcon=image;}else{supply.adOfferDetails=details;supply.adRewardIcon=image;}
            }
            supply.storeButton.gameObject.SetActive(false);supply.useButton.gameObject.SetActive(false);Pos(supply.status.rectTransform,Vector2.one*.5f,new Vector2(0,-207),new Vector2(590,36));
            var progress=hud.GetComponent<ItemsProgressPanel>();progress.soundLabel.GetComponentInParent<UnityEngine.UI.Button>(true).gameObject.SetActive(false);progress.hapticsLabel.GetComponentInParent<UnityEngine.UI.Button>(true).gameObject.SetActive(false);
            foreach(var icon in progress.rowIcons)if(icon)icon.gameObject.SetActive(false);
            progress.rowCounts=new UnityEngine.UI.Text[progress.rows.Length];
            for(int i=0;i<progress.rows.Length;i++){var row=progress.rows[i];row.rectTransform.offsetMin=new Vector2(22,4);row.rectTransform.offsetMax=new Vector2(-180,-4);row.alignment=TextAnchor.MiddleLeft;var count=Label("Count",row.transform.parent,"",Vector2.zero,new Vector2(150,38),22);Pos(count.rectTransform,new Vector2(1,.5f),new Vector2(-93,0),new Vector2(150,38));count.alignment=TextAnchor.MiddleRight;count.color=new Color(1,.9f,.3f);progress.rowCounts[i]=count;}
            progress.summary.rectTransform.sizeDelta=new Vector2(700,68);progress.summary.resizeTextMaxSize=24;BackTitle(progress.summary);
            var shop=menus.storeRoot.transform.Find("Card");menus.cashAdButton=Button("CashRewardAd",shop,"WATCH AD +50 CASH",new Vector2(0,-203),new Vector2(320,45),menus.WatchCash,"gold");menus.cashAdLabel=menus.cashAdButton.GetComponentInChildren<UnityEngine.UI.Text>(true);menus.cashAdLabel.color=Color.black;
            Pos(menus.storeStatus.rectTransform,Vector2.one*.5f,new Vector2(0,-242),new Vector2(900,28));
            Pos(rewards.milestone.rectTransform,new Vector2(1,1),new Vector2(-240,-290),new Vector2(430,44));BackTitle(rewards.milestone);
            Pos((RectTransform)rewards.shortGoalFill.parent,new Vector2(1,1),new Vector2(-240,-321),new Vector2(360,10));
            var completion=hud.GetComponent<LabCompletionPanel>();Wire(completion.continueButton,completion.PlayAgain);completion.continueButton.GetComponentInChildren<UnityEngine.UI.Text>(true).text="PLAY AGAIN";
            var explore=Button("Explore",completion.card,"KEEP EXPLORING",new Vector2(0,-195),new Vector2(300,40),completion.ContinueExploring);Pos((RectTransform)completion.continueButton.transform,Vector2.one*.5f,new Vector2(0,-142),new Vector2(300,48));
            foreach(var t in new[]{supply.cashOfferDetails,supply.adOfferDetails,menus.cashAdLabel})if(!t.GetComponent<LabLocalizedLabel>())t.gameObject.AddComponent<LabLocalizedLabel>();
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);
        }
    }
}
