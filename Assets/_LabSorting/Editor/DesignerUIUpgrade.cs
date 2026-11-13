using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEditor.U2D.Sprites;
using LabSorting.UI;
using LabSorting.Player;
using LabSorting.Sorting;
using LabSorting.Services;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        const string Root="Assets/_LabSorting/Art/UI/";
        const string Game=Root+"Lab sorting game mockup and sprites 1/Gameplay/";
        static readonly Dictionary<string,string> Paths=new Dictionary<string,string>{
            {"plus",Game+"glossy_blue_and_gold_game_ui_icons_13.png"},
            {"cream",Game+"collected items sprite/polished_blue-and-gold_ui_asset_sheet_04.png"},
            {"pausebar",Game+"glossy_blue_and_gold_game_ui_icons_07.png"},
            {"round",Game+"glossy_blue_and_gold_game_ui_icons_02.png"},
            {"frame",Game+"glossy_blue_and_gold_game_ui_icons_01.png"},
            {"title",Game+"glossy_blue_and_gold_game_ui_icons_14 (1).png"},
            {"green",Game+"Layer 2.png"},
            {"gold",Root+"Store and settings panel sprite/Store/Layer 3 copy.png"},
            {"blue",Root+"Lab sorting game mockup and sprites 1/Pause/Layer 2.png"},
            {"close",Root+"Lab sorting game mockup and sprites 1/Pause/Layer 4.png"},
            {"joystick",Game+"ChatGPT Image Nov 12, 2026, 12_07_19 PM-2.png"},
            {"knob",Game+"ChatGPT Image Nov 12, 2026, 12_07_16 PM-1.png"},
            {"pick",Game+"glossy_3d_medical_ui_icon_collection_05.png"},
            {"search",Game+"glossy_3d_medical_ui_icon_collection_06.png"},
            {"magnet",Game+"glossy_3d_medical_ui_icon_collection_07.png"},
            {"drop",Game+"glossy_3d_medical_ui_icon_collection_09.png"},
            {"items",Game+"glossy_3d_medical_ui_icon_collection_11.png"},
            {"store",Game+"glossy_blue_and_gold_game_ui_icons_10.png"},
            {"slots",Root+"Store and settings panel sprite/Store/Layer 3.png"},
            {"wallet",Game+"glossy_blue_and_gold_game_ui_icons_16.png"},
            {"cash",Root+"Store and settings panel sprite/Store/Green_cash_bundle_game_icon_21bc9553.png"},
            {"cashLarge",Root+"Store and settings panel sprite/Store/Cash_pack_game_icon_bc9bd6f6.png"},
            {"noads",Root+"Store and settings panel sprite/Store/No_ads_3D_game_icon_e20dc271.png"},
            {"doctor",Game+"Glossy Tutorial Panel with Doctor.png"},
            {"video",Game+"Magnet count panel sprite/ChatGPT Image Nov 12, 2026, 12_05_03 PM-3.png"},
            {"settings",Root+"Lab sorting game mockup and sprites 1/Main Menu/setting icon.png"},
            {"left",Game+"glossy_3d_medical_ui_icon_collection_03.png"},
            {"right",Game+"glossy_3d_medical_ui_icon_collection_04.png"},
            {"track",Game+"glossy_blue_and_gold_game_ui_icons_18.png"},
            {"fill",Game+"glossy_blue_and_gold_game_ui_icons_19.png"},
            {"background",Root+"Lab sorting game mockup and sprites 1/Loading Screen/Layer 2.png"},
            {"logo",Root+"Lab sorting game mockup and sprites 1/Loading Screen/Layer 3.png"}
        };
        static Dictionary<string,Sprite> sprites;
        static Font font,headingFont;
        public static void ImportArt()
        {
            foreach(var pair in Paths)
            {
                var importer=AssetImporter.GetAtPath(pair.Value) as TextureImporter;if(!importer)throw new Exception("Missing artwork: "+pair.Value);
                var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(pair.Value));var pixels=tex.GetPixels32();int x0=tex.width,y0=tex.height,x1=0,y1=0;
                for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)if(pixels[y*tex.width+x].a>12){x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y);}
                var rect=UnityEngine.Rect.MinMaxRect(Math.Max(0,x0-1),Math.Max(0,y0-1),Math.Min(tex.width,x1+2),Math.Min(tex.height,y1+2));UnityEngine.Object.DestroyImmediate(tex);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.isReadable=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=pair.Key=="background"?2048:pair.Key=="doctor"||pair.Key=="logo"?1024:512;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();if(capability==null)throw new Exception("Sprite edit capability missing");var cap=capability.GetEditCapability();
                foreach(var needed in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot,EEditCapability.EditSpriteName})if(!cap.HasCapability(needed))throw new Exception("Sprite operation unsupported: "+needed);
                var old=provider.GetSpriteRects();var id=old.Length>0?old[0].spriteID:GUID.Generate();float border=Math.Min(rect.width,rect.height)*.36f;
                bool sliced=pair.Key=="cream"||pair.Key=="frame"||pair.Key=="title"||pair.Key=="green"||pair.Key=="gold"||pair.Key=="blue"||pair.Key=="track"||pair.Key=="fill";
                provider.SetSpriteRects(new[]{new SpriteRect{name="LabUI_"+pair.Key,rect=rect,pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,border=sliced?(pair.Key=="frame"?Vector4.one*border:new Vector4(rect.height*.5f,0,rect.height*.5f,0)):Vector4.zero,spriteID=id}});
                var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();if(names!=null)names.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair("LabUI_"+pair.Key,id)});provider.Apply();importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
        static T Ensure<T>(Component owner) where T:Component {var c=owner.GetComponent<T>();return c?c:owner.gameObject.AddComponent<T>();}
        static T Ensure<T>(GameObject owner) where T:Component {var c=owner.GetComponent<T>();return c?c:owner.AddComponent<T>();}
        static void Load(){sprites=Paths.ToDictionary(p=>p.Key,p=>AssetDatabase.LoadAllAssetsAtPath(p.Value).OfType<Sprite>().First());font=AssetDatabase.LoadAssetAtPath<Font>(Root+"onlygraphic/Onlygraphic Rounded.otf");headingFont=AssetDatabase.LoadAssetAtPath<Font>(Root+"onlygraphic/Onlygraphic Bold.otf");}
        static RectTransform Rect(string name,Transform parent,Vector2 size)
        {var t=parent.Find(name);if(!t)t=new GameObject(name,typeof(RectTransform)).transform;t.SetParent(parent,false);var r=(RectTransform)t;r.localScale=Vector3.one;r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=Vector2.zero;r.sizeDelta=size;return r;}
        static void Pos(RectTransform r,Vector2 anchor,Vector2 position,Vector2 size){r.anchorMin=r.anchorMax=anchor;r.pivot=Vector2.one*.5f;r.anchoredPosition=position;r.sizeDelta=size;}
        static UnityEngine.UI.Image Skin(Transform t,string key,bool slice=false,bool raycast=false)
        {var im=t.GetComponent<UnityEngine.UI.Image>();if(!im)im=t.gameObject.AddComponent<UnityEngine.UI.Image>();im.sprite=sprites[key];im.color=Color.white;im.raycastTarget=raycast;im.type=slice?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;im.preserveAspect=!slice;if(slice)im.pixelsPerUnitMultiplier=key=="frame"?Mathf.Max(1,sprites[key].border.x/24f):Mathf.Max(.01f,sprites[key].rect.height/Mathf.Max(1,((RectTransform)t).rect.height));return im;}
        static UnityEngine.UI.Text Label(string name,Transform parent,string value,Vector2 pos,Vector2 size,int fontSize=20)
        {var r=Rect(name,parent,size);r.anchoredPosition=pos;var t=r.GetComponent<UnityEngine.UI.Text>();if(!t)t=r.gameObject.AddComponent<UnityEngine.UI.Text>();t.font=font;t.fontStyle=FontStyle.Normal;t.fontSize=fontSize;t.text=value;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.resizeTextForBestFit=true;t.resizeTextMinSize=14;t.resizeTextMaxSize=fontSize;return t;}
        static void Icon(Transform parent,string key,float size,Vector2 pos)
        {var r=Rect("DesignerIcon",parent,Vector2.one*size);r.anchoredPosition=pos;Skin(r,key);}
        static void Wire(UnityEngine.UI.Button b,UnityEngine.Events.UnityAction call){while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddPersistentListener(b.onClick,call);}
        static UnityEngine.UI.Button Button(string name,Transform parent,string caption,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction call,string style="blue")
        {var r=Rect(name,parent,size);r.anchoredPosition=pos;var b=r.GetComponent<UnityEngine.UI.Button>();if(!b)b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=Skin(r,style,true,true);if(call!=null)Wire(b,call);Label("Label",r,caption,Vector2.zero,size-new Vector2(24,10));return b;}
        static void StyleButton(UnityEngine.UI.Button b,string key="blue")
        {b.targetGraphic=Skin(b.transform,key,true,true);var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,1,.87f);colors.pressedColor=new Color(.75f,.85f,1);colors.disabledColor=new Color(.55f,.6f,.7f,.8f);b.colors=colors;foreach(var t in b.GetComponentsInChildren<UnityEngine.UI.Text>(true)){t.color=Color.white;t.fontStyle=FontStyle.Bold;t.resizeTextForBestFit=true;t.resizeTextMinSize=14;}}
        static void RoundButton(UnityEngine.UI.Button b,string icon)
        {foreach(var g in b.GetComponentsInChildren<ControlGlyph>(true))g.gameObject.SetActive(false);b.targetGraphic=Skin(b.transform,"round",false,true);if(icon!=null)Icon(b.transform,icon,Mathf.Min(((RectTransform)b.transform).sizeDelta.x,((RectTransform)b.transform).sizeDelta.y)*.60f,Vector2.zero);}
        static void CloseButton(UnityEngine.UI.Button b)
        {b.targetGraphic=Skin(b.transform,"close",false,true);((RectTransform)b.transform).sizeDelta=Vector2.one*48;foreach(var t in b.GetComponentsInChildren<UnityEngine.UI.Text>(true))t.enabled=false;}
        static void Panel(Transform t){Skin(t,"frame",true,true);}
        static void BackTitle(UnityEngine.UI.Text t)
        {var r=Rect("DesignerTitle_"+t.name,t.transform.parent,t.rectTransform.sizeDelta+new Vector2(25,8));r.anchorMin=t.rectTransform.anchorMin;r.anchorMax=t.rectTransform.anchorMax;r.pivot=t.rectTransform.pivot;r.anchoredPosition=t.rectTransform.anchoredPosition;Skin(r,"title",true);r.SetSiblingIndex(t.transform.GetSiblingIndex());t.transform.SetAsLastSibling();t.color=Color.white;t.font=headingFont;t.fontStyle=FontStyle.Normal;var close=t.transform.parent.Find("Close");if(close)close.SetAsLastSibling();}
        public static void ApplyGameplay()
        {
            Load();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();if(!hud)throw new Exception("Gameplay scene required");var safe=(RectTransform)hud.transform.Find("SafeArea");var economy=hud.GetComponent<EconomyHUD>();economy.designerSkin=true;var settings=hud.GetComponent<SettingsTutorialPanel>();var progress=hud.GetComponent<ItemsProgressPanel>();var actions=hud.GetComponent<ContextActionHUD>();var rewards=hud.GetComponent<SortingRewardsHUD>();var supply=hud.GetComponent<SupplyPanel>();
            // Preserve every existing action component and event; replace only artwork/layout.
            foreach(var b in hud.GetComponentsInChildren<UnityEngine.UI.Button>(true))StyleButton(b);
            RoundButton(actions.pick.GetComponent<UnityEngine.UI.Button>(),"pick");RoundButton(actions.drop.GetComponent<UnityEngine.UI.Button>(),"drop");
            var place=actions.place.GetComponent<UnityEngine.UI.Button>();Pos((RectTransform)place.transform,new Vector2(1,0),new Vector2(-76,202),Vector2.one*82);RoundButton(place,"items");
            RoundButton(economy.searchButton,"search");RoundButton(economy.magnetButton,"magnet");RoundButton(economy.upgradeButton,"slots");RoundButton(settings.openButton,"settings");RoundButton(progress.openButton,"items");
            foreach(var touch in hud.GetComponentsInChildren<LabTouchArea>(true))if(touch.movement){foreach(var g in touch.GetComponentsInChildren<ControlGlyph>(true))g.gameObject.SetActive(false);Skin(touch.transform,"joystick",false,true);Skin(touch.knob,"knob");}
            hud.designerSkin=true;foreach(var b in hud.inventoryButtons)StyleButton(b,"blue");
            foreach(var im in hud.inventoryIcons)if(im)im.preserveAspect=true;
            foreach(var b in new[]{hud.previousPage,hud.nextPage,progress.previousButton,progress.nextButton}){RoundButton(b,b==hud.previousPage||b==progress.previousButton?"left":"right");foreach(var t in b.GetComponentsInChildren<UnityEngine.UI.Text>(true))t.enabled=false;}
            var wallet=economy.wallet.transform.parent;Skin(wallet,"wallet",false,true);Pos((RectTransform)wallet,new Vector2(1,1),new Vector2(-128,-42),new Vector2(220,60));Pos(economy.wallet.rectTransform,Vector2.one*.5f,new Vector2(3,0),new Vector2(110,38));economy.wallet.fontStyle=FontStyle.Bold;economy.wallet.fontSize=23;
            Panel(safe.Find("SelectedItemCard"));BackTitle(hud.progress);BackTitle(rewards.milestone);
            Skin(rewards.shortGoalFill.parent,"track",true);Skin(rewards.shortGoalFill,"fill",true);((RectTransform)rewards.shortGoalFill.parent).sizeDelta=new Vector2(360,10);
            Panel(progress.card);BackTitle(progress.summary);CloseButton(progress.closeButton);            for(int i=0;i<progress.rows.Length;i++){var row=progress.rows[i];Skin(row.transform.parent,"blue",true);var r=(RectTransform)row.transform.parent;r.sizeDelta=new Vector2(790,48);r.anchoredPosition=new Vector2(0,-133-i*51);row.rectTransform.anchorMin=Vector2.zero;row.rectTransform.anchorMax=Vector2.one;row.rectTransform.offsetMin=new Vector2(64,4);row.rectTransform.offsetMax=new Vector2(-14,-4);if(progress.rowIcons[i])Pos(progress.rowIcons[i].rectTransform,new Vector2(0,.5f),new Vector2(31,0),Vector2.one*34);}
            Panel(settings.title.transform.parent);BackTitle(settings.title);CloseButton(settings.closeButton);var slider=settings.sensitivitySlider;Skin(slider.transform.Find("Track"),"track",true);Skin(slider.handleRect,"knob");
            var completion=hud.GetComponent<LabCompletionPanel>();var completionCard=safe.Find("LabCompletion/Card");Panel(completionCard);BackTitle(completionCard.Find("Title").GetComponent<UnityEngine.UI.Text>());
            Panel(supply.title.transform.parent);BackTitle(supply.title);CloseButton(supply.closeButton);
            // Existing offers retain their cash/ad/store functions, now with distinct artwork above the labels.
            var supplyCard=(RectTransform)supply.title.transform.parent;supplyCard.sizeDelta=new Vector2(740,450);supply.title.rectTransform.anchoredPosition=new Vector2(0,185);supply.closeButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(330,185);BackTitle(supply.title);
            supply.description.rectTransform.anchoredPosition=new Vector2(0,133);supply.owned.rectTransform.anchoredPosition=new Vector2(0,91);
            int option=0;foreach(var b in new[]{supply.cashButton,supply.adButton,supply.storeButton}){Pos((RectTransform)b.transform,Vector2.one*.5f,new Vector2((option-1)*225,-10),new Vector2(210,132));StyleButton(b,"blue");Icon(b.transform,option==0?"cash":option==1?"video":"store",50,new Vector2(0,35));var label=b.GetComponentInChildren<UnityEngine.UI.Text>();Pos(label.rectTransform,Vector2.one*.5f,new Vector2(0,-28),new Vector2(184,55));option++;}
            supply.useButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-125);StyleButton(supply.useButton,"green");supply.status.rectTransform.anchoredPosition=new Vector2(0,-184);
            var services=hud.GetComponent<ServicesPanel>();Panel(services.root.transform);BackTitle(services.root.transform.Find("Title").GetComponent<UnityEngine.UI.Text>());
            var tutorial=hud.GetComponent<GuidedTutorial>();if(tutorial&&tutorial.bubble){Skin(tutorial.bubble,"doctor");Pos(tutorial.bubble,new Vector2(1,1),new Vector2(-254,-185),new Vector2(440,180));Pos(tutorial.message.rectTransform,Vector2.one*.5f,new Vector2(-47,-8),new Vector2(285,76));tutorial.message.resizeTextMaxSize=18;tutorial.message.resizeTextMinSize=14;Pos(tutorial.stepLabel.rectTransform,Vector2.one*.5f,new Vector2(-25,64),new Vector2(210,25));Pos((RectTransform)tutorial.skipButton.transform,Vector2.one*.5f,new Vector2(-100,-61),new Vector2(90,30));Pos((RectTransform)tutorial.gotItButton.transform,Vector2.one*.5f,new Vector2(10,-61),new Vector2(90,30));}
            BuildCollectedList(hud,safe,supply);
            BuildMenus(hud,safe,settings);
            AddMotionAndToasts(hud,safe,supply,settings);
            foreach(var text in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true)){text.font=font;text.fontStyle=FontStyle.Normal;if(!text.GetComponent<LabLocalizedLabel>())text.gameObject.AddComponent<LabLocalizedLabel>();}
            foreach(var text in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true))if(text.name=="Title"||text==progress.summary||text==hud.progress||text==rewards.milestone||text.name=="Name"||text.name=="CollectedHeader")text.font=headingFont;
            PolishLayout();
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);if(!EditorSceneManager.SaveScene(hud.gameObject.scene))throw new IOException("Gameplay UI scene save failed");
        }
        static void BuildCollectedList(SortingHUD hud,RectTransform safe,SupplyPanel supply)
        {
            supply.designerQuickUse=true;
            var list=Rect("DesignerCollected",safe,new Vector2(278,294));Pos(list,new Vector2(0,1),new Vector2(157,-252),new Vector2(278,294));Panel(list);hud.inventoryPanel=list.gameObject;
            hud.inventorySummary=Label("CollectedHeader",list,"Collected Items 0 / 10",new Vector2(-15,118),new Vector2(224,34),18);
            var plus=Button("CapacityPlus",list,"",new Vector2(112,118),Vector2.one*42,supply.OpenSlots);plus.targetGraphic=Skin(plus.transform,"plus",false,true);
            var view=Rect("Viewport",list,new Vector2(234,220));view.anchoredPosition=new Vector2(-5,-16);var maskImage=Ensure<UnityEngine.UI.Image>(view);maskImage.color=Color.white;maskImage.raycastTarget=true;var mask=Ensure<UnityEngine.UI.Mask>(view);mask.showMaskGraphic=false;
            var content=Rect("Content",view,Vector2.zero);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
            var layout=Ensure<UnityEngine.UI.VerticalLayoutGroup>(content);layout.spacing=5;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            var fitter=Ensure<UnityEngine.UI.ContentSizeFitter>(content);fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;fitter.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            var scroll=Ensure<UnityEngine.UI.ScrollRect>(list);scroll.viewport=view;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;scroll.inertia=true;scroll.decelerationRate=.1f;
            int original=hud.inventoryButtons.Length;Array.Resize(ref hud.inventoryButtons,30);Array.Resize(ref hud.inventoryLabels,30);Array.Resize(ref hud.inventoryIcons,30);
            for(int i=0;i<30;i++){if(i>=original){var go=UnityEngine.Object.Instantiate(hud.inventoryButtons[0].gameObject,content);go.name="Slot"+i;hud.inventoryButtons[i]=go.GetComponent<UnityEngine.UI.Button>();hud.inventoryLabels[i]=go.GetComponentInChildren<UnityEngine.UI.Text>(true);hud.inventoryIcons[i]=go.transform.Find("ItemIcon").GetComponent<UnityEngine.UI.Image>();}var b=hud.inventoryButtons[i];b.transform.SetParent(content,false);var element=Ensure<UnityEngine.UI.LayoutElement>(b);element.preferredHeight=42;element.minHeight=42;StyleButton(b,"cream");Pos(hud.inventoryIcons[i].rectTransform,new Vector2(0,.5f),new Vector2(25,0),Vector2.one*36);Pos(hud.inventoryLabels[i].rectTransform,Vector2.one*.5f,new Vector2(24,0),new Vector2(170,38));hud.inventoryLabels[i].color=new Color(.04f,.07f,.13f);hud.inventoryLabels[i].resizeTextMaxSize=17;hud.inventoryLabels[i].resizeTextMinSize=14;hud.inventoryLabels[i].alignment=TextAnchor.MiddleLeft;}
            var track=Rect("Scrollbar",list,new Vector2(12,216));track.anchoredPosition=new Vector2(124,-16);Skin(track,"frame",true);var handle=Rect("Handle",track,Vector2.zero);handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;var bar=Ensure<UnityEngine.UI.Scrollbar>(track);bar.handleRect=handle;bar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;bar.targetGraphic=Skin(handle,"gold",true,true);scroll.verticalScrollbar=bar;
            var economy=hud.GetComponent<EconomyHUD>();foreach(var b in new[]{economy.searchButton,economy.magnetButton}){var offer=Button("OffersPlus",b.transform,"",new Vector2(26,26),Vector2.one*42,b==economy.searchButton?(UnityEngine.Events.UnityAction)supply.SearchOffers:supply.MagnetOffers);offer.targetGraphic=Skin(offer.transform,"plus",false,true);}
        }
        static void AddMotionAndToasts(SortingHUD hud,RectTransform safe,SupplyPanel supply,SettingsTutorialPanel settings)
        {
            var menus=hud.GetComponent<DesignerMenus>();foreach(var root in new[]{menus.pauseRoot,menus.storeRoot,supply.root,settings.root}){var motion=Ensure<DesignerPanelMotion>(root);motion.card=(RectTransform)root.transform.Find("Card");Ensure<CanvasGroup>(root);}
            var feedback=Ensure<DesignerToasts>(hud);feedback.hud=hud;var toast=Rect("DesignerToast",safe,new Vector2(600,42));Pos(toast,new Vector2(.5f,0),new Vector2(0,30),new Vector2(600,42));Skin(toast,"title",true);hud.status.transform.SetParent(toast,false);Pos(hud.status.rectTransform,Vector2.one*.5f,Vector2.zero,new Vector2(568,32));feedback.toast=Ensure<CanvasGroup>(toast);feedback.toast.blocksRaycasts=false;
            var ad=Rect("DesignerAdPrompt",safe,new Vector2(520,62));Pos(ad,new Vector2(.5f,1),new Vector2(0,-180),new Vector2(520,62));Skin(ad,"blue",true);feedback.adPrompt=ad.gameObject;feedback.adText=Label("Countdown",ad,"",Vector2.zero,new Vector2(485,44),25);ad.gameObject.SetActive(false);
        }
        static GameObject Modal(string name,Transform safe,Vector2 size,out RectTransform card)
        {var r=Rect(name,safe,Vector2.zero);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;var dim=r.GetComponent<UnityEngine.UI.Image>();if(!dim)dim=r.gameObject.AddComponent<UnityEngine.UI.Image>();dim.color=new Color(0,.015f,.06f,.78f);dim.raycastTarget=true;card=Rect("Card",r,size);Panel(card);return r.gameObject;}
        static void BuildMenus(SortingHUD hud,Transform safe,SettingsTutorialPanel settings)
        {
            var menus=hud.GetComponent<DesignerMenus>();if(!menus)menus=hud.gameObject.AddComponent<DesignerMenus>();menus.session=hud.session;menus.settings=settings;
            menus.pauseRoot=Modal("DesignerPause",safe,new Vector2(420,490),out var card);var title=Label("Title",card,"PAUSE",new Vector2(0,200),new Vector2(280,44),28);BackTitle(title);
            Button("Resume",card,"RESUME",new Vector2(0,112),new Vector2(300,58),menus.Close,"green");Button("Save",card,"SAVE",new Vector2(0,40),new Vector2(300,58),menus.Save);Button("Settings",card,"SETTINGS",new Vector2(0,-32),new Vector2(300,58),menus.Settings);Button("Store",card,"STORE",new Vector2(0,-104),new Vector2(300,58),menus.StoreFromPause);menus.saveStatus=Label("Status",card,"",new Vector2(0,-185),new Vector2(340,36),18);menus.pauseRoot.SetActive(false);
            Wire(settings.openButton,menus.OpenPause);var glyph=settings.openButton.transform.Find("DesignerIcon");if(glyph)glyph.gameObject.SetActive(false);var pauseLabel=settings.openButton.GetComponentInChildren<UnityEngine.UI.Text>();pauseLabel.text="PAUSE";for(int i=0;i<2;i++){var bar=Rect("PauseBar"+i,settings.openButton.transform,new Vector2(8,28));bar.anchoredPosition=new Vector2(i==0?-7:7,0);Skin(bar,"pausebar");}
            var store=Button("DesignerStoreButton",safe,"STORE",Vector2.zero,Vector2.one*56,menus.OpenStore);Pos((RectTransform)store.transform,new Vector2(1,1),new Vector2(-288,-42),Vector2.one*56);RoundButton(store,"store");var label=store.GetComponentInChildren<UnityEngine.UI.Text>();Pos(label.rectTransform,Vector2.one*.5f,new Vector2(0,-38),new Vector2(90,20));label.fontSize=13;label.resizeTextMaxSize=13;
            menus.storeRoot=Modal("DesignerStore",safe,new Vector2(1020,530),out card);BackTitle(Label("Title",card,"STORE",new Vector2(0,217),new Vector2(310,44),28));menus.storeClose=Button("Close",card,"",new Vector2(465,216),Vector2.one*48,menus.Close);CloseButton(menus.storeClose);
            // Reuse cards moved into the polished scroll view on earlier authoring passes.
            var existingContent=card.Find("ProductsViewport/Content");if(existingContent)for(int p=0;p<6;p++){var existing=existingContent.Find("Product"+p);if(existing)existing.SetParent(card,false);}
            menus.prices=new UnityEngine.UI.Text[6];menus.purchases=new UnityEngine.UI.Button[6];string[] titles={"REMOVE ADS","CASH","BIG CASH","MAGNET","SEARCH","CAPACITY"},icons={"noads","cash","cashLarge","magnet","search","slots"};var config=UnityEngine.Object.FindFirstObjectByType<SupplyCommerce>().settings;
            string[] amounts={"No forced ads","+"+config.smallCashAmount+" cash","+"+config.largeCashAmount+" cash","+"+config.magnetPackAmount+" uses","+"+config.searchPackAmount+" uses","+"+config.slotPackAmount+" slots"};
            for(int i=0;i<6;i++){int index=i;var tile=Rect("Product"+i,card,new Vector2(148,318));tile.anchoredPosition=new Vector2((i-2.5f)*160,10);Panel(tile);Label("Name",tile,titles[i],new Vector2(0,129),new Vector2(135,35),18);Icon(tile,icons[i],100,new Vector2(0,53));Label("Amount",tile,amounts[i],new Vector2(0,-28),new Vector2(132,50),17);menus.purchases[i]=Button("Buy",tile,"Unavailable",new Vector2(0,-108),new Vector2(130,60),null,"gold"); // Persistent integer events survive scene reload.
                while(menus.purchases[i].onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(menus.purchases[i].onClick,0);UnityEventTools.AddIntPersistentListener(menus.purchases[i].onClick,menus.Purchase,index);menus.prices[i]=menus.purchases[i].GetComponentInChildren<UnityEngine.UI.Text>();}
            menus.storeStatus=Label("Status",card,"",new Vector2(0,-210),new Vector2(880,45),18);menus.storeRoot.SetActive(false);
        }
        public static void ApplyLoading()
        {
            Load();var startup=UnityEngine.Object.FindFirstObjectByType<LabStartup>();if(!startup)throw new Exception("Boot scene required");var canvas=startup.loadingLabel.GetComponentInParent<Canvas>();var root=canvas.transform;
            var bg=Rect("DesignerLoadingBackground",root,Vector2.zero);bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.offsetMin=bg.offsetMax=Vector2.zero;Skin(bg,"background");var fit=bg.GetComponent<UnityEngine.UI.AspectRatioFitter>();if(!fit)fit=bg.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();fit.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=sprites["background"].rect.width/sprites["background"].rect.height;bg.SetAsFirstSibling();var oldBackground=root.Find("Background");if(oldBackground)oldBackground.SetAsFirstSibling();
            var logo=Rect("DesignerLoadingLogo",root,new Vector2(500,340));logo.anchoredPosition=new Vector2(0,70);Skin(logo,"logo");
            Pos(startup.loadingLabel.rectTransform,Vector2.one*.5f,new Vector2(0,-145),new Vector2(600,40));startup.loadingLabel.color=Color.white;startup.loadingLabel.font=font;startup.loadingLabel.fontStyle=FontStyle.Normal;
            var track=Rect("DesignerLoadingTrack",root,new Vector2(440,24));track.anchoredPosition=new Vector2(0,-190);Skin(track,"track",true);var fill=Rect("Fill",track,new Vector2(432,16));startup.loadingFill=Skin(fill,"fill",true);fill.anchorMin=Vector2.zero;fill.anchorMax=new Vector2(0,1);fill.offsetMin=new Vector2(4,4);fill.offsetMax=new Vector2(-4,-4);startup.loadingFill.enabled=false;
            PolishCurrentAspect();
        }
    }
}
