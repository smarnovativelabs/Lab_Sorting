using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        static Sprite MockupSprite(string path,string name,Vector4 border)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>()?.GetEditCapability();
            foreach(var flag in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditSpriteName,EEditCapability.EditPivot})if(capability==null||!capability.Value.HasCapability(flag))throw new Exception("Sprite editing unsupported: "+flag);
            var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));int w=texture.width,h=texture.height;UnityEngine.Object.DestroyImmediate(texture);
            var old=provider.GetSpriteRects();var id=old.Length>0?old[0].spriteID:GUID.Generate();provider.SetSpriteRects(new[]{new SpriteRect{name=name,rect=new UnityEngine.Rect(0,0,w,h),pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,border=border,spriteID=id}});
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(name,id)});provider.Apply();importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }
        static void Art(Transform target,Sprite sprite,bool sliced,float multiplier=1)
        {var image=Ensure<UnityEngine.UI.Image>(target);image.sprite=sprite;image.type=sliced?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.preserveAspect=!sliced;image.pixelsPerUnitMultiplier=multiplier;image.color=Color.white;}
        public static void ApplyMockupShapes()
        {
            Load();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var safe=hud.transform.Find("SafeArea");
            var panel=MockupSprite(Game+"All items Panel sprite/Layer 3.png","Mockup_AllItemsPanel",Vector4.zero);
            var rowArt=MockupSprite(Game+"All items Panel sprite/Layer 2.png","Mockup_CyanRow",new Vector4(65,45,65,45));
            var closeArt=MockupSprite(Game+"All items Panel sprite/Layer 4.png","Mockup_CloseBase",Vector4.zero);
            var closeCross=MockupSprite(Game+"All items Panel sprite/Rectangle 1 copy.png","Mockup_CloseCross",Vector4.zero);
            var collected=MockupSprite(Game+"collected items sprite/polished_blue-and-gold_ui_asset_sheet_01.png","Mockup_CollectedPanel",new Vector4(130,130,130,250));
            var scrollTrack=MockupSprite(Game+"collected items sprite/polished_blue-and-gold_ui_asset_sheet_02.png","Mockup_ScrollTrack",new Vector4(4,25,4,25));
            var scrollHandle=MockupSprite(Game+"collected items sprite/polished_blue-and-gold_ui_asset_sheet_03.png","Mockup_ScrollHandle",new Vector4(4,25,4,25));
            // Large panels need large visible corner radii, not button-sized corners.
            foreach(var image in hud.GetComponentsInChildren<UnityEngine.UI.Image>(true))if(image.sprite==sprites["frame"]&&image.rectTransform.rect.width>200)image.pixelsPerUnitMultiplier=1.15f;
            foreach(var image in hud.GetComponentsInChildren<UnityEngine.UI.Image>(true))if(image.sprite==sprites["title"]){image.sprite=sprites["blue"];image.pixelsPerUnitMultiplier=sprites["blue"].rect.height/Mathf.Max(1,image.rectTransform.rect.height);}
            var progress=hud.GetComponent<ItemsProgressPanel>();progress.card.sizeDelta=new Vector2(850,850*panel.rect.height/panel.rect.width);Art(progress.card,panel,false);
            foreach(var text in progress.card.GetComponentsInChildren<UnityEngine.UI.Text>(true))if(text.text.StartsWith("Remaining includes"))text.gameObject.SetActive(false);
            var previous=progress.card.Find("DesignerTitle_"+progress.summary.name);if(previous)previous.gameObject.SetActive(false);
            progress.heading=Label("MockupHeading",progress.card,"ALL ITEMS",Vector2.zero,new Vector2(430,62),32);progress.heading.font=headingFont;Pos(progress.heading.rectTransform,new Vector2(.5f,1),new Vector2(0,-48),new Vector2(430,62));Ensure<LabLocalizedLabel>(progress.heading);
            Pos(progress.summary.rectTransform,new Vector2(.5f,1),new Vector2(0,-125),new Vector2(700,36));progress.summary.resizeTextMaxSize=24;
            for(int i=0;i<progress.rows.Length;i++)
            {
                var row=(RectTransform)progress.rows[i].transform.parent;Pos(row,new Vector2(.5f,1),new Vector2(0,-180-i*70),new Vector2(760,60));Art(row,rowArt,true,rowArt.rect.height/60);
                progress.rows[i].rectTransform.offsetMin=new Vector2(24,5);progress.rows[i].rectTransform.offsetMax=new Vector2(-175,-5);progress.rows[i].resizeTextMinSize=18;progress.rows[i].resizeTextMaxSize=24;
            }
            Pos((RectTransform)progress.closeButton.transform,new Vector2(1,1),new Vector2(-48,-85),Vector2.one*52);
            Art(progress.closeButton.transform,closeArt,false);var cross=Rect("MockupCross",progress.closeButton.transform,Vector2.one*27);Art(cross,closeCross,false);cross.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            Pos((RectTransform)progress.previousButton.transform,new Vector2(.5f,0),new Vector2(-105,36),Vector2.one*42);Pos((RectTransform)progress.nextButton.transform,new Vector2(.5f,0),new Vector2(105,36),Vector2.one*42);Pos(progress.pageLabel.rectTransform,new Vector2(.5f,0),new Vector2(0,36),new Vector2(100,28));
            var list=(RectTransform)hud.inventoryPanel.transform;Pos(list,new Vector2(0,1),new Vector2(164,-279),new Vector2(290,350));Art(list,collected,true,3.5f);
            Pos(hud.inventorySummary.rectTransform,new Vector2(.5f,1),new Vector2(-15,-28),new Vector2(230,34));hud.inventorySummary.resizeTextMaxSize=19;
            var plus=list.Find("CapacityPlus");if(plus)Pos((RectTransform)plus,new Vector2(1,1),new Vector2(-25,-28),Vector2.one*36);
            var scroll=list.GetComponent<UnityEngine.UI.ScrollRect>();Pos(scroll.viewport,new Vector2(.5f,.5f),new Vector2(-5,-23),new Vector2(252,264));
            Pos((RectTransform)scroll.verticalScrollbar.transform,new Vector2(1,.5f),new Vector2(-14,-23),new Vector2(12,258));
            Art(scroll.verticalScrollbar.transform,scrollTrack,true,scrollTrack.rect.width/12);Art(scroll.verticalScrollbar.handleRect,scrollHandle,true,scrollHandle.rect.width/12);
            foreach(var button in hud.inventoryButtons)button.image.pixelsPerUnitMultiplier=1.4f;
            var tutorial=hud.GetComponent<GuidedTutorial>();var tutorialImage=tutorial.bubble.GetComponent<UnityEngine.UI.Image>();float th=440*tutorialImage.sprite.rect.height/tutorialImage.sprite.rect.width;Pos(tutorial.bubble,new Vector2(1,1),new Vector2(-254,-185),new Vector2(440,th));
            Pos(tutorial.message.rectTransform,new Vector2(.5f,.5f),new Vector2(-64,-10),new Vector2(270,64));Pos(tutorial.stepLabel.rectTransform,Vector2.one*.5f,new Vector2(-25,54),new Vector2(210,25));tutorial.message.resizeTextMinSize=16;tutorial.message.resizeTextMaxSize=20;
            tutorial.stepLabel.font=headingFont;tutorial.stepLabel.alignment=TextAnchor.MiddleCenter;tutorial.stepLabel.color=Color.white;tutorial.stepLabel.resizeTextMaxSize=22;
            var rewards=hud.GetComponent<SortingRewardsHUD>();var old=safe.Find("DesignerTitle_"+rewards.milestone.name);if(old)old.gameObject.SetActive(false);
            var task=Rect("MockupTaskCard",safe,new Vector2(440,th));Pos(task,new Vector2(1,1),new Vector2(-254,-185),new Vector2(440,th));Skin(task,"doctor");
            var taskTitle=Label("TaskHeading",task,"TASK",new Vector2(-25,54),new Vector2(210,25),24);taskTitle.font=headingFont;Ensure<LabLocalizedLabel>(taskTitle);
            rewards.milestone.transform.SetParent(task,false);Pos(rewards.milestone.rectTransform,new Vector2(.5f,.5f),new Vector2(-64,-10),new Vector2(270,64));rewards.milestone.resizeTextMaxSize=20;rewards.milestone.resizeTextMinSize=17;
            var track=(RectTransform)rewards.shortGoalFill.parent;track.SetParent(task,false);Pos(track,new Vector2(.5f,.5f),new Vector2(-64,-59),new Vector2(250,8));
            var layout=Ensure<TaskCardLayout>(hud);layout.tutorial=tutorial;layout.card=task;
            var collection=UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection("LabUI");foreach(var table in collection.StringTables){if(table.LocaleIdentifier.Code=="es")table.AddEntry("TASK","TAREA");if(table.LocaleIdentifier.Code=="en")table.AddEntry("TASK","TASK");EditorUtility.SetDirty(table);}EditorUtility.SetDirty(collection.SharedData);EditorUtility.SetDirty(collection);UnityEditor.Localization.LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
