using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
namespace LabSorting.Editor
{
    public static class PauseAndBarShapeUpgrade
    {
        public static void Apply()
        {
            const string root="Assets/_LabSorting/Art/UI/Lab sorting game mockup and sprites 1/Pause/";
            var importer=(TextureImporter)AssetImporter.GetAtPath(root+"Layer 2.png");
            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditBorder))throw new Exception("Border editing unavailable");
            var rects=provider.GetSpriteRects();
            // Protect the complete shoulder, including the highlight, beyond the semicircle.
            foreach(var rect in rects)rect.border=new Vector4(rect.rect.height*.85f,0,rect.rect.height*.85f,0);
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var pauseImporter=(TextureImporter)AssetImporter.GetAtPath(root+"Layer 1 copy.png");
            pauseImporter.textureType=TextureImporterType.Sprite;pauseImporter.spriteImportMode=SpriteImportMode.Single;pauseImporter.mipmapEnabled=false;pauseImporter.alphaIsTransparency=true;pauseImporter.npotScale=TextureImporterNPOTScale.None;pauseImporter.maxTextureSize=1024;pauseImporter.textureCompression=TextureImporterCompression.Compressed;pauseImporter.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(root+"Layer 1 copy.png");
            var hud=UnityEngine.Object.FindFirstObjectByType<LabSorting.UI.SortingHUD>();
            var card=(RectTransform)hud.transform.Find("SafeArea/DesignerPause/Card");
            card.sizeDelta=new Vector2(420,420*sprite.rect.height/sprite.rect.width);
            var image=card.GetComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;
            card.Find("DesignerTitle_Title").gameObject.SetActive(false);
            var title=card.Find("Title").GetComponent<UnityEngine.UI.Text>();title.rectTransform.anchoredPosition=new Vector2(0,card.sizeDelta.y*.418f);title.rectTransform.sizeDelta=new Vector2(248,48);title.fontSize=32;title.resizeTextMaxSize=32;title.text="PAUSED";
            string[] names={"Resume","Save","Settings","Store"};
            for(int i=0;i<names.Length;i++){var button=(RectTransform)card.Find(names[i]);button.anchoredPosition=new Vector2(0,120-i*83);button.sizeDelta=new Vector2(300,66);var art=button.GetComponent<UnityEngine.UI.Image>();art.pixelsPerUnitMultiplier=art.sprite.rect.height/66;}
            ((RectTransform)card.Find("Status")).anchoredPosition=new Vector2(0,-205);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
