using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;

namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        // Presentation-only upgrade: keep all existing commerce and action callbacks.
        public static void PolishLayout()
        {
            Load();
            var hud=Object.FindFirstObjectByType<SortingHUD>();
            var safe=hud.transform.Find("SafeArea");
            var scaler=hud.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var supply=hud.GetComponent<SupplyPanel>();
            var card=(RectTransform)supply.title.transform.parent;
            card.sizeDelta=new Vector2(800,480);
            Pos(supply.title.rectTransform,Vector2.one*.5f,new Vector2(0,211),new Vector2(350,42));BackTitle(supply.title);
            Pos((RectTransform)supply.closeButton.transform,Vector2.one*.5f,new Vector2(360,207),Vector2.one*48);
            Pos(supply.description.rectTransform,Vector2.one*.5f,new Vector2(0,151),new Vector2(710,40));
            Pos(supply.owned.rectTransform,Vector2.one*.5f,new Vector2(0,112),new Vector2(710,32));
            int i=0;
            foreach(var b in new[]{supply.cashButton,supply.adButton,supply.storeButton})
            {
                Pos((RectTransform)b.transform,Vector2.one*.5f,new Vector2((i-1)*245,-7),new Vector2(225,180));
                b.targetGraphic=Skin(b.transform,"frame",true,true);
                Pos((RectTransform)b.transform.Find("DesignerIcon"),Vector2.one*.5f,new Vector2(0,35),Vector2.one*60);
                var label=b.GetComponentInChildren<UnityEngine.UI.Text>();
                Pos(label.rectTransform,Vector2.one*.5f,new Vector2(0,-44),new Vector2(195,60));
                label.resizeTextMaxSize=21;
                i++;
            }
            Pos((RectTransform)supply.useButton.transform,Vector2.one*.5f,new Vector2(0,-143),new Vector2(300,52));
            Pos(supply.status.rectTransform,Vector2.one*.5f,new Vector2(0,-202),new Vector2(710,40));

            // Wider shop cards scroll horizontally instead of squeezing six products into one row.
            var menus=hud.GetComponent<DesignerMenus>();
            var shop=menus.storeRoot.transform.Find("Card");
            var viewport=Rect("ProductsViewport",shop,new Vector2(938,340));viewport.anchoredPosition=new Vector2(0,7);
            Ensure<UnityEngine.UI.RectMask2D>(viewport);
            var content=Rect("Content",viewport,new Vector2(6*224-14,328));
            content.anchorMin=content.anchorMax=new Vector2(0,.5f);content.pivot=new Vector2(0,.5f);
            var scroll=Ensure<UnityEngine.UI.ScrollRect>(viewport);scroll.viewport=viewport;scroll.content=content;scroll.horizontal=true;scroll.vertical=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            for(i=0;i<6;i++)
            {
                var tile=menus.purchases[i].transform.parent as RectTransform;tile.SetParent(content,false);
                Pos(tile,new Vector2(0,.5f),new Vector2(105+i*224,0),new Vector2(210,320));
                Pos((RectTransform)tile.Find("Name"),Vector2.one*.5f,new Vector2(0,127),new Vector2(180,36));
                Pos((RectTransform)tile.Find("DesignerIcon"),Vector2.one*.5f,new Vector2(0,42),Vector2.one*105);
                Pos((RectTransform)tile.Find("Amount"),Vector2.one*.5f,new Vector2(0,-44),new Vector2(180,44));
                Pos((RectTransform)menus.purchases[i].transform,Vector2.one*.5f,new Vector2(0,-113),new Vector2(180,50));
                Pos(menus.prices[i].rectTransform,Vector2.one*.5f,Vector2.zero,new Vector2(156,40));
            }
            var rail=Rect("ProductsScrollbar",shop,new Vector2(900,12));rail.anchoredPosition=new Vector2(0,-177);
            var railImage=Ensure<UnityEngine.UI.Image>(rail);railImage.sprite=null;railImage.color=new Color(.015f,.04f,.12f);railImage.raycastTarget=true;
            var handle=Rect("Handle",rail,Vector2.zero);handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            var handleImage=Ensure<UnityEngine.UI.Image>(handle);handleImage.sprite=null;handleImage.color=new Color(1,.73f,.06f);handleImage.raycastTarget=true;
            var bar=Ensure<UnityEngine.UI.Scrollbar>(rail);bar.direction=UnityEngine.UI.Scrollbar.Direction.LeftToRight;bar.handleRect=handle;bar.targetGraphic=handleImage;scroll.horizontalScrollbar=bar;
            scroll.horizontalNormalizedPosition=0;

            var list=hud.inventoryPanel.transform;
            Pos(hud.inventorySummary.rectTransform,Vector2.one*.5f,new Vector2(-19,118),new Vector2(206,34));
            var listTrack=list.Find("Scrollbar").GetComponent<UnityEngine.UI.Image>();listTrack.sprite=null;listTrack.color=new Color(.015f,.04f,.12f);
            var listHandle=listTrack.transform.Find("Handle").GetComponent<UnityEngine.UI.Image>();listHandle.sprite=null;listHandle.color=new Color(1,.73f,.06f);
            var settings=hud.GetComponent<SettingsTutorialPanel>();
            Pos(settings.title.rectTransform,Vector2.one*.5f,new Vector2(0,202),new Vector2(360,42));BackTitle(settings.title);
            settings.sensitivitySlider.handleRect.sizeDelta=Vector2.one*36;
            var progress=hud.GetComponent<ItemsProgressPanel>();
            foreach(var b in new[]{progress.previousButton,progress.nextButton})((RectTransform)b.transform).sizeDelta=Vector2.one*48;
            foreach(var image in hud.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if(!image.sprite||!image.sprite.name.StartsWith("LabUI_"))continue;
                if(image.type!=UnityEngine.UI.Image.Type.Sliced){image.preserveAspect=true;continue;}
                // Use actual laid-out height, never a stale pre-layout RectTransform height.
                float height=image.rectTransform.rect.height;
                image.pixelsPerUnitMultiplier=image.sprite.name=="LabUI_frame"?image.sprite.border.x/18f:image.sprite.rect.height/Mathf.Max(1,height);
            }
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            if(!EditorSceneManager.SaveScene(hud.gameObject.scene))throw new System.IO.IOException("Cannot save polished UI");
        }
    }
}
