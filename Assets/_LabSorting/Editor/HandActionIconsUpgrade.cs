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
        const string HandPick=Game+"Pinching Hand with Wrist Cuff-1.png";
        const string HandPlace=Game+"Hand Dropping Rounded Square Icon-2.png";
        const string HandDrop=Game+"White Hand Throwing Puff Icon-3.png";
        // PICK = pinching hand, PLACE = hand setting an item down, DROP (physics throw) = throwing hand.
        [MenuItem("Tools/Lab Sorting/Designer/Hand Action Icons (Pick, Place, Drop)")]
        public static void ApplyHandActionIcons()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop play mode first");
            var actions=UnityEngine.Object.FindFirstObjectByType<ContextActionHUD>();
            if(!actions||!actions.pick||!actions.place||!actions.drop)throw new Exception("Open the gameplay scene with the designer HUD first");
            var report=new System.Text.StringBuilder();
            foreach(var (button,path,key) in new[]{(actions.pick,HandPick,"pick"),(actions.place,HandPlace,"place"),(actions.drop,HandDrop,"drop")})
            {
                var sprite=ImportCroppedIcon(path,"LabUI_"+key);
                var icon=button.transform.Find("DesignerIcon");if(!icon)throw new Exception("Missing DesignerIcon on "+button.name);
                var image=icon.GetComponent<UnityEngine.UI.Image>();
                image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;image.color=Color.white;image.raycastTarget=false;
                EditorUtility.SetDirty(image);report.Append(button.name+" -> "+sprite.name+" "+sprite.rect.size+"; ");
            }
            Debug.Log("HAND ICONS: "+report);
            EditorSceneManager.MarkSceneDirty(actions.gameObject.scene);EditorSceneManager.SaveScene(actions.gameObject.scene);
        }
        public static void ApplyHandActionIconsBatch(){EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");ApplyHandActionIcons();}

        // Same budget as UITextureBudget (512, ASTC 4x4 on mobile). The sprite is cropped to the visible
        // artwork (glow included) so the wide transparent margins don't shrink the hand inside the button.
        static Sprite ImportCroppedIcon(string path,string name)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(!importer)throw new Exception("Missing artwork: "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.crunchedCompression=false;
            foreach(string platform in new[]{"Android","iPhone"})
            {var s=importer.GetPlatformTextureSettings(platform);s.overridden=true;s.maxTextureSize=512;s.format=TextureImporterFormat.ASTC_4x4;s.compressionQuality=100;s.crunchedCompression=false;importer.SetPlatformTextureSettings(s);}
            importer.SaveAndReimport();

            var source=new Texture2D(2,2);source.LoadImage(File.ReadAllBytes(path));var pixels=source.GetPixels32();int w=source.width,h=source.height;UnityEngine.Object.DestroyImmediate(source);
            int left=w,right=-1,bottom=h,top=-1;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[y*w+x].a>8){if(x<left)left=x;if(x>right)right=x;if(y<bottom)bottom=y;if(y>top)top=y;}
            if(right<left)throw new Exception("Artwork is fully transparent: "+path);
            left=Math.Max(0,left-2);right=Math.Min(w-1,right+2);bottom=Math.Max(0,bottom-2);top=Math.Min(h-1,top+2);

            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var old=provider.GetSpriteRects();var id=old.Length>0?old[0].spriteID:GUID.Generate();
            provider.SetSpriteRects(new[]{new SpriteRect{name=name,rect=new UnityEngine.Rect(left,bottom,right-left+1,top-bottom+1),pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,spriteID=id,border=Vector4.zero}});
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(name,id)});
            provider.Apply();importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }
    }
}
