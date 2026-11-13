#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using LabSorting.UI;
using LabSorting.Sorting;
using LabSorting.Services;
namespace LabSorting.Prototype
{
    public static class DesignerUIChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("DESIGNER UI: "+text);Debug.Log("DESIGNER UI PASS: "+text);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var saved=s.Capture();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var menus=hud.GetComponent<DesignerMenus>();var settings=hud.GetComponent<SettingsTutorialPanel>();var progress=hud.GetComponent<ItemsProgressPanel>();var supply=hud.GetComponent<SupplyPanel>();
            Check(s.items.Length==4032&&menus&&hud.designerSkin,"Designer skin wired with 4032 items");
            StoreReferenceChecks.Run(menus);
            Check(hud.progress.font.name=="Onlygraphic Bold"&&hud.active.font.name=="Onlygraphic Rounded","Requested heading/body fonts assigned");
            Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1&&hud.GetComponent<UnityEngine.UI.GraphicRaycaster>(),"Exactly one EventSystem and active canvas raycaster");
            foreach(var im in hud.GetComponentsInChildren<UnityEngine.UI.Image>(true))if(im.sprite&&im.sprite.name.StartsWith("LabUI_"))Check(im.type==UnityEngine.UI.Image.Type.Sliced||im.preserveAspect,"Artwork uses aspect preservation or bordered slicing: "+im.name);
            try
            {
                yield return new WaitForSecondsRealtime(.3f);settings.OpenSettings();yield return null;
                try{ToastSliderChecks.Run(hud);}finally{settings.Close();}
                SortingSmokeChecks.Capture(s,"Logs/DesignerUI-HUD.png");
                settings.openButton.onClick.Invoke();Check(menus.pauseRoot.activeSelf&&s.player.blocked,"Pause button opens designer pause and blocks sorting");yield return new WaitForSecondsRealtime(.3f);SortingSmokeChecks.Capture(s,"Logs/DesignerUI-Pause.png");
                menus.Settings();Check(settings.IsOpen&&!menus.IsOpen&&s.player.blocked,"Pause to settings keeps one modal");yield return new WaitForSecondsRealtime(.3f);SortingSmokeChecks.Capture(s,"Logs/DesignerUI-Settings.png");settings.Close();Check(!s.player.blocked,"Settings close restores gameplay");
                menus.OpenStore();yield return new WaitForSecondsRealtime(.3f);Check(menus.storeRoot.activeSelf&&menus.purchases.Length==6,"Six store products wired");Check(menus.purchases.All(b=>b.onClick.GetPersistentEventCount()==1),"All purchase buttons retain serialized actions");Check(menus.prices.All(t=>t.text=="Unavailable"||t.text==LabLocalization.Text("Unavailable"))&&menus.purchases.All(b=>!b.interactable),"Unconfigured store cannot display invented prices or charge: "+string.Join(",",menus.prices.Select(t=>t.text))+" buttons="+string.Join(",",menus.purchases.Select(b=>b.interactable)));SortingSmokeChecks.Capture(s,"Logs/DesignerUI-Store.png");yield return StoreTouchChecks.Run(menus);menus.Close();Check(!s.player.blocked,"Store close restores gameplay");
                progress.Open();yield return new WaitForSecondsRealtime(.25f);SortingSmokeChecks.Capture(s,"Logs/DesignerUI-Items.png");progress.Close();
                foreach(var kind in new[]{SupplyKind.Search,SupplyKind.Magnet,SupplyKind.Slot,SupplyKind.Cash}){supply.Open(kind);yield return new WaitForSecondsRealtime(.3f);Check(supply.IsOpen&&s.player.blocked,"Offer opens: "+kind);SortingSmokeChecks.Capture(s,"Logs/DesignerUI-"+kind+".png");supply.Close();}
                foreach(var item in s.items.GroupBy(i=>i.definition).Take(10).Select(g=>g.First())){Check(s.TryPick(item),"Inventory fixture pickup");DG.Tweening.DOTween.CompleteAll();}yield return new WaitForSecondsRealtime(.3f);SortingSmokeChecks.Capture(s,"Logs/DesignerUI-Inventory.png");
                                var scroll=hud.inventoryButtons[0].GetComponentInParent<UnityEngine.UI.ScrollRect>();scroll.verticalNormalizedPosition=0;yield return null;hud.inventoryButtons[0].onClick.Invoke();yield return null;Check(s.Active.definition==s.carried[0].definition,"Collected row selects its actual item type");hud.inventoryButtons[9].onClick.Invoke();yield return null;Check(s.carried.Count==10,"Scrolling and switching preserve inventory");SortingSmokeChecks.Capture(s,"Logs/DesignerUI-InventoryEnd.png");
                yield return (IEnumerator)typeof(DevicePerformanceChecks).GetMethod("Ratios",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{s});
                                Check(hud.inventoryButtons.Length==30&&hud.inventoryButtons[0].GetComponentInParent<UnityEngine.UI.ScrollRect>()&&hud.inventorySummary.text.Contains("10 /"),"Collected inventory uses scrollable icon/name/count list");
                var empty=JsonUtility.FromJson<SortingSession.Snapshot>(JsonUtility.ToJson(saved));empty.searchCredits=0;empty.magnetCredits=0;s.Restore(empty);supply.OpenSearch();Check(supply.IsOpen,"Empty search opens cash/ad offers");supply.Close();supply.OpenMagnet();Check(supply.IsOpen,"Empty magnet opens cash/ad offers");supply.Close();
                                var withCredits=s.Capture();withCredits.searchCredits=1;withCredits.magnetCredits=1;Check(s.Restore(withCredits),"Tool-credit fixture restores");Check(s.TryPick(s.items[0]),"Tool fixture selected item");DG.Tweening.DOTween.CompleteAll();int beforeCash=s.Cash;supply.OpenSearch();Check(!supply.IsOpen&&s.SearchType&&s.SearchCredits==0&&s.Cash==beforeCash,"Owned search uses directly without opening offers or charging cash");supply.SearchOffers();Check(supply.IsOpen,"Search plus opens offers even with active search");supply.Close();supply.OpenMagnet();Check(!supply.IsOpen&&s.carried.Count>1&&s.MagnetCredits==0&&s.Cash==beforeCash,"Owned magnet brings actual items without opening offers or charging cash");DG.Tweening.DOTween.CompleteAll();s.Restore(saved);
                var c=LabServiceRoot.Instance.commerce;Check(!c.Watch(SupplyKind.Magnet)&&c.AdCountdownSeconds==0,"Unavailable ad never starts fake countdown");
                // Exercise the countdown lifecycle without a live SDK; readiness is rechecked before display.
                typeof(SupplyCommerce).GetMethod("BeginAdCountdown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{false});yield return null;Check(c.Busy&&c.AdCountdownSeconds>0,"Interstitial countdown reserves ad request once");c.SendMessage("OnApplicationPause",true);Check(!c.Busy&&c.AdCountdownSeconds==0,"Background cancels pending ad without reward");c.SendMessage("OnApplicationPause",false);
                typeof(SupplyCommerce).GetMethod("BeginAdCountdown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{true});yield return new WaitForSecondsRealtime(2.2f);Check(!c.Busy&&c.AdCountdownSeconds==0&&s.Cash==saved.cash,"Unavailable-at-display ad releases busy state without reward");
                Check(s.Cash==saved.cash,"Visual menu checks never modify cash");
                yield return DesignerLayoutChecks.Run(s);
                yield return SoundscapeChecks.Run(s);
                yield return DescriptiveNameChecks.Run(s);
                yield return RackLabelIconChecks.Run(s);
            }
            finally {if(menus.IsOpen)menus.Close();if(settings.IsOpen)settings.Close();if(progress.IsOpen)progress.Close();if(supply.IsOpen)supply.Close();s.Restore(saved);}
        }
    }
}
#endif
