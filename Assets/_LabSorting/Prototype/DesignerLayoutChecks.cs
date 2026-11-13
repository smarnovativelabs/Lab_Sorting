#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using LabSorting.UI;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class DesignerLayoutChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("UI LAYOUT: "+message);Debug.Log("UI LAYOUT PASS: "+message);}
        public static IEnumerator Run(SortingSession session)
        {
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var canvas=hud.GetComponent<Canvas>();var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();var safe=hud.GetComponentInChildren<SafeAreaPanel>();var menus=hud.GetComponent<DesignerMenus>();var supply=hud.GetComponent<SupplyPanel>();var settings=hud.GetComponent<SettingsTutorialPanel>();
            var camera=session.player.view;var oldTarget=camera.targetTexture;var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldScale=canvas.scaleFactor,oldPlane=canvas.planeDistance;bool oldScaler=scaler.enabled,oldSafe=safe.enabled;
            var corners=new Vector3[4];
            try
            {
                scaler.enabled=false;safe.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.01f;
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1440,720),new Vector2Int(1560,720),new Vector2Int(1600,720),new Vector2Int(1024,768)})
                {
                    var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;
                    try
                    {
                        canvas.scaleFactor=Mathf.Min(size.x/scaler.referenceResolution.x,size.y/scaler.referenceResolution.y);
                        safe.ApplySample(new Rect(32,20,size.x-64,size.y-40),size);Canvas.ForceUpdateCanvases();yield return null;
                        var controls=hud.GetComponent<ControlSizes>();
                        if(controls)
                        {
                            var rects=new System.Collections.Generic.List<Rect>();
                            foreach(var control in new System.Collections.Generic.List<RectTransform>(controls.actions){controls.joystick})
                            {
                                control.GetWorldCorners(corners);var low=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var high=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                                Check(low.x>=31&&high.x<=size.x-31&&low.y>=19&&high.y<=size.y-19,"Sized control remains inside safe area: "+control.name+" at "+size);
                                var rect=new Rect(low,high-low);foreach(var other in rects)Check(!rect.Overlaps(other),"Sized controls do not overlap at "+size);rects.Add(rect);
                            }
                        }
                        var goal=hud.GetComponent<TaskCardLayout>().card;
                        goal.GetWorldCorners(corners);float goalRight=RectTransformUtility.WorldToScreenPoint(camera,corners[2]).x;float goalBottom=RectTransformUtility.WorldToScreenPoint(camera,corners[0]).y;
                        foreach(var tool in new[]{hud.GetComponent<EconomyHUD>().searchButton,hud.GetComponent<EconomyHUD>().magnetButton})
                        {
                            ((RectTransform)tool.transform).GetWorldCorners(corners);
                            Check(goalRight+8<RectTransformUtility.WorldToScreenPoint(camera,corners[0]).x||goalBottom>RectTransformUtility.WorldToScreenPoint(camera,corners[2]).y+8,"Task card leaves tool column clear at "+size+" goalBottom="+goalBottom+" toolTop="+RectTransformUtility.WorldToScreenPoint(camera,corners[2]).y);
                        }
                        menus.OpenPause();yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();camera.Render();Canvas.ForceUpdateCanvases();
                        var pauseCard=(RectTransform)menus.pauseRoot.transform.Find("Card");
                        var pauseArt=pauseCard.GetComponent<UnityEngine.UI.Image>();
                        Check(pauseArt.sprite.name=="Layer 1 copy"&&pauseArt.preserveAspect,"Pause uses supplied designer artwork");
                        Check(Mathf.Abs(pauseCard.rect.width/pauseCard.rect.height-pauseArt.sprite.rect.width/pauseArt.sprite.rect.height)<.01f,"Pause artwork aspect remains natural");
                        foreach(var target in new[]{pauseCard,(RectTransform)safe.transform.Find("DesignerTitle_Progress"),(RectTransform)safe.transform.Find("DesignerToast")})
                        {
                            target.GetWorldCorners(corners);var lo=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var hi=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                            Check(lo.x>=31&&hi.x<=size.x-31&&lo.y>=19&&hi.y<=size.y-19,"Pause/progress/toast fits safe area: "+target.name+" at "+size);
                            Check(Mathf.Abs(target.lossyScale.x-target.lossyScale.y)<.001f,"No nonuniform UI scale: "+target.name);
                            if(target==pauseCard)continue;
                            var art=target.GetComponent<UnityEngine.UI.Image>();
                            Check(art.type==UnityEngine.UI.Image.Type.Sliced&&art.sprite.border.x>=art.sprite.rect.height*.8f&&art.sprite.border.z>=art.sprite.rect.height*.8f,"Entire rounded shoulder protected: "+target.name);
                            Check(Mathf.Abs(art.pixelsPerUnitMultiplier-art.sprite.rect.height/target.rect.height)<.01f,"End caps use uniform pixel scale: "+target.name);
                        }
                        camera.Render();var pauseActive=RenderTexture.active;RenderTexture.active=rt;var pauseTexture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);pauseTexture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);pauseTexture.Apply();File.WriteAllBytes("Logs/PauseShapes-"+size.x+".png",pauseTexture.EncodeToPNG());UnityEngine.Object.Destroy(pauseTexture);RenderTexture.active=pauseActive;menus.Close();
                        for(int panel=0;panel<3;panel++)
                        {
                            RectTransform card;
                            if(panel==0){menus.OpenStore();card=(RectTransform)menus.storeRoot.transform.Find("Card");}
                            else if(panel==1){supply.Open(LabSorting.Sorting.SupplyKind.Magnet);card=(RectTransform)supply.title.transform.parent;}
                            else{settings.OpenSettings();card=(RectTransform)settings.title.transform.parent;}
                            yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
                            card.GetWorldCorners(corners);var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                            Check(min.x>=31&&max.x<=size.x-31&&min.y>=19&&max.y<=size.y-19,"Modal fits inset safe area "+panel+" at "+size);
                            Check(Mathf.Abs(card.lossyScale.x-card.lossyScale.y)<.001f,"Modal scales uniformly");
                            if(panel==0)
                            {
                                StoreTouchChecks.Prices(menus);
                                var scroll=card.Find("ProductsViewport").GetComponent<UnityEngine.UI.ScrollRect>();scroll.horizontalNormalizedPosition=1;Canvas.ForceUpdateCanvases();yield return null;
                                var last=(RectTransform)menus.purchases[5].transform;last.GetWorldCorners(corners);var end=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                                Check(end.x<=max.x-10,"Final capacity purchase reachable by scrolling");scroll.horizontalNormalizedPosition=0;
                            }
                            camera.Render();var active=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();File.WriteAllBytes("Logs/UILayout-"+panel+"-"+size.x+".png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);RenderTexture.active=active;
                            if(panel==1)
                            {
                                var dismiss=supply.root.GetComponent<SupplyBackdropDismiss>();Check(dismiss,"Offer backdrop dismissal wired");
                                var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){pointerId=1,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
                                data.pointerPressRaycast=new UnityEngine.EventSystems.RaycastResult{module=canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>()};
                                data.position=(min+max)*.5f;dismiss.OnPointerDown(data);dismiss.OnPointerClick(data);Check(supply.IsOpen,"Inside card does not dismiss");
                                dismiss.OnPointerDown(data);data.position=new Vector2(40,40);dismiss.OnPointerClick(data);Check(supply.IsOpen,"Dragging out does not dismiss");
                                int cash=session.Cash;dismiss.OnPointerDown(data);dismiss.OnPointerClick(data);yield return new WaitForSecondsRealtime(.2f);Check(!supply.IsOpen&&!session.player.blocked&&session.Cash==cash,"Outside tap closes without purchase or blocked controls");
                                supply.OpenSlots();yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
                                Check(card.anchorMin.x==0&&supply.offerArtwork.localScale.x==-1,"Capacity opens beside the left inventory with inward-facing artwork");
                                card.GetWorldCorners(corners);Check(RectTransformUtility.WorldToScreenPoint(camera,corners[0]).x>=31&&RectTransformUtility.WorldToScreenPoint(camera,corners[2]).x<=size.x-31,"Capacity fits safe area at "+size);
                                SortingSmokeChecks.Capture(session,"Logs/CapacityLeft-"+size.x+".png");supply.Close();
                            }
                            if(panel==0)menus.Close();else if(panel==1)supply.Close();else settings.Close();
                        }
                    }
                    finally{camera.targetTexture=oldTarget;UnityEngine.Object.Destroy(rt);}
                }
            }
            finally{if(menus.IsOpen)menus.Close();if(supply.IsOpen)supply.Close();if(settings.IsOpen)settings.Close();camera.targetTexture=oldTarget;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;canvas.scaleFactor=oldScale;scaler.enabled=oldScaler;safe.enabled=oldSafe;safe.Apply();}
        }
    }
}
#endif
