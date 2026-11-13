using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class StoreReferenceUpgrade
    {
        const string Root="Assets/_LabSorting/Art/UI/Store and settings panel sprite/Store/";
        static Sprite Import(string file,bool panel)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Root+file);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.mipmapEnabled=false;importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=panel?2048:512;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            var f=new SpriteDataProviderFactories();f.Init();var provider=f.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var cap=provider.GetDataProvider<ISpriteFrameEditCapability>();foreach(var flag in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditSpriteName,EEditCapability.EditPivot,EEditCapability.EditBorder})if(cap==null||!cap.GetEditCapability().HasCapability(flag))throw new Exception("Sprite editing unavailable");
            var tex=new Texture2D(2,2);tex.LoadImage(System.IO.File.ReadAllBytes(Root+file));float w=panel?tex.width:500,h=tex.height;UnityEngine.Object.DestroyImmediate(tex);
            var old=provider.GetSpriteRects();var id=old.Length>0?old[0].spriteID:GUID.Generate();string name=panel?"StoreReferencePanel":"StoreReferenceProduct";
            provider.SetSpriteRects(new[]{new SpriteRect{name=name,rect=new Rect(0,0,w,h),pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,spriteID=id,border=Vector4.zero}});provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(name,id)});
            Vector2[] points=panel?new[]{new Vector2(90,85),new Vector2(650,85),new Vector2(660,45),new Vector2(690,15),new Vector2(740,0),new Vector2(1360,0),new Vector2(1410,15),new Vector2(1440,45),new Vector2(1455,85),new Vector2(w-90,85),new Vector2(w-40,100),new Vector2(w-10,140),new Vector2(w,190),new Vector2(w,h-90),new Vector2(w-10,h-40),new Vector2(w-40,h-10),new Vector2(w-90,h),new Vector2(90,h),new Vector2(40,h-10),new Vector2(10,h-40),new Vector2(0,h-90),new Vector2(0,190),new Vector2(10,140),new Vector2(40,100)}:new[]{new Vector2(60,0),new Vector2(w-60,0),new Vector2(w-25,10),new Vector2(w-5,35),new Vector2(w,65),new Vector2(w,h-75),new Vector2(w-10,h-35),new Vector2(w-35,h-8),new Vector2(w-75,h),new Vector2(75,h),new Vector2(35,h-8),new Vector2(10,h-35),new Vector2(0,h-75),new Vector2(0,65),new Vector2(5,35),new Vector2(25,10)};
            var outline=provider.GetDataProvider<ISpriteOutlineDataProvider>();if(outline==null)throw new Exception("Sprite outline provider unavailable");outline.SetOutlines(id,new System.Collections.Generic.List<Vector2[]>{points.Select(p=>new Vector2(p.x-w*.5f,h*.5f-p.y)).ToArray()});provider.Apply();importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(Root+file).OfType<Sprite>().First();
        }
        static void Pos(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        static void Text(UnityEngine.UI.Text t,int size){t.alignment=TextAnchor.MiddleCenter;t.alignByGeometry=true;t.resizeTextForBestFit=true;t.resizeTextMinSize=14;t.resizeTextMaxSize=size;}
        public static void Apply()
        {
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var m=hud.GetComponent<DesignerMenus>();var shop=(RectTransform)m.storeRoot.transform.Find("Card");var panel=Import("Layer 1.png",true);var product=Import("rays.png",false);
            var image=shop.GetComponent<UnityEngine.UI.Image>();image.sprite=panel;image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;image.useSpriteMesh=true;shop.sizeDelta=new Vector2(1040,1040*panel.rect.height/panel.rect.width);
            shop.Find("DesignerTitle_Title").gameObject.SetActive(false);var heading=shop.Find("Title").GetComponent<UnityEngine.UI.Text>();Pos(heading,24,shop.sizeDelta.y*.417f,250,45);Text(heading,32);
            var titleIcon=shop.Find("StoreHeadingIcon") as RectTransform;if(!titleIcon){titleIcon=new GameObject("StoreHeadingIcon",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();titleIcon.SetParent(shop,false);}Pos(titleIcon,-112,shop.sizeDelta.y*.417f,44,44);var icon=titleIcon.GetComponent<UnityEngine.UI.Image>();icon.sprite=hud.GetComponent<DesignerMenus>().storeRoot.transform.parent.Find("StoreButton")?.GetComponent<UnityEngine.UI.Image>()?.sprite;icon.raycastTarget=false;
            // Use the existing store HUD icon if its hierarchy differs.
            if(!icon.sprite){icon.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/_LabSorting/Art/UI/Lab sorting game mockup and sprites 1/Gameplay/glossy_blue_and_gold_game_ui_icons_10.png").OfType<Sprite>().First();}icon.preserveAspect=true;
            var viewport=(RectTransform)shop.Find("ProductsViewport");Pos(viewport,0,-3,920,370);var scroll=viewport.GetComponent<UnityEngine.UI.ScrollRect>();scroll.content.sizeDelta=new Vector2(6*232-16,366);
            var priceSprite=AssetDatabase.LoadAllAssetsAtPath("Assets/_LabSorting/Art/UI/Lab sorting game mockup and sprites 1/Gameplay/All items Panel sprite/Layer 2.png").OfType<Sprite>().First();
            for(int i=0;i<m.purchases.Length;i++)
            {
                var button=m.purchases[i];var tile=(RectTransform)button.transform.parent;Pos(tile,108+i*232,0,216,216*product.rect.height/product.rect.width);tile.anchorMin=tile.anchorMax=new Vector2(0,.5f);var tileImage=tile.GetComponent<UnityEngine.UI.Image>();tileImage.sprite=product;tileImage.type=UnityEngine.UI.Image.Type.Simple;tileImage.preserveAspect=true;tileImage.useSpriteMesh=true;
                var name=tile.Find("Name").GetComponent<UnityEngine.UI.Text>();Pos(name,0,tile.rect.height*.422f,186,38);Text(name,22);Pos(tile.Find("DesignerIcon"),0,54,128,128);var amount=tile.Find("Amount").GetComponent<UnityEngine.UI.Text>();Pos(amount,0,-45,182,48);Text(amount,19);
                var badge=tile.Find("PriceBadge") as RectTransform;if(!badge){badge=new GameObject("PriceBadge",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();badge.SetParent(tile,false);}Pos(badge,0,-105,184,36);var bg=badge.GetComponent<UnityEngine.UI.Image>();bg.sprite=priceSprite;bg.type=UnityEngine.UI.Image.Type.Sliced;bg.pixelsPerUnitMultiplier=priceSprite.rect.height/36;bg.raycastTarget=false;
                var price=m.prices[i];price.transform.SetParent(badge,false);Pos(price,0,0,160,28);Text(price,20);price.color=Color.white;
                Pos(button,0,-149,184,40);var label=button.transform.Find("PurchaseLabel")?.GetComponent<UnityEngine.UI.Text>();if(!label){label=UnityEngine.Object.Instantiate(price,button.transform);label.name="PurchaseLabel";}label.text="PURCHASE";label.color=new Color(.02f,.06f,.18f);Pos(label,0,0,160,32);Text(label,19);
            }
            Pos(shop.Find("ProductsScrollbar"),0,-194,900,8);Pos(m.cashAdButton,0,-229,340,40);Pos(m.storeStatus,0,-270,880,24);Text(m.storeStatus,17);Pos(m.storeClose,474,218,42,42);scroll.horizontalNormalizedPosition=0;
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
