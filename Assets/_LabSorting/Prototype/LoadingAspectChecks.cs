#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using LabSorting.Services;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class LoadingAspectChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("LOADING UI: "+message);Debug.Log("LOADING UI PASS: "+message);}
        public static IEnumerator Run()
        {
            var startup=UnityEngine.Object.FindFirstObjectByType<LabStartup>();
            var canvas=startup.loadingLabel.GetComponentInParent<Canvas>();
            var scaler=canvas.GetComponent<CanvasScaler>();
            var camera=new GameObject("Loading capture",typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float plane=canvas.planeDistance,scale=canvas.scaleFactor;
            bool scalerEnabled=scaler.enabled;
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;scaler.enabled=false;
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1440,720),new Vector2Int(1560,720),new Vector2Int(1600,720),new Vector2Int(1024,768)})
                {
                    var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;
                    try
                    {
                        canvas.scaleFactor=Mathf.Min(size.x/scaler.referenceResolution.x,size.y/scaler.referenceResolution.y);
                        Canvas.ForceUpdateCanvases();yield return null;
                        var originalRect=startup.loadingFill.rectTransform.rect;
                        foreach(float progress in new[]{0f,.01f,.25f,.5f,1f})
                        {
                            startup.SetLoadingProgress(progress);Canvas.ForceUpdateCanvases();
                            Check(startup.loadingFill.rectTransform.rect==originalRect,"Progress never squeezes fill geometry "+progress+" at "+size);
                            Check(Mathf.Approximately(startup.loadingFill.fillAmount,progress),"Correct visible progress");
                        }
                        foreach(string name in new[]{"DesignerLoadingLogo","DesignerLoadingTrack"})
                        {
                            var image=canvas.transform.Find(name).GetComponent<Image>();var rect=image.rectTransform;
                            Check(Mathf.Abs(rect.rect.width/rect.rect.height-image.sprite.rect.width/image.sprite.rect.height)<.01f,"Artwork aspect preserved: "+name);
                            var corners=new Vector3[4];rect.GetWorldCorners(corners);
                            var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                            Check(min.x>=32&&max.x<=size.x-32&&min.y>=20&&max.y<=size.y-20,"Loading content inside inset screen: "+name);
                        }
                        startup.SetLoadingProgress(.5f);yield return null;camera.Render();
                        var old=RenderTexture.active;RenderTexture.active=rt;
                        var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                        texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();
                        File.WriteAllBytes("Logs/LoadingAspect-"+size.x+".png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);RenderTexture.active=old;
                    }
                    finally{camera.targetTexture=null;UnityEngine.Object.Destroy(rt);}
                }
                var button=startup.connectionRoot.GetComponentInChildren<Button>(true);
                button.gameObject.SetActive(true);startup.connectionRoot.SetActive(true);yield return null;
                var motion=button.GetComponent<UIButtonMotion>();Check(motion,"Button feedback wired");
                var original=button.transform.localScale;motion.OnPointerDown(null);yield return new WaitForSecondsRealtime(.12f);
                Check(button.transform.localScale.x<original.x,"Press gives subtle feedback");
                Check(Mathf.Approximately(button.transform.localScale.x,button.transform.localScale.y),"Press remains uniform");
                motion.OnPointerUp(null);yield return new WaitForSecondsRealtime(.3f);
                Check((button.transform.localScale-original).sqrMagnitude<.00001f,"Release restores button");
                startup.connectionRoot.SetActive(false);
            }
            finally{startup.SetLoadingProgress(0);canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=plane;canvas.scaleFactor=scale;scaler.enabled=scalerEnabled;UnityEngine.Object.Destroy(camera.gameObject);}
        }
    }
}
#endif
