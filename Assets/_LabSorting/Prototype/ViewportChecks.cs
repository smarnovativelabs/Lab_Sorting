using System;
using System.Collections;
using System.IO;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class ViewportChecks
    {
        public static IEnumerator Run(SortingSession session,Action<bool,string> check)
        {
            var canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>();var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();var layout=canvas.GetComponent<LabResponsiveLayout>();var safe=(RectTransform)canvas.transform.Find("SafeArea");var crystal=safe.GetComponent<Crystal.SafeArea>();
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldScale=canvas.scaleFactor,oldPlane=canvas.planeDistance;var oldMin=safe.anchorMin;var oldMax=safe.anchorMax;bool oldLayout=layout.enabled,oldCrystal=crystal.enabled;
            var initial=session.Capture();var go=new GameObject("ViewportCheckCamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(session.player.view);camera.enabled=false;go.transform.SetPositionAndRotation(session.player.view.transform.position,session.player.view.transform.rotation);RenderTexture rt=null;
            try
            {
                scaler.enabled=false;layout.enabled=false;crystal.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.01f;
                for(int i=0;i<10;i++){session.TryPick(session.items[i]);DG.Tweening.DOTween.CompleteAll();}yield return null;
                var sizes=new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2400,1080),new Vector2Int(1024,768),new Vector2Int(1280,600)};
                foreach(var size in sizes)
                {
                    rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;camera.aspect=(float)size.x/size.y;
                    var area=size.x==2400?new Rect(100,40,size.x-200,size.y-40):new Rect(0,0,size.x,size.y);
                    canvas.scaleFactor=LabResponsiveLayout.FitScale(area);safe.anchorMin=new Vector2(area.x/size.x,area.y/size.y);safe.anchorMax=new Vector2(area.xMax/size.x,area.yMax/size.y);Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
                    foreach(var button in canvas.GetComponentsInChildren<UnityEngine.UI.Button>())
                    {
                        if(!button.gameObject.activeInHierarchy)continue;var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(safe,button.transform);var r=safe.rect;
                        check(bounds.min.x>=r.xMin-1&&bounds.max.x<=r.xMax+1&&bounds.min.y>=r.yMin-1&&bounds.max.y<=r.yMax+1,"HUD button "+button.name+" stays inside safe area at "+size.x+"x"+size.y);
                    }
                    var hud=canvas.GetComponent<SortingHUD>();var pick=Array.Find(canvas.GetComponentsInChildren<UnityEngine.UI.Button>(),b=>b.name=="PICK");var pickBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(safe,pick.transform);
                    foreach(var button in hud.inventoryButtons)if(button.gameObject.activeInHierarchy){var b=RectTransformUtility.CalculateRelativeRectTransformBounds(safe,button.transform);check(!b.Intersects(pickBounds),"Inventory avoids PICK at "+size.x+"x"+size.y);}
                    var settings=canvas.GetComponent<SettingsTutorialPanel>();settings.OpenSettings();Canvas.ForceUpdateCanvases();var card=RectTransformUtility.CalculateRelativeRectTransformBounds(safe,settings.root.transform.Find("Card"));check(card.min.y>=safe.rect.yMin-1&&card.max.y<=safe.rect.yMax+1,"Settings card fits viewport "+size.x+"x"+size.y);settings.Close();
                    camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes("Logs/Viewport-"+size.x+"x"+size.y+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=old;camera.targetTexture=null;rt.Release();UnityEngine.Object.Destroy(rt);rt=null;
                }
            }
            finally{session.Restore(initial);canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;canvas.scaleFactor=oldScale;safe.anchorMin=oldMin;safe.anchorMax=oldMax;crystal.enabled=oldCrystal;scaler.enabled=true;layout.enabled=oldLayout;if(rt){rt.Release();UnityEngine.Object.Destroy(rt);}UnityEngine.Object.Destroy(go);}
        }
    }
}
