using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class SettingsReferenceUpgrade
    {
        const string Root="Assets/_LabSorting/Art/UI/Store and settings panel sprite/Settings/";
        static Sprite Import(string name,bool panel=false)
        {
            string path=Root+name;var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.mipmapEnabled=false;importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=panel?1024:512;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var cap=provider.GetDataProvider<ISpriteFrameEditCapability>();
            foreach(var flag in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditSpriteName,EEditCapability.EditPivot,EEditCapability.EditBorder})if(cap==null||!cap.GetEditCapability().HasCapability(flag))throw new Exception("Sprite edit unsupported: "+flag);
            var source=new Texture2D(2,2);source.LoadImage(System.IO.File.ReadAllBytes(path));var rect=panel?new Rect(470,252,1626,1086):new Rect(0,0,source.width,source.height);UnityEngine.Object.DestroyImmediate(source);
            var old=provider.GetSpriteRects();var id=old.Length>0?old[0].spriteID:GUID.Generate();provider.SetSpriteRects(new[]{new SpriteRect{name=panel?"SettingsReferencePanel":"Settings_"+name,rect=rect,pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,spriteID=id,border=Vector4.zero}});provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(panel?"SettingsReferencePanel":"Settings_"+name,id)});
            if(panel)
            {
                // The delivered sheet has an opaque surround. A tight sprite mesh excludes it
                // without resampling or modifying the designer's source image.
                var outline=provider.GetDataProvider<ISpriteOutlineDataProvider>();if(outline==null)throw new Exception("Sprite outline editing unavailable");
                Vector2[] points={new Vector2(60,60),new Vector2(445,60),new Vector2(455,35),new Vector2(480,10),new Vector2(525,0),new Vector2(1115,0),new Vector2(1160,10),new Vector2(1185,35),new Vector2(1195,60),new Vector2(1565,60),new Vector2(1597,70),new Vector2(1618,95),new Vector2(1626,130),new Vector2(1626,1010),new Vector2(1618,1045),new Vector2(1595,1072),new Vector2(1560,1086),new Vector2(65,1086),new Vector2(30,1075),new Vector2(8,1050),new Vector2(0,1015),new Vector2(0,130),new Vector2(8,95),new Vector2(30,70)};
                outline.SetOutlines(id,new System.Collections.Generic.List<Vector2[]>{points.Select(p=>new Vector2(p.x-813,543-p.y)).ToArray()});
            }
            provider.Apply();importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }
        static void Pos(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        public static void Apply()
        {
            var h=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var p=h.GetComponent<SettingsTutorialPanel>();var services=h.GetComponent<ServicesPanel>();var card=(RectTransform)p.title.transform.parent;var image=card.GetComponent<UnityEngine.UI.Image>();var reference=h.GetComponent<SettingsReferenceArt>()??h.gameObject.AddComponent<SettingsReferenceArt>();if(!reference.tutorialSprite)reference.tutorialSprite=image.sprite;
            reference.panel=p;reference.image=image;reference.settingsSprite=Import("Layer 2 copy 4.png",true);reference.tutorialTitle=card.Find("DesignerTitle_Title").gameObject;
            card.sizeDelta=new Vector2(820,820*1086f/1626);image.sprite=reference.settingsSprite;image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;image.useSpriteMesh=true;reference.tutorialTitle.SetActive(false);Pos(p.title,0,card.sizeDelta.y*.427f,330,48);p.title.alignByGeometry=true;p.title.resizeTextMaxSize=32;
            Pos(p.closeButton,365,215,44,44);Pos(p.settingsContent.transform,0,0,760,440);
            foreach(var c in new Component[]{p.joystickSizeSlider,p.actionSizeSlider,p.joystickSizeLabel,p.actionSizeLabel})c.gameObject.SetActive(false);
            p.settingsContent.transform.Find("Help").gameObject.SetActive(false);
            Pos(p.soundButton,-185,145,300,46);Pos(p.hapticsButton,185,145,300,46);
            var track=Import("Layer 4.png");var knob=Import("Layer 6.png");
            var sliders=new[]{p.sensitivitySlider,p.moveSpeedSlider};var labels=new[]{p.sensitivityLabel,p.moveSpeedLabel};
            for(int i=0;i<2;i++){float y=76-i*69;Pos(labels[i],-185,y,310,42);labels[i].alignment=TextAnchor.MiddleLeft;labels[i].resizeTextMaxSize=21;Pos(sliders[i],175,y,310,42);var r=(RectTransform)sliders[i].transform.Find("Track");r.sizeDelta=new Vector2(310,310*track.rect.height/track.rect.width);var im=r.GetComponent<UnityEngine.UI.Image>();im.sprite=track;im.type=UnityEngine.UI.Image.Type.Simple;im.preserveAspect=true;sliders[i].handleRect.GetComponent<UnityEngine.UI.Image>().sprite=knob;((RectTransform)sliders[i].handleRect.parent).sizeDelta=new Vector2(280,42);((RectTransform)sliders[i].fillRect.parent).sizeDelta=new Vector2(290,16);}
            Pos(p.replayButton,-185,-62,300,44);Pos(p.settingsContent.transform.Find("HowToPlay"),185,-62,300,44);Pos(p.resetButton,-185,-131,300,44);Pos(p.settingsContent.transform.Find("Services"),185,-131,300,44);
            foreach(var spec in new[]{"PrivacyPolicy","RestorePurchases"})
            {
                var existing=p.settingsContent.transform.Find(spec);var button=existing?existing.GetComponent<UnityEngine.UI.Button>():UnityEngine.Object.Instantiate(p.resetButton,p.settingsContent.transform);button.name=spec;button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();if(spec=="PrivacyPolicy")UnityEventTools.AddPersistentListener(button.onClick,services.Policy);else UnityEventTools.AddPersistentListener(button.onClick,services.Restore);Pos(button,spec=="PrivacyPolicy"?-185:185,-200,310,44);button.image.color=Color.clear;var label=button.GetComponentInChildren<UnityEngine.UI.Text>(true);label.text=spec=="PrivacyPolicy"?"PRIVACY POLICY":"RESTORE PURCHASES";label.resizeTextMaxSize=22;label.alignment=TextAnchor.MiddleCenter;label.alignByGeometry=true;
            }
            var task=h.transform.Find("SafeArea/MockupTaskCard/TaskHeading").GetComponent<UnityEngine.UI.Text>();Pos(task,-25,61,210,30);task.alignment=TextAnchor.MiddleCenter;task.alignByGeometry=true;
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene);EditorSceneManager.SaveScene(h.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
