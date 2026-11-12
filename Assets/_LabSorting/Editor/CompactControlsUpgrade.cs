using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using LabSorting.Sorting;
using LabSorting.UI;
using LabSorting.Player;
using LabSorting.Services;
namespace LabSorting.Editor
{
    public static class CompactControlsUpgrade
    {
        static Font font;
        static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size)
        {
            var child=parent.Find(name);if(!child)child=new GameObject(name,typeof(RectTransform)).transform;
            child.SetParent(parent,false);var r=(RectTransform)child;r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;
        }
        static UnityEngine.UI.Text Text(string name,Transform parent,Vector2 pos,Vector2 size,int fs,string value="")
        {var r=Rect(name,parent,new Vector2(.5f,.5f),pos,size);var t=r.GetComponent<UnityEngine.UI.Text>();if(!t)t=r.gameObject.AddComponent<UnityEngine.UI.Text>();t.font=font;t.fontSize=fs;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.text=value;return t;}
        static void Position(RectTransform r,Vector2 anchor,Vector2 pos,Vector2 size){r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;}
        static void Wire(UnityEngine.UI.Button b,UnityEngine.Events.UnityAction action){while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddPersistentListener(b.onClick,action);}
        static void Icon(UnityEngine.UI.Button b,ControlGlyph.Symbol symbol,Vector2 anchor,Vector2 pos,float size,string caption)
        {
            Position((RectTransform)b.transform,anchor,pos,Vector2.one*size);
            var image=b.GetComponent<UnityEngine.UI.Image>();if(image)image.color=new Color(0,0,0,0);
            var r=Rect("ControlSymbol",b.transform,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*size);
            if(!r.GetComponent<CanvasRenderer>())r.gameObject.AddComponent<CanvasRenderer>();
            var glyph=r.GetComponent<ControlGlyph>();if(!glyph)glyph=r.gameObject.AddComponent<ControlGlyph>();glyph.symbol=symbol;glyph.raycastTarget=false;glyph.color=Color.white;glyph.accent=symbol==ControlGlyph.Symbol.Search?new Color(1,.8f,.2f):new Color(.3f,.95f,.9f);glyph.SetAllDirty();b.targetGraphic=glyph;
            var label=b.GetComponentInChildren<UnityEngine.UI.Text>(true);if(label){Position(label.rectTransform,new Vector2(.5f,.5f),new Vector2(0,-size*.5f-9),new Vector2(size+40,20));label.fontSize=13;label.text=caption;label.color=Color.white;label.raycastTarget=false;}
        }
        static UnityEngine.UI.Button Button(string name,Transform parent,Vector2 pos,Vector2 size,string caption,UnityEngine.Events.UnityAction action)
        {var r=Rect(name,parent,new Vector2(.5f,.5f),pos,size);var image=r.GetComponent<UnityEngine.UI.Image>();if(!image)image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.06f,.23f,.28f);var b=r.GetComponent<UnityEngine.UI.Button>();if(!b)b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;Wire(b,action);Text("Label",r,Vector2.zero,size-Vector2.one*8,19,caption);return b;}
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Exit play mode first");
            var s=Object.FindFirstObjectByType<SortingSession>();var hud=Object.FindFirstObjectByType<SortingHUD>();if(!s||!hud)throw new System.Exception("Open gameplay scene");
            var safe=(RectTransform)hud.transform.Find("SafeArea");if(!safe)throw new System.Exception("Missing SafeArea");font=hud.progress.font;
            s.placementReward=0;s.firstTenBonus=2;s.categoryBonus=3;s.halfBonus=5;s.completeBonus=10;s.maxCapacity=30;s.searchDuration=12;
            var guided=Object.FindFirstObjectByType<GuidedTutorial>();if(guided)guided.reward=3;
            var economy=hud.GetComponent<EconomyHUD>();var actions=hud.GetComponent<ContextActionHUD>();var settings=hud.GetComponent<SettingsTutorialPanel>();var progress=hud.GetComponent<ItemsProgressPanel>();
            var commerce=Object.FindFirstObjectByType<SupplyCommerce>();if(!commerce){commerce=s.gameObject.AddComponent<SupplyCommerce>();commerce.session=s;}commerce.settings=AssetDatabase.LoadAssetAtPath<LabServiceSettings>("Assets/_LabSorting/Settings/LabServiceSettings.asset");
            var panel=hud.GetComponent<SupplyPanel>();if(!panel)panel=hud.gameObject.AddComponent<SupplyPanel>();panel.session=s;panel.commerce=commerce;
            economy.compact=true;
            Icon(actions.pick.GetComponent<UnityEngine.UI.Button>(),ControlGlyph.Symbol.Pick,Vector2.one*0+new Vector2(1,0),new Vector2(-76,92),82,"PICK");
            Icon(actions.place.GetComponent<UnityEngine.UI.Button>(),ControlGlyph.Symbol.Place,new Vector2(1,0),new Vector2(-76,202),82,"PLACE");
            Icon(actions.drop.GetComponent<UnityEngine.UI.Button>(),ControlGlyph.Symbol.Throw,new Vector2(1,0),new Vector2(-184,92),70,"DROP");
            Icon(economy.searchButton,ControlGlyph.Symbol.Search,new Vector2(1,.5f),new Vector2(-72,60),66,"SEARCH +");Wire(economy.searchButton,panel.OpenSearch);
            Icon(economy.magnetButton,ControlGlyph.Symbol.Magnet,new Vector2(1,.5f),new Vector2(-72,-35),66,"MAGNET +");Wire(economy.magnetButton,panel.OpenMagnet);
            Icon(economy.upgradeButton,ControlGlyph.Symbol.Slots,new Vector2(0,1),new Vector2(220,-45),60,"CAPACITY");Wire(economy.upgradeButton,panel.OpenSlots);
            if(settings)Icon(settings.openButton,ControlGlyph.Symbol.Settings,new Vector2(0,1),new Vector2(48,-45),56,"SETTINGS");
            if(progress)Icon(progress.openButton,ControlGlyph.Symbol.List,new Vector2(0,1),new Vector2(130,-45),56,"ITEMS");
            var wallet=Button("CashOffers",safe,Vector2.zero,new Vector2(144,48),"",panel.OpenCash);Position((RectTransform)wallet.transform,new Vector2(1,1),new Vector2(-98,-40),new Vector2(144,48));economy.wallet=wallet.GetComponentInChildren<UnityEngine.UI.Text>();
            foreach(var text in safe.GetComponentsInChildren<UnityEngine.UI.Text>(true))if(text.text.Contains("WASD move"))text.gameObject.SetActive(false);
            foreach(var touch in hud.GetComponentsInChildren<LabTouchArea>(true)){touch.session=s;if(touch.movement){Position((RectTransform)touch.transform,Vector2.zero,new Vector2(100,100),new Vector2(145,145));}}
            hud.compact=true;hud.progress.fontSize=18;Position(hud.progress.rectTransform,new Vector2(.5f,1),new Vector2(0,-34),new Vector2(380,46));
            var rewards=hud.GetComponent<SortingRewardsHUD>();if(rewards){rewards.milestone.fontSize=13;rewards.showMagnetChips=false;}
            var locator=hud.GetComponent<ItemLocatorHUD>();if(!locator)locator=hud.gameObject.AddComponent<ItemLocatorHUD>();locator.session=s;locator.safeArea=safe;
            locator.focusFrame=Rect("FocusCorners",safe,new Vector2(.5f,.5f),Vector2.zero,Vector2.one*80);if(!locator.focusFrame.GetComponent<CanvasRenderer>())locator.focusFrame.gameObject.AddComponent<CanvasRenderer>();var corners=locator.focusFrame.GetComponent<FocusCorners>();if(!corners)corners=locator.focusFrame.gameObject.AddComponent<FocusCorners>();corners.color=new Color(.3f,1,1,.9f);corners.raycastTarget=false;
            locator.beacons=new UnityEngine.UI.Text[6];for(int i=0;i<6;i++){var label=Text("SearchBeacon"+i,safe,Vector2.zero,new Vector2(96,28),18);label.color=new Color(1,.9f,.15f);var shadow=label.GetComponent<UnityEngine.UI.Shadow>();if(!shadow)shadow=label.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectDistance=Vector2.one*2;shadow.effectColor=Color.black;locator.beacons[i]=label;}
            economy.marker.fontSize=22;economy.marker.color=new Color(1,.9f,.15f);var markerBg=economy.marker.GetComponent<UnityEngine.UI.Shadow>();if(!markerBg)markerBg=economy.marker.gameObject.AddComponent<UnityEngine.UI.Shadow>();markerBg.effectDistance=Vector2.one*2;
            var root=Rect("SupplyOffers",safe,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            var backdrop=root.GetComponent<UnityEngine.UI.Image>();if(!backdrop)backdrop=root.gameObject.AddComponent<UnityEngine.UI.Image>();backdrop.color=new Color(0,0,0,.78f);panel.root=root.gameObject;
            var card=Rect("Card",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(680,380));var cardImage=card.GetComponent<UnityEngine.UI.Image>();if(!cardImage)cardImage=card.gameObject.AddComponent<UnityEngine.UI.Image>();cardImage.color=new Color(.025f,.09f,.13f,.99f);
            panel.title=Text("Title",card,new Vector2(0,150),new Vector2(570,42),27);
            panel.description=Text("Description",card,new Vector2(0,100),new Vector2(610,48),18);
            panel.owned=Text("Owned",card,new Vector2(0,55),new Vector2(610,30),18);
            panel.cashButton=Button("Cash",card,new Vector2(-210,-15),new Vector2(190,80),"",panel.BuyCash);panel.cashLabel=panel.cashButton.GetComponentInChildren<UnityEngine.UI.Text>();
            panel.adButton=Button("RewardVideo",card,new Vector2(0,-15),new Vector2(190,80),"",panel.Watch);panel.adLabel=panel.adButton.GetComponentInChildren<UnityEngine.UI.Text>();
            panel.storeButton=Button("Store",card,new Vector2(210,-15),new Vector2(190,80),"",panel.BuyStore);panel.storeLabel=panel.storeButton.GetComponentInChildren<UnityEngine.UI.Text>();
            panel.useButton=Button("Use",card,new Vector2(0,-105),new Vector2(280,50),"",panel.Use);panel.useLabel=panel.useButton.GetComponentInChildren<UnityEngine.UI.Text>();
            panel.status=Text("Status",card,new Vector2(0,-157),new Vector2(620,34),15);
            panel.closeButton=Button("Close",card,new Vector2(300,150),new Vector2(42,42),"X",panel.Close);root.gameObject.SetActive(false);
            EditorUtility.SetDirty(s);EditorUtility.SetDirty(hud);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);
        }
    }
}


