using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    // UI polish requested 2026-11-18:
    //  - SEARCH / MAGNET owned uses in a gold (owned) / silver (empty) knob badge, captions on navy plates
    //    (white captions measured 1.7-2.7:1 over the lab floor); same plates for PAUSE / ITEMS / STORE.
    //  - Settings rebuilt on the designer's reference (Glossy Blue Settings Panel): MUSIC / SOUND / HAPTICS
    //    switches, LANGUAGE dropdown (locked to English with "MORE SOON"), red RESET, underlined links, version.
    //  - Center toast sized to its message with an alert badge; smaller welcome-back toast.
    //  - SORTED plate gets a progress fill; the pause menu drops the redundant SAVE (progress autosaves).
    // Only adds or re-lays out objects; every existing button keeps its serialized onClick.
    public static class UIPolishUpgrade
    {
        const string ScenePath="Assets/Scenes/Lab_Sorting.unity";
        const string UIRoot="Assets/_LabSorting/Art/UI/";
        const string Game=UIRoot+"Lab sorting game mockup and sprites 1/Gameplay/";
        const string SettingsArt=UIRoot+"Store and settings panel sprite/Settings/";
        static readonly Color Navy=new Color32(0x10,0x22,0x4A,255),SoftBlue=new Color32(0x9F,0xB7,0xE0,255);
        // Row centres of the six bands baked into the settings panel art (content space).
        static readonly float[] Band={143,74,4,-65,-134,-203};
        static Font bold,rounded;

        static Sprite LabUI(string file)=>AssetDatabase.LoadAllAssetsAtPath(Game+file).OfType<Sprite>().First();
        // Designer settings pieces; imported as single sprites (sliced horizontally when border > 0).
        static Sprite Piece(string file,float border=0)
        {
            string path=SettingsArt+file;var existing=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if(existing&&(border<=0||existing.border.x>0))return existing;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.wrapMode=TextureWrapMode.Clamp;
            importer.spriteBorder=new Vector4(border,0,border,0);importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }
        // Plain white rounded rectangle (radius 10 of 32px, 9-sliced) tinted per use; no rim for text to collide with.
        const string ChipPath=UIRoot+"Generated/LabUI_chip.png";
        const float ChipRadius=10;
        static Sprite Chip()=>Rounded(ChipPath,32,ChipRadius,12);
        // White circle (64px, 9-sliced at 32) for round badges and pill-shaped ribbons.
        const string DotPath=UIRoot+"Generated/LabUI_dot.png";
        static Sprite Dot()=>Rounded(DotPath,64,32,32);
        static Sprite Rounded(string path,int size,float radius,float border)
        {
            var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();if(sprite)return sprite;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(radius-(x+.5f),0,(x+.5f)-(size-radius)),dy=Mathf.Max(radius-(y+.5f),0,(y+.5f)-(size-radius));
                texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(radius-Mathf.Sqrt(dx*dx+dy*dy)+.5f)));
            }
            System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.spriteBorder=Vector4.one*border;importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }
        // A sliced Dot at this height draws a perfect circle (square rect) or pill (wide rect).
        static void Pill(Image image,float height){image.sprite=Dot();image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=32f/(height*.5f);}
        static RectTransform Child(Transform parent,string name)
        {
            var t=parent.Find(name);
            if(!t){var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer;t=go.transform;t.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"UI polish");}
            return (RectTransform)t;
        }
        static T Ensure<T>(Component c) where T:Component{var x=c.GetComponent<T>();return x?x:c.gameObject.AddComponent<T>();}
        static void Place(RectTransform r,Vector2 anchor,Vector2 position,Vector2 size){r.anchorMin=r.anchorMax=anchor;r.pivot=Vector2.one*.5f;r.anchoredPosition=position;r.sizeDelta=size;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;}
        static void Fill(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=Vector2.one*.5f;r.offsetMin=min;r.offsetMax=max;r.localScale=Vector3.one;}
        static Image Art(RectTransform r,Sprite sprite,float slicedHeight=0)
        {
            var image=Ensure<Image>(r);image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;
            image.type=slicedHeight>0?Image.Type.Sliced:Image.Type.Simple;image.preserveAspect=slicedHeight<=0&&sprite;
            if(slicedHeight>0)image.pixelsPerUnitMultiplier=sprite.rect.height/slicedHeight;
            return image;
        }
        static Text Label(RectTransform r,string value,Font font,int size,Color color,TextAnchor anchor)
        {
            var t=Ensure<Text>(r);t.font=font;t.text=value;t.fontSize=size;t.fontStyle=FontStyle.Normal;t.color=color;t.alignment=anchor;t.raycastTarget=false;t.alignByGeometry=true;
            t.resizeTextForBestFit=true;t.resizeTextMinSize=Mathf.Min(11,size);t.resizeTextMaxSize=size;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;
            return t;
        }
        static void Rewire(Button b,UnityEngine.Events.UnityAction call){while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddPersistentListener(b.onClick,call);}
        static void Dirty(params Object[] objects){foreach(var o in objects)if(o)EditorUtility.SetDirty(o);}

        [MenuItem("Tools/Lab Sorting/Designer/UI Polish (Badges, Settings Reference, Toasts)")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Stop play mode first");
            var hud=Object.FindFirstObjectByType<SortingHUD>();if(!hud||hud.gameObject.scene.path!=ScenePath)throw new System.Exception("Open "+ScenePath+" first");
            bold=AssetDatabase.LoadAssetAtPath<Font>(UIRoot+"onlygraphic/Onlygraphic Bold.otf");rounded=AssetDatabase.LoadAssetAtPath<Font>(UIRoot+"onlygraphic/Onlygraphic Rounded.otf");
            if(!bold||!rounded)throw new System.Exception("Onlygraphic fonts missing");
            var safe=hud.transform.Find("SafeArea");var fill=LabUI("glossy_blue_and_gold_game_ui_icons_19.png");
            var gold=Piece("Layer 7.png");var silver=Piece("Layer 6.png");
            var chip=Chip();
            Badges(hud,gold,silver);Captions(hud,safe,chip);Toasts(hud,chip);Progress(hud,safe,fill);Pause(hud);Settings(hud,gold);
            Store(hud,chip);Offers(hud,chip);Juice(hud,safe);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);AssetDatabase.SaveAssets();
        }

        static void Badges(SortingHUD hud,Sprite gold,Sprite silver)
        {
            var eco=hud.GetComponent<EconomyHUD>();eco.badgeOwned=gold;eco.badgeEmpty=silver;
            foreach(var search in new[]{true,false})
            {
                var button=search?eco.searchButton:eco.magnetButton;float size=((RectTransform)button.transform).rect.width;
                // Top-left: the "+" offers button already owns the top-right corner.
                var badge=Child(button.transform,"CountBadge");Place(badge,Vector2.one*.5f,new Vector2(-size*.37f,size*.37f),Vector2.one*30);badge.SetAsLastSibling();
                var image=Art(badge,gold);
                var count=Child(badge,"Count");Fill(count,new Vector2(2,1),new Vector2(-2,-1));var text=Label(count,"0",bold,18,Navy,TextAnchor.MiddleCenter);text.resizeTextMinSize=10;
                if(search){eco.searchBadge=image;eco.searchCount=text;}else{eco.magnetBadge=image;eco.magnetCount=text;}
                Dirty(badge,image,count,text);
            }
            Dirty(eco);
        }

        static void Captions(SortingHUD hud,Transform safe,Sprite chip)
        {
            var eco=hud.GetComponent<EconomyHUD>();var settings=hud.GetComponent<SettingsTutorialPanel>();var progress=hud.GetComponent<ItemsProgressPanel>();var store=safe.Find("DesignerStoreButton");
            var actions=hud.GetComponent<ContextActionHUD>();
            var labels=new[]{eco.searchLabel,eco.magnetLabel,settings.openButton.GetComponentInChildren<Text>(true),progress.openButton.GetComponentInChildren<Text>(true),store?store.GetComponentInChildren<Text>(true):null,
                actions.pick.GetComponentInChildren<Text>(true),actions.place.GetComponentInChildren<Text>(true),actions.drop.GetComponentInChildren<Text>(true)};
            foreach(var label in labels)
            {
                if(!label)continue;
                // Flat navy chip (no gold rim) a fixed 3px below the button, fixed 13pt bold so text never outgrows it.
                var button=(RectTransform)label.transform.parent;var target=label.rectTransform;
                target.anchorMin=target.anchorMax=target.pivot=Vector2.one*.5f;target.anchoredPosition=new Vector2(0,-(button.rect.height*.5f+3+10));target.sizeDelta=new Vector2(Mathf.Max(target.sizeDelta.x,90),20);
                label.font=bold;label.fontSize=13;label.fontStyle=FontStyle.Normal;label.resizeTextForBestFit=false;label.alignment=TextAnchor.MiddleCenter;label.alignByGeometry=true;
                label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;label.color=Color.white;
                var r=Child(button,"CaptionPlate");
                if(r.GetSiblingIndex()>label.transform.GetSiblingIndex())r.SetSiblingIndex(label.transform.GetSiblingIndex());
                r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=target.anchoredPosition;r.localScale=Vector3.one;
                r.sizeDelta=new Vector2(Mathf.Max(40,TextFit.Width(label,label.text,13)+16),20);
                var image=Ensure<Image>(r);image.sprite=chip;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=ChipRadius/6f;image.preserveAspect=false;image.raycastTarget=false;image.color=new Color32(0x0A,0x1F,0x4F,217);
                var caption=Ensure<CaptionPlate>(r);caption.label=label;caption.padding=16;caption.minWidth=40;caption.height=20;
                Dirty(r,image,caption,label,target);
            }
        }

        static readonly Color SuccessGreen=new Color32(0x2E,0xB8,0x4F,255),AlertOrange=new Color32(0xFF,0x7A,0x45,255);
        // Round tone badge (white chip sprite tinted) at the left edge of a toast card.
        static GameObject ToneBadge(RectTransform card,string name,Sprite chip,Color color,string mark,Color markColor,float x)
        {
            var badge=Child(card,name);Place(badge,new Vector2(0,.5f),new Vector2(x,0),Vector2.one*24);var image=Ensure<Image>(badge);Pill(image,24);image.color=color;image.raycastTarget=false;
            var glyph=Child(badge,"Mark");Fill(glyph,Vector2.zero,Vector2.zero);Label(glyph,mark,bold,16,markColor,TextAnchor.MiddleCenter);
            badge.gameObject.SetActive(false);Dirty(badge,image,glyph);return badge.gameObject;
        }
        static void Toasts(SortingHUD hud,Sprite chip)
        {
            var toasts=hud.GetComponent<DesignerToasts>();var card=(RectTransform)toasts.toast.transform;
            toasts.minWidth=200;toasts.maxWidth=440;toasts.padding=48;
            toasts.alertBadge=ToneBadge(card,"AlertBadge",chip,AlertOrange,"!",new Color32(0x3A,0x14,0x00,255),22);
            toasts.successBadge=ToneBadge(card,"SuccessBadge",chip,SuccessGreen,"✓",Color.white,22);Dirty(toasts);
            // Welcome-back / purchase toast: 700x65 at 22pt read as a banner; width now follows the text up to 460.
            var service=Object.FindFirstObjectByType<ServicePresentation>();var serviceCard=(RectTransform)service.toastText.transform.parent;var art=serviceCard.GetComponent<Image>();
            if(art&&art.type==Image.Type.Sliced&&art.sprite)art.pixelsPerUnitMultiplier=art.sprite.rect.height/58f;
            serviceCard.sizeDelta=new Vector2(460,58);service.toastText.rectTransform.sizeDelta=new Vector2(424,48);service.toastText.resizeTextMaxSize=19;service.toastText.resizeTextMinSize=14;
            service.toastMinWidth=260;service.toastMaxWidth=460;service.toastPadding=56;
            service.toastSuccessBadge=ToneBadge(serviceCard,"SuccessBadge",chip,SuccessGreen,"✓",Color.white,26);
            service.toastAlertBadge=ToneBadge(serviceCard,"AlertBadge",chip,AlertOrange,"!",new Color32(0x3A,0x14,0x00,255),26);
            Dirty(service,serviceCard,art,service.toastText);
        }

        // Store cards: one gold buy button showing the price, big gold amount over a small unit, BEST VALUE on the
        // large cash pack, arrows at the row ends, and no permanent status line (results reach the toast).
        static void Store(SortingHUD hud,Sprite chip)
        {
            var m=hud.GetComponent<DesignerMenus>();var card=(RectTransform)m.storeRoot.transform.Find("Card");
            var values=new Text[m.purchases.Length];var units=new Text[m.purchases.Length];
            for(int i=0;i<m.purchases.Length;i++)
            {
                var button=m.purchases[i];var tile=(RectTransform)button.transform.parent;
                var badge=tile.Find("PriceBadge");if(badge){badge.gameObject.SetActive(false);Dirty(badge.gameObject);}
                var purchaseLabel=button.transform.Find("PurchaseLabel");if(purchaseLabel){purchaseLabel.gameObject.SetActive(false);Dirty(purchaseLabel.gameObject);}
                Place((RectTransform)button.transform,Vector2.one*.5f,new Vector2(0,-128),new Vector2(184,46));
                var price=m.prices[i];price.transform.SetParent(button.transform,false);Fill(price.rectTransform,new Vector2(12,4),new Vector2(-12,-4));
                // StoreTouchChecks: long localized prices ("USD 123,456,789.99") must still fit without going under 14pt.
                Label(price.rectTransform,price.text,bold,21,new Color(.02f,.06f,.18f),TextAnchor.MiddleCenter).resizeTextMinSize=14;
                Place((RectTransform)tile.Find("DesignerIcon"),Vector2.one*.5f,new Vector2(0,46),Vector2.one*116);
                var unit=tile.Find("Amount").GetComponent<Text>();units[i]=unit;
                var value=Child(tile,"AmountValue");values[i]=Label(value,"",bold,30,new Color32(0xFF,0xC2,0x1A,255),TextAnchor.MiddleCenter);values[i].resizeTextMinSize=18;
                var outline=Ensure<UnityEngine.UI.Outline>(values[i]);outline.effectColor=new Color(.02f,.08f,.25f,.9f);outline.effectDistance=new Vector2(1.5f,-1.5f);
                bool hasNumber=i>0;Place(value,Vector2.one*.5f,new Vector2(0,-42),new Vector2(182,38));
                Place(unit.rectTransform,Vector2.one*.5f,new Vector2(0,hasNumber?-74:-52),new Vector2(182,hasNumber?22:30));Label(unit.rectTransform,unit.text,rounded,hasNumber?15:17,Color.white,TextAnchor.MiddleCenter);
                Dirty(button,tile,price,unit,value,values[i],outline);
            }
            m.amountValues=values;m.amountUnits=units;
            // BEST VALUE between the title and the icon of the large cash pack.
            var big=(RectTransform)m.purchases[2].transform.parent;var ribbon=Child(big,"BestValue");Place(ribbon,Vector2.one*.5f,new Vector2(0,118),new Vector2(120,24));
            var ribbonArt=Ensure<Image>(ribbon);Pill(ribbonArt,24);ribbonArt.color=new Color32(0xE8,0x45,0x2C,255);ribbonArt.raycastTarget=false;
            var ribbonText=Child(ribbon,"Label");Fill(ribbonText,new Vector2(6,0),new Vector2(-6,0));Label(ribbonText,"BEST VALUE",bold,14,Color.white,TextAnchor.MiddleCenter);
            // Arrows at the row ends (shown only when more cards lie that way).
            var viewport=(RectTransform)card.Find("ProductsViewport");var scroll=viewport.GetComponent<ScrollRect>();var arrows=Ensure<StoreScrollArrows>(card);arrows.scroll=scroll;arrows.step=232;
            foreach(var (name,file,direction) in new[]{("ScrollLeft","glossy_3d_medical_ui_icon_collection_03.png",-1),("ScrollRight","glossy_3d_medical_ui_icon_collection_04.png",1)})
            {
                var r=Child(card,name);Place(r,Vector2.one*.5f,new Vector2(direction*(viewport.sizeDelta.x*.5f+26),viewport.anchoredPosition.y),Vector2.one*46);r.SetAsLastSibling();
                var image=Art(r,LabUI(file));image.raycastTarget=true;var button=Ensure<Button>(r);button.targetGraphic=image;
                Rewire(button,direction<0?(UnityEngine.Events.UnityAction)arrows.Left:arrows.Right);
                if(direction<0)arrows.left=r.gameObject;else arrows.right=r.gameObject;Dirty(r,image,button);
            }
            m.storeStatus.text="";m.storeStatus.gameObject.SetActive(false);
            Dirty(m,ribbon,ribbonArt,ribbonText,arrows,m.storeStatus.gameObject);
        }

        // Offer panel: its status line now only shows work in progress, on the same navy chip as the HUD captions.
        static void Offers(SortingHUD hud,Sprite chip)
        {
            var supply=hud.GetComponent<SupplyPanel>();var label=supply.status;var target=label.rectTransform;
            if(!Mathf.Approximately(target.anchorMin.x,target.anchorMax.x))return;
            label.text="";label.alignment=TextAnchor.MiddleCenter;label.horizontalOverflow=HorizontalWrapMode.Overflow;label.resizeTextForBestFit=false;label.fontSize=15;label.color=Color.white;target.sizeDelta=new Vector2(target.sizeDelta.x,24);
            var r=Child(label.transform.parent,"StatusChip");if(r.GetSiblingIndex()>label.transform.GetSiblingIndex())r.SetSiblingIndex(label.transform.GetSiblingIndex());
            r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=target.anchoredPosition;r.sizeDelta=new Vector2(100,24);r.localScale=Vector3.one;
            var image=Ensure<Image>(r);image.sprite=chip;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=ChipRadius/7f;image.color=new Color32(0x0A,0x1F,0x4F,217);image.raycastTarget=false;
            var plate=Ensure<CaptionPlate>(r);plate.label=label;plate.padding=24;plate.minWidth=60;plate.height=24;Dirty(supply,label,target,r,image,plate);
        }

        // Cash gains: count-up (EconomyHUD), coins flying into the wallet, wallet bump, light haptic.
        static void Juice(SortingHUD hud,Transform safe)
        {
            var eco=hud.GetComponent<EconomyHUD>();var juice=Ensure<WalletJuice>(hud);juice.session=eco.session;juice.feedback=Object.FindFirstObjectByType<LabSorting.Sorting.SortingFeedback>();
            juice.wallet=(RectTransform)eco.wallet.transform.parent;
            var layer=Child(safe,"WalletCoins");layer.anchorMin=Vector2.zero;layer.anchorMax=Vector2.one;layer.pivot=Vector2.one*.5f;layer.offsetMin=layer.offsetMax=Vector2.zero;layer.localScale=Vector3.one;
            juice.layer=layer;juice.coin=AssetDatabase.LoadAllAssetsAtPath(UIRoot+"Store and settings panel sprite/Store/Green_cash_bundle_game_icon_21bc9553.png").OfType<Sprite>().First();
            Dirty(juice,layer);
        }

        static void Progress(SortingHUD hud,Transform safe,Sprite fill)
        {
            var plate=(RectTransform)safe.Find("DesignerTitle_Progress");
            // The longest label ("SORTED  4042 / 4042   100%") needs 245 at 18pt; 405 left a long empty bar.
            plate.sizeDelta=new Vector2(320,plate.sizeDelta.y);var title=hud.progress.rectTransform;if(Mathf.Approximately(title.anchorMin.x,title.anchorMax.x))title.sizeDelta=new Vector2(280,title.sizeDelta.y);Dirty(plate,title);
            var track=Child(plate,"ProgressTrack");track.anchorMin=Vector2.zero;track.anchorMax=new Vector2(1,0);track.pivot=new Vector2(.5f,0);track.anchoredPosition=new Vector2(0,8);track.sizeDelta=new Vector2(-52,6);track.localScale=Vector3.one;
            var groove=Art(track,fill,6);groove.color=new Color(.02f,.06f,.18f,.75f);
            var bar=Child(track,"ProgressFill");bar.anchorMin=Vector2.zero;bar.anchorMax=new Vector2(0,1);bar.pivot=new Vector2(0,.5f);bar.offsetMin=bar.offsetMax=Vector2.zero;bar.localScale=Vector3.one;
            Art(bar,fill,6);hud.progressFill=bar;bar.gameObject.SetActive(false);Dirty(hud,track,groove,bar);
        }

        static void Pause(SortingHUD hud)
        {
            var card=hud.GetComponent<DesignerMenus>().pauseRoot.transform.Find("Card");
            var save=card.Find("Save");if(save){save.gameObject.SetActive(false);Dirty(save.gameObject);}
            foreach(var (name,y) in new[]{("Resume",55f),("Settings",-28f),("Store",-111f)}){var r=(RectTransform)card.Find(name);if(!r)continue;r.anchoredPosition=new Vector2(r.anchoredPosition.x,y);Dirty(r);}
        }

        static void Settings(SortingHUD hud,Sprite gold)
        {
            var p=hud.GetComponent<SettingsTutorialPanel>();var content=p.settingsContent.transform;
            var toggle=Piece("Layer 2.png");var red=Piece("Rectangle 1 copy 2.png",33);var cream=Piece("Rectangle 1 copy 3.png",30);var chipArt=Piece("Rectangle 1 copy.png",33);var arrow=Piece("Layer 9.png");
            var labelFont=p.sensitivityLabel.font?p.sensitivityLabel.font:rounded;
            // Band 1: MUSIC / SOUND / HAPTICS switches.
            var musicT=content.Find("Music");var music=musicT?musicT.GetComponent<Button>():null;
            if(!music){music=Object.Instantiate(p.soundButton,content);music.name="Music";Undo.RegisterCreatedObjectUndo(music.gameObject,"UI polish");}
            Rewire(music,p.ToggleMusic);p.musicButton=music;p.soundscape=Object.FindFirstObjectByType<LabSoundscape>();
            // 226-wide cells line the first label and last switch up with the slider rows' edges.
            var cells=new[]{(music,"MUSIC",-228f),(p.soundButton,"SOUND",0f),(p.hapticsButton,"HAPTICS",228f)};
            foreach(var (button,name,x) in cells)
            {
                var r=(RectTransform)button.transform;Place(r,Vector2.one*.5f,new Vector2(x,Band[0]),new Vector2(226,44));
                var hit=button.GetComponent<Image>();hit.sprite=null;hit.type=Image.Type.Simple;hit.color=new Color(1,1,1,0);hit.raycastTarget=true;
                var label=button.GetComponentsInChildren<Text>(true).First(t=>t.transform.parent==button.transform);Fill(label.rectTransform,new Vector2(6,0),new Vector2(-104,0));
                Label(label.rectTransform,name,labelFont,21,Color.white,TextAnchor.MiddleLeft);
                var sw=Child(button.transform,"Switch");Place(sw,new Vector2(1,.5f),new Vector2(-50,0),new Vector2(92,40));
                var track=Child(sw,"Track");Place(track,Vector2.one*.5f,Vector2.zero,new Vector2(92,40));Art(track,toggle);
                // Off art has the silver knob baked at (105, 94.5) of 470x204; mirrored, the gold knob covers it.
                var knob=Child(sw,"OnKnob");Place(knob,Vector2.one*.5f,new Vector2(25.5f,1.5f),Vector2.one*33);Art(knob,gold);
                var state=Ensure<SettingsSwitch>(sw);state.track=track;state.onKnob=knob.gameObject;
                if(button==music)p.musicSwitch=state;else if(button==p.soundButton)p.soundSwitch=state;else p.hapticsSwitch=state;
                Dirty(r,hit,label,sw,track,knob,state,button);
            }
            // Bands 2-3: the two sliders keep their art, re-centred on the panel's bands.
            foreach(var (label,slider,y) in new[]{(p.sensitivityLabel,p.sensitivitySlider,Band[1]),(p.moveSpeedLabel,p.moveSpeedSlider,Band[2])})
            {var a=label.rectTransform;a.anchoredPosition=new Vector2(a.anchoredPosition.x,y);var b=(RectTransform)slider.transform;b.anchoredPosition=new Vector2(b.anchoredPosition.x,y);Dirty(a,b);}
            Language(p,content,labelFont,cream,chipArt,arrow);
            // Band 5: HOW TO PLAY, plus PRIVACY CHOICES only where Google's consent SDK requires it (SettingsHelpRow).
            // REPLAY TUTORIAL and STORE & PRIVACY were removed on request; they stay in the scene, hidden, with their wiring.
            var services=hud.GetComponent<ServicesPanel>();
            foreach(var name in new[]{"ReplayTutorial","Services"}){var hidden=content.Find(name);if(hidden){hidden.gameObject.SetActive(false);Dirty(hidden.gameObject);}}
            var how=(RectTransform)content.Find("HowToPlay");Place(how,Vector2.one*.5f,new Vector2(0,Band[4]),new Vector2(300,44));
            var howText=how.GetComponentInChildren<Text>(true);howText.resizeTextForBestFit=true;howText.resizeTextMaxSize=17;howText.resizeTextMinSize=12;
            var privacyT=content.Find("PrivacyChoices");var privacy=privacyT?privacyT.GetComponent<Button>():null;
            if(!privacy){privacy=Object.Instantiate(how.GetComponent<Button>(),content);privacy.name="PrivacyChoices";Undo.RegisterCreatedObjectUndo(privacy.gameObject,"UI polish");}
            Rewire(privacy,services.Privacy);Place((RectTransform)privacy.transform,Vector2.one*.5f,new Vector2(170,Band[4]),new Vector2(300,44));
            var privacyText=privacy.GetComponentInChildren<Text>(true);privacyText.text="PRIVACY CHOICES";privacy.gameObject.SetActive(false);
            var row=Ensure<SettingsHelpRow>(p.settingsContent.transform);row.howToPlay=how;row.privacyChoices=privacy.gameObject;row.spacing=170;
            Dirty(how,howText,privacy,privacyText,row);
            // Band 6: underlined links either side of a red RESET.
            foreach(var (name,x) in new[]{("PrivacyPolicy",-268f),("RestorePurchases",268f)})
            {
                var r=(RectTransform)content.Find(name);Place(r,Vector2.one*.5f,new Vector2(x,Band[5]),new Vector2(220,44));
                var hit=r.GetComponent<Image>();hit.sprite=null;hit.type=Image.Type.Simple;hit.color=new Color(1,1,1,0);hit.raycastTarget=true;
                var text=r.GetComponentsInChildren<Text>(true).First(t=>t.transform.parent==r);Fill(text.rectTransform,Vector2.zero,Vector2.zero);Label(text.rectTransform,text.text,bold,18,Color.white,TextAnchor.MiddleCenter);
                var line=Child(r,"Underline");Place(line,Vector2.one*.5f,new Vector2(0,-12),new Vector2(Mathf.Min(200,TextFit.Width(text,text.text,18)),2));var ink=Ensure<Image>(line);ink.sprite=null;ink.color=new Color(1,1,1,.85f);ink.raycastTarget=false;
                Dirty(r,hit,text,line,ink);
            }
            // RESET SETTINGS removed on request (button hidden, ResetSettings() and its wiring kept).
            var reset=(RectTransform)p.resetButton.transform;Place(reset,Vector2.one*.5f,new Vector2(0,Band[5]),new Vector2(220,44));reset.gameObject.SetActive(false);
            // Version in the panel's bottom-right corner, readable (was 12pt, dim).
            var version=Child(content,"Version");Place(version,Vector2.one*.5f,new Vector2(312,-247),new Vector2(130,22));Label(version,"v"+PlayerSettings.bundleVersion,rounded,15,new Color(.86f,.9f,.97f),TextAnchor.MiddleRight);Ensure<VersionLabel>(version).developerPanel=services;
            Dirty(p,reset,reset.gameObject,version);
        }

        static void Language(SettingsTutorialPanel p,Transform content,Font labelFont,Sprite cream,Sprite chipArt,Sprite arrow)
        {
            float y=Band[3];
            var heading=Child(content,"LanguageLabel");Place(heading,Vector2.one*.5f,new Vector2(-185,y),new Vector2(310,42));Label(heading,"LANGUAGE",labelFont,21,Color.white,TextAnchor.MiddleLeft);
            var chip=Child(content,"LanguageSoon");Place(chip,Vector2.one*.5f,new Vector2(4,y),new Vector2(124,30));Art(chip,chipArt,30);
            var chipText=Child(chip,"Label");Fill(chipText,new Vector2(8,0),new Vector2(-8,0));Label(chipText,"MORE SOON",bold,14,Navy,TextAnchor.MiddleCenter);
            var existing=content.Find("Language");Dropdown dropdown=existing?existing.GetComponent<Dropdown>():null;
            if(!dropdown)
            {
                var go=DefaultControls.CreateDropdown(new DefaultControls.Resources{standard=cream,background=cream,dropdown=arrow,inputField=cream,knob=cream,checkmark=arrow,mask=cream});
                go.name="Language";go.transform.SetParent(content,false);Undo.RegisterCreatedObjectUndo(go,"UI polish");dropdown=go.GetComponent<Dropdown>();
                foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=content.gameObject.layer;
            }
            var r=(RectTransform)dropdown.transform;Place(r,Vector2.one*.5f,new Vector2(208,y),new Vector2(254,44));
            var box=Art(r,cream,44);box.raycastTarget=true;dropdown.targetGraphic=box;
            var colors=dropdown.colors;colors.normalColor=colors.disabledColor=Color.white;colors.highlightedColor=new Color(1,1,.92f);colors.pressedColor=new Color(.9f,.9f,.85f);dropdown.colors=colors;
            var nav=dropdown.navigation;nav.mode=Navigation.Mode.None;dropdown.navigation=nav;
            Fill(dropdown.captionText.rectTransform,new Vector2(18,0),new Vector2(-44,0));Label(dropdown.captionText.rectTransform,"English",bold,20,Navy,TextAnchor.MiddleLeft);
            var arrowRect=(RectTransform)r.Find("Arrow");if(arrowRect){Place(arrowRect,new Vector2(1,.5f),new Vector2(-24,0),new Vector2(20,13));Art(arrowRect,arrow);}
            if(dropdown.template){var templateArt=dropdown.template.GetComponent<Image>();if(templateArt){templateArt.sprite=cream;templateArt.type=Image.Type.Sliced;templateArt.pixelsPerUnitMultiplier=cream.rect.height/44;}foreach(var t in dropdown.template.GetComponentsInChildren<Text>(true)){t.font=bold;t.color=Navy;t.fontSize=18;}dropdown.template.gameObject.SetActive(false);}
            dropdown.ClearOptions();dropdown.AddOptions(new System.Collections.Generic.List<string>{"English"});dropdown.SetValueWithoutNotify(0);dropdown.interactable=false;
            var picker=Ensure<LanguageDropdown>(dropdown);picker.dropdown=dropdown;picker.comingSoon=chip.gameObject;
            Dirty(heading,chip,chipText,dropdown,r,box,picker,dropdown.captionText,arrowRect);
        }
    }
}
