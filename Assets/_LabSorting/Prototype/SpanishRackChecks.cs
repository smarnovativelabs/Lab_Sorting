#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using LabSorting.Services;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class SpanishRackChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("SPANISH RACK: "+message);Debug.Log("SPANISH RACK PASS: "+message);}
        static readonly List<string> clipping=new List<string>();
        static void Measure(string panel)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var t in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if(!t.isActiveAndEnabled||string.IsNullOrWhiteSpace(t.text)||!t.font)continue;
                var rect=t.rectTransform.rect;var settings=t.GetGenerationSettings(rect.size);
                settings.resizeTextForBestFit=false;settings.fontSize=t.resizeTextForBestFit?t.resizeTextMinSize:t.fontSize;
                float height=t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text,settings)/t.pixelsPerUnit;
                if(height>rect.height+2)clipping.Add(panel+" | "+t.name+" | "+rect.size+" needed="+height+" | "+t.text.Replace('\n',' '));
            }
        }
        static IEnumerator Ratios(SortingSession s,Action<string> measure)
        {
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var canvas=hud.GetComponent<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();var safe=hud.GetComponentInChildren<SafeAreaPanel>();var camera=s.player.view;
            var mode=canvas.renderMode;var world=canvas.worldCamera;var target=camera.targetTexture;float scale=canvas.scaleFactor,plane=canvas.planeDistance;bool scalerEnabled=scaler.enabled,safeEnabled=safe.enabled;
            try
            {
                scaler.enabled=false;safe.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.01f;
                foreach(int width in new[]{1280,1440,1560,1600})
                {
                    var rt=new RenderTexture(width,720,24);camera.targetTexture=rt;
                    try
                    {
                        canvas.scaleFactor=Mathf.Pow(2,Mathf.Lerp(Mathf.Log(width/scaler.referenceResolution.x,2),Mathf.Log(720/scaler.referenceResolution.y,2),scaler.matchWidthOrHeight));
                        safe.ApplySample(new Rect(48,24,width-96,696),new Vector2Int(width,720));Canvas.ForceUpdateCanvases();yield return null;yield return null;
                        measure(width+"x720");
                    }
                    finally{camera.targetTexture=target;UnityEngine.Object.Destroy(rt);}
                }
            }
            finally{camera.targetTexture=target;canvas.renderMode=mode;canvas.worldCamera=world;canvas.planeDistance=plane;canvas.scaleFactor=scale;scaler.enabled=scalerEnabled;safe.enabled=safeEnabled;safe.Apply();}
        }
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var snapshot=s.Capture();
            var oldLocale=LocalizationSettings.SelectedLocale;string oldLanguage=PlayerPrefs.GetString("Lab.Language","en");
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var settings=hud.GetComponent<SettingsTutorialPanel>();
            var progress=hud.GetComponent<ItemsProgressPanel>();var supplies=hud.GetComponent<SupplyPanel>();var services=hud.GetComponent<ServicesPanel>();
            clipping.Clear();
            try
            {
                yield return LocalizationSettings.InitializationOperation;
                float startupDeadline=Time.realtimeSinceStartup+15;while(!LabLocalization.Ready&&Time.realtimeSinceStartup<startupDeadline)yield return null;
                LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("es");
                float deadline=Time.realtimeSinceStartup+15;while(LabLocalization.Text("SETTINGS")!="AJUSTES"&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(LabLocalization.Text("SETTINGS")=="AJUSTES","Spanish table loaded");
                var names=new List<string>();
                foreach(var type in s.items.Select(i=>i.definition).Distinct())
                {var translated=LabLocalization.Text(type.displayName);names.Add(type.displayName+" | "+translated);Check(translated!=type.displayName||type.displayName.StartsWith("Radio"),"Item name: "+type.displayName);}
                File.WriteAllLines("Logs/SpanishItemNames.txt",names);
                Check(LabLocalization.Text("SORTED  12 / 4032")=="ORDENADOS  12 / 4032","Dynamic counts retain numbers");
                Check(LabLocalization.Text("PACK +25\nRs 1,100.00").Contains("Rs 1,100.00"),"Store price remains unchanged");
                Check(LabLocalization.Text(s.Message)=="Recoge un objeto y asígnalo a un estante vacío.","Initial gameplay hint fully translated");
                yield return null;yield return null;Measure("HUD");
                settings.OpenSettings();yield return null;yield return null;Measure("Settings");SortingSmokeChecks.Capture(s,"Logs/SpanishSettings.png");
                settings.OpenHowToPlay();for(int i=0;i<4;i++){yield return null;yield return null;Measure("Tutorial"+i);if(i<3)settings.Next();}settings.Close();
                progress.Open();yield return new WaitForSecondsRealtime(.25f);for(int i=0;i<8;i++){yield return null;yield return null;Measure("Items"+i);if(i==0)SortingSmokeChecks.Capture(s,"Logs/SpanishItems.png");if(i==3)yield return Ratios(s,ratio=>Measure("Long item names "+ratio));progress.Next();}progress.Close();
                foreach(var kind in new[]{SupplyKind.Search,SupplyKind.Magnet,SupplyKind.Slot,SupplyKind.Cash}){supplies.Open(kind);yield return null;yield return null;Measure("Supply"+kind);supplies.Close();}
                settings.OpenSettings();services.Open();yield return null;yield return null;Measure("Services");SortingSmokeChecks.Capture(s,"Logs/SpanishServices.png");yield return Ratios(s,ratio=>Measure("Services "+ratio));services.Close();settings.Close();
                yield return (IEnumerator)typeof(DevicePerformanceChecks).GetMethod("Ratios",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{s});
                File.WriteAllLines("Logs/SpanishClipping.txt",clipping.Distinct());
                var rack=s.racks.First(r=>r.SizeBased);var item=s.items.First(i=>rack.CanAccept(i.definition));
                var renderers=rack.GetComponentsInChildren<Renderer>(true).Where(r=>r!=rack.indicator&&!r.GetComponentInParent<SortItem>()&&!r.GetComponent<TextMesh>()).ToArray();
                var materials=renderers.Select(r=>r.sharedMaterials).ToArray();var itemMaterial=item.visual.sharedMaterial;
                rack.Preview(item.definition);Check(renderers.All(r=>r.sharedMaterials.All(m=>m==rack.valid)),"Whole rack valid green");
                Check(item.visual.sharedMaterial==itemMaterial,"Item textures unchanged");
                Check(s.TryPick(item),"Pick preview item");DG.Tweening.DOTween.CompleteAll();
                typeof(SortingSession).GetProperty("TargetRack").SetValue(s,rack);
                var preview=s.GetComponent<RackPlacementPreview>();preview.SendMessage("LateUpdate");
                var expected=rack.SlotPoseFor(item.definition,rack.NextSlot(item.definition));
                Check(preview.Visible&&Vector3.Distance(preview.PreviewPose.position,expected.position)<.0001f,"Ghost uses exact next placement slot");
                var camera=s.player.view;var previousCameraPosition=camera.transform.position;var previousCameraRotation=camera.transform.rotation;
                camera.transform.position=rack.transform.TransformPoint(new Vector3(0,1.6f,-2.5f));camera.transform.LookAt(rack.transform.TransformPoint(new Vector3(0,1.0f,0)));
                rack.Preview(item.definition);preview.SendMessage("LateUpdate");SortingSmokeChecks.Capture(s,"Logs/FullRackGreen.png");
                Check(s.TryPlace(rack),"Place preview item");DG.Tweening.DOTween.CompleteAll();
                Check(Vector3.Distance(item.transform.position,expected.position)<.001f,"Actual placement matches preview");
                var other=s.items.First(i=>i.definition!=item.definition);rack.Preview(other.definition);
                Check(renderers.All(r=>r.sharedMaterials.All(m=>m==rack.invalid)),"Wrong type makes whole rack red");
                SortingSmokeChecks.Capture(s,"Logs/FullRackRed.png");camera.transform.SetPositionAndRotation(previousCameraPosition,previousCameraRotation);
                rack.Preview(null);Check(renderers.Select((r,i)=>r.sharedMaterials.SequenceEqual(materials[i])).All(v=>v),"Original rack materials restored");
                s.player.SetBlocked(true);preview.SendMessage("LateUpdate");Check(!preview.Visible,"Modal hides ghost");s.player.SetBlocked(false);
                LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("en");deadline=Time.realtimeSinceStartup+10;while(LabLocalization.Text("SETTINGS")!="SETTINGS"&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;yield return null;
                Check(LabLocalization.Text("Antibiotic 1")=="Antibiotic 1","English restored without altering definition");
                Check(clipping.Count==0,"No clipped text at minimum readable font; see Logs/SpanishClipping.txt");
                Debug.Log("SPANISH_RACK_CHECKS_OK");
            }
            finally
            {
                services.Close();settings.Close();progress.Close();supplies.Close();s.player.SetBlocked(false);s.Restore(snapshot);
                foreach(var rack in s.racks)rack.Preview(null);
                if(oldLocale)LocalizationSettings.SelectedLocale=oldLocale;PlayerPrefs.SetString("Lab.Language",oldLanguage);PlayerPrefs.Save();
            }
        }
    }
}
#endif
