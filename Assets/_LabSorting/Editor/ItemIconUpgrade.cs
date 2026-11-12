using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.SceneManagement;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class ItemIconUpgrade
    {
        static readonly string[] First={"first-aid-box","antibiotic-1","antibiotic-2","bandage-rolls-1","bandage-rolls-2","blood-bag","disinfectant-spray","face-mask-box-1","face-mask-box-2","first-aid-box-2","gas-mask","hand-sanitizer-bottles-1",null,"iv-bag","medicine-bottle-2","medicine-bottle-3","medicine-bottle-4","painkiller-1","painkiller-2","surgical-gloves-box","surgical-gloves-box-2"};
        static readonly string[] Second={"battries-large","battries-small","blood-sample-tube","emergency-tool-kit-blue","emergency-tool-kit-green","flashlight","food-rations-1","food-rations-2","medical-test-kit","micro-scope","micro-scope-big","oxygen-cylinder-1","oxygen-cylinder-2","protein-bar-1","protein-bar-2","radio-1","radio-2","test-tube","thermal-blanket-1","thermal-blanket-2","therometer-1","vilamin-bottle-1","vilamin-bottle-2","wakie-takie-2","water-purification-1","water-purification-2"};
        static void Slice(string path,string[] names,int columns)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))throw new Exception("Sprite slicing unsupported: "+path);
            var original=provider.GetSpriteRects();var pixels=new Texture2D(2,2);pixels.LoadImage(File.ReadAllBytes(path));var colors=pixels.GetPixels32();
            var rects=new System.Collections.Generic.List<SpriteRect>();
            try
            {
                for(int i=0;i<names.Length;i++)
                {
                    if(names[i]==null)continue;int x0=Mathf.RoundToInt(i%columns*pixels.width/(float)columns),x1=Mathf.RoundToInt((i%columns+1)*pixels.width/(float)columns);
                    int y0=pixels.height-Mathf.RoundToInt((i/columns+1)*pixels.height/(float)columns),y1=pixels.height-Mathf.RoundToInt(i/columns*pixels.height/(float)columns);
                    int minX=x1,minY=y1,maxX=x0,maxY=y0;
                    for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){var c=colors[y*pixels.width+x];if(c.a>20){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}}
                    if(maxX<=minX||maxY<=minY)throw new Exception("Empty icon "+names[i]);
                    var old=original.FirstOrDefault(r=>r.name==names[i]);
                    rects.Add(new SpriteRect{name=names[i],rect=new Rect(minX,minY,maxX-minX+1,maxY-minY+1),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),spriteID=old!=null?old.spriteID:GUID.Generate()});
                }
                provider.SetSpriteRects(rects.ToArray());
                var ids=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();if(ids!=null)ids.SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
                provider.Apply();importer.SaveAndReimport();
            }
            finally{UnityEngine.Object.DestroyImmediate(pixels);}
            foreach(var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
            {var def=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_LabSorting/Designer/Definitions/"+sprite.name+".asset");if(!def)throw new Exception("Missing definition "+sprite.name);def.icon=sprite;EditorUtility.SetDirty(def);}
        }
        static UnityEngine.UI.Image Icon(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var child=parent.Find(name);if(!child){child=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)).transform;child.SetParent(parent,false);}
            var image=child.GetComponent<UnityEngine.UI.Image>();image.preserveAspect=true;image.raycastTarget=false;image.color=Color.white;
            var rect=image.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return image;
        }
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode");
            Slice("Assets/_LabSorting/Art/UI/Lab_Items_Atlas_2048.png",First,5);Slice("Assets/_LabSorting/Art/UI/Lab_Items_Atlas_2_2048.png",Second,6);
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();hud.inventoryIcons=new UnityEngine.UI.Image[hud.inventoryButtons.Length];
            for(int i=0;i<hud.inventoryButtons.Length;i++)
            {
                var button=hud.inventoryButtons[i];var r=(RectTransform)button.transform;r.sizeDelta=new Vector2(124,62);r.anchoredPosition=new Vector2((i%5-2)*130,66+i/5*68);
                hud.inventoryIcons[i]=Icon(button.transform,"ItemIcon",new Vector2(25,0),new Vector2(44,48));
                var text=hud.inventoryLabels[i];text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(51,4);text.rectTransform.offsetMax=new Vector2(-4,-4);text.fontSize=15;text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=15;text.alignment=TextAnchor.MiddleLeft;
            }
            hud.activeIcon=Icon(hud.active.transform.parent,"SelectedItemIcon",new Vector2(30,0),new Vector2(48,48));
            hud.active.rectTransform.anchoredPosition=new Vector2(30,210);hud.active.rectTransform.sizeDelta=new Vector2(310,64);hud.active.fontSize=19;hud.activeIcon.rectTransform.anchorMin=hud.activeIcon.rectTransform.anchorMax=new Vector2(.5f,0);hud.activeIcon.rectTransform.anchoredPosition=new Vector2(-160,210);var bg=(RectTransform)hud.transform.Find("SafeArea/SelectedItemCard");bg.anchoredPosition=new Vector2(0,210);var reward=UnityEngine.Object.FindFirstObjectByType<SortingRewardsHUD>();reward.selectedSwatch.enabled=false;reward.ability.rectTransform.anchoredPosition=new Vector2(0,270);
            hud.previousPage.GetComponent<RectTransform>().anchoredPosition=new Vector2(-350,100);hud.nextPage.GetComponent<RectTransform>().anchoredPosition=new Vector2(350,100);hud.pageLabel.rectTransform.anchoredPosition=new Vector2(350,61);
            hud.progress.fontSize=22;hud.status.fontSize=18;hud.target.fontSize=20;
            var panel=UnityEngine.Object.FindFirstObjectByType<ItemsProgressPanel>();panel.rowIcons=new UnityEngine.UI.Image[panel.rows.Length];
            for(int i=0;i<panel.rows.Length;i++){var row=panel.rows[i];panel.rowIcons[i]=Icon(row.transform.parent,"ItemIcon",new Vector2(23,0),new Vector2(38,38));row.rectTransform.anchorMin=Vector2.zero;row.rectTransform.anchorMax=Vector2.one;row.rectTransform.offsetMin=new Vector2(52,3);row.rectTransform.offsetMax=new Vector2(-12,-3);row.fontSize=17;}
            EditorUtility.SetDirty(hud);EditorUtility.SetDirty(panel);AssetDatabase.SaveAssets();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("ITEM_ICONS_WIRED 46");
        }
    }
}
