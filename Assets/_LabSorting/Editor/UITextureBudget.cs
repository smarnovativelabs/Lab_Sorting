using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
namespace LabSorting.Editor
{
    public static class UITextureBudget
    {
        public static string Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop play mode");
            var paths=AssetDatabase.GetDependencies(new[]{"Assets/Scenes/Lab_Sorting.unity","Assets/Scenes/MainMenu.unity"},true).Where(p=>p.StartsWith("Assets/_LabSorting/Art/UI/")&&AssetImporter.GetAtPath(p) is TextureImporter).ToArray();
            var report=new StringBuilder("UI texture import budget (source artwork retained)\n");long before=0,after=0;int changed=0,cropped=0;
            foreach(var path in paths)
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);before+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                if(path.Contains("Atlas")||path.EndsWith("TutorialHand.png")){after+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);continue;}
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                int oldWidth=texture.width,oldHeight=texture.height;
                bool large=path.Contains("Panel")||path.Contains("Loading Screen")||path.Contains("asset_sheet_01")||path.Contains("All items");
                int max=large?1024:path.Contains("asset_sheet_02")||path.Contains("asset_sheet_03")?256:512;
                // Already small icon artwork stays sharp; compression handles its memory cost.
                importer.maxTextureSize=max;importer.isReadable=false;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.crunchedCompression=false;
                foreach(string platform in new[]{"Android","iPhone"})
                {
                    var settings=importer.GetPlatformTextureSettings(platform);settings.overridden=true;settings.maxTextureSize=max;settings.format=TextureImporterFormat.ASTC_4x4;settings.compressionQuality=100;settings.crunchedCompression=false;importer.SetPlatformTextureSettings(settings);
                }
                var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);
                if(provider!=null&&importer.spriteImportMode==SpriteImportMode.Multiple)
                {
                    provider.InitSpriteEditorDataProvider();var rects=provider.GetSpriteRects();
                    if(rects.Length==1)
                    {
                        var cap=provider.GetDataProvider<ISpriteFrameEditCapability>();if(cap==null||!cap.GetEditCapability().HasCapability(EEditCapability.EditSpriteRect)||!cap.GetEditCapability().HasCapability(EEditCapability.EditBorder))throw new Exception("Sprite crop unsupported: "+path);
                        var source=new Texture2D(2,2);source.LoadImage(File.ReadAllBytes(path));var pixels=source.GetPixels32();var r=rects[0];int x0=(int)r.rect.x,y0=(int)r.rect.y,x1=(int)r.rect.xMax-1,y1=(int)r.rect.yMax-1;
                        int left=x1,right=x0,bottom=y1,top=y0;
                        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)if(pixels[y*source.width+x].a>8){left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
                        left=Math.Max(x0,left-2);right=Math.Min(x1,right+2);bottom=Math.Max(y0,bottom-2);top=Math.Min(y1,top+2);
                        if(right>left&&top>bottom&&(right-left+1f)*(top-bottom+1)<r.rect.width*r.rect.height*.97f)
                        {
                            r.border=new Vector4(Mathf.Max(0,r.border.x-(left-x0)),Mathf.Max(0,r.border.y-(bottom-y0)),Mathf.Max(0,r.border.z-(x1-right)),Mathf.Max(0,r.border.w-(y1-top)));
                            r.rect=new Rect(left,bottom,right-left+1,top-bottom+1);provider.SetSpriteRects(rects);provider.Apply();cropped++;
                        }
                        UnityEngine.Object.DestroyImmediate(source);
                    }
                }
                importer.SaveAndReimport();changed++;texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);after+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                report.AppendLine(oldWidth+"x"+oldHeight+" -> "+texture.width+"x"+texture.height+" "+texture.format+" | "+path);
            }
            string summary="Textures optimized: "+changed+", cropped: "+cropped+", loaded texture memory: "+(before/1048576f).ToString("F2")+" -> "+(after/1048576f).ToString("F2")+" MiB (active Editor platform).";
            report.AppendLine(summary);File.WriteAllText("Logs/UITextureBudget.txt",report.ToString());AssetDatabase.SaveAssets();return summary;
        }
    }
}
