#if UNITY_EDITOR
using System;
using UnityEngine;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class ToastSliderChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("TOAST/SLIDER: "+message);Debug.Log("TOAST/SLIDER PASS: "+message);}
        public static void Run(SortingHUD hud)
        {
            var q=new ToastPriority();q.Push("Picked item",0,0,2);q.Push("Save delayed",3,.1f,4);
            for(int i=0;i<100;i++)q.Push("Placed "+i,0,.2f+i*.01f,2);
            Check(q.Text=="Save delayed"&&Mathf.Approximately(q.Until,4.1f),"Rapid actions cannot replace or extend critical feedback");
            q.Push("Save delayed",3,2,4);Check(Mathf.Approximately(q.Until,4.1f),"Duplicate does not restart timer");
            q.Push("Purchase successful",2,2,4);q.Push("Purchase successful",2,2.1f,4);q.Tick(4.2f);Check(q.Text=="Purchase successful","Important pending message appears after protected message");q.Tick(8.3f);Check(q.Text=="","No stale routine actions or duplicate messages replay");
            q.Push("Picked",0,9,2);q.Push("Inventory full",2,9.1f,4);Check(q.Text=="Inventory full","Error interrupts routine feedback");
            Check(ToastPriority.Classify("Save delayed",true)==3&&ToastPriority.Classify("Received 50 Cash")==2,"Save and delivery messages classified as important");
            q=new ToastPriority();q.Push("Purchase pending",2,0,4);q.Supersede("Purchase",1);q.Push("Purchase delivered",2,1,4);Check(q.Text=="Purchase delivered","Final transaction result supersedes outdated pending state");q.Push("Checking connection...",0,2,4);Check(q.Text=="Purchase delivered","Routine connection checks cannot replace purchase result");
            foreach(var slider in hud.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
            {
                if(!slider.gameObject.activeInHierarchy)continue;
                var trackImage=slider.transform.Find("Track").GetComponent<UnityEngine.UI.Image>();if(trackImage.preserveAspect)Check(Mathf.Abs(trackImage.rectTransform.rect.width/trackImage.rectTransform.rect.height-trackImage.sprite.rect.width/trackImage.sprite.rect.height)<.01f,"Track uses native aspect so fill stays inside: "+slider.name);
                Check(((RectTransform)slider.transform.Find("Track")).rect.height>=24&&slider.fillRect,"Readable track and value fill: "+slider.name);
                float old=slider.value;foreach(float value in new[]{slider.minValue,(slider.minValue+slider.maxValue)*.5f,slider.maxValue}){slider.SetValueWithoutNotify(value);Canvas.ForceUpdateCanvases();Check(Mathf.Abs(slider.fillRect.anchorMax.x-slider.normalizedValue)<.001f,"Fill follows value: "+slider.name);}slider.SetValueWithoutNotify(old);
            }
            var supply=hud.GetComponent<SupplyPanel>();var card=(RectTransform)supply.title.transform.parent;
            var settings=hud.GetComponent<SettingsTutorialPanel>();Check(!settings.joystickSizeSlider.gameObject.activeSelf&&!settings.actionSizeSlider.gameObject.activeSelf,"Size sliders removed from settings UI");
            var restore=settings.settingsContent.transform.Find("RestorePurchases").GetComponent<UnityEngine.UI.Button>();Check(restore.onClick.GetPersistentEventCount()==1&&restore.onClick.GetPersistentMethodName(0)=="Restore","Restore Purchases connected to existing restoration flow");
            var reference=hud.GetComponent<SettingsReferenceArt>();Check(reference&&reference.settingsSprite.name=="SettingsReferencePanel"&&reference.image.useSpriteMesh,"Actual settings reference sprite uses tight mesh");
            Check(Mathf.Abs(supply.title.rectTransform.anchoredPosition.y-card.rect.height*.38f)<1&&!card.Find("DesignerTitle_Title").gameObject.activeSelf,"Offer heading centered inside embedded title art");
            foreach(var label in new[]{supply.cashLabel,supply.adLabel})Check(label.alignment==TextAnchor.MiddleCenter&&label.alignByGeometry&&label.rectTransform.anchorMin==Vector2.zero&&label.rectTransform.anchorMax==Vector2.one,"Offer button lettering centered with inset padding");
        }
    }
}
#endif
