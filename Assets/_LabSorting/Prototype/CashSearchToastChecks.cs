#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class CashSearchToastChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("REQUESTED FIX: "+text);Debug.Log("REQUESTED FIX PASS: "+text);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();
            while(!s.Initialized)yield return null;
            s.player.SendMessage("OnApplicationFocus",true);s.player.SetNetworkBlocked(false);
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();
            var panel=hud.GetComponent<SupplyPanel>();var menus=hud.GetComponent<DesignerMenus>();
            var original=s.Capture();s.player.SetBlocked(false);
            try
            {
                foreach(var kind in new[]{SupplyKind.Search,SupplyKind.Magnet,SupplyKind.Slot})
                {
                    var data=s.Capture();data.cash=0;data.capacity=10;data.slotCredits=0;s.Restore(data);
                    panel.Open(kind);Check(panel.cashButton.interactable,"Low-cash button remains clickable: "+kind);
                    panel.cashButton.onClick.Invoke();
                    Check(!panel.IsOpen&&menus.storeRoot.activeSelf&&s.player.blocked,"Low cash opens IAP Store: "+kind);
                    Check(s.Cash==0,"No debit on store navigation: "+kind);menus.Close();
                    Check(!s.player.blocked,"Closing Store restores controls");
                    data=s.Capture();data.cash=1000;s.Restore(data);
                    int before=kind==SupplyKind.Slot?s.capacity:s.Owned(kind),price=s.OfferPrice(kind),amount=s.OfferAmount(kind);
                    panel.Open(kind);panel.cashButton.onClick.Invoke();
                    Check(s.Cash==1000-price&&(kind==SupplyKind.Slot?s.capacity:s.Owned(kind))==before+amount,"Cash offer charges and grants once: "+kind);panel.Close();
                }
                s.Restore(original);s.player.SetBlocked(false);
                while(s.carried.Count<s.capacity){s.TryPick(s.items.First(i=>i.location==ItemLocation.Loose&&!i.Moving));DG.Tweening.DOTween.CompleteAll();}
                var extra=s.items.First(i=>i.location==ItemLocation.Loose&&!i.Moving);
                int version=s.FullNoticeVersion,cash=s.Cash,credits=s.MagnetCredits;
                s.TryPick(extra);hud.SendMessage("Update");yield return null;
                var toast=hud.GetComponent<DesignerToasts>();
                float firstToastDeadline=Time.realtimeSinceStartup+.5f;
                while(toast.ShownText!="Hands full! Place or drop an item."&&Time.realtimeSinceStartup<firstToastDeadline)yield return null;
                Check(toast.ShownText=="Hands full! Place or drop an item.","Short full-hands toast appears");
                yield return new WaitForSecondsRealtime(2.7f);
                s.TryPick(extra);hud.SendMessage("Update");
                // The toast animates in LateUpdate; a coroutine can resume before that
                // frame's LateUpdate in batch mode. Wait for visibility with a bound.
                float toastDeadline=Time.realtimeSinceStartup+.5f;
                while((toast.ShownText!="Hands full! Place or drop an item."||toast.toast.alpha<=0)&&Time.realtimeSinceStartup<toastDeadline)yield return null;
                Check(toast.ShownText=="Hands full! Place or drop an item."&&toast.toast.alpha>0,"Identical toast appears again after expiry");
                s.TryPick(extra);s.TryMagnet();
                Check(s.FullNoticeVersion==version+4&&s.Cash==cash&&s.MagnetCredits==credits,"Every repeated click notifies without charging");
                var q=new ToastPriority();q.Push("Hands full!",2,0,2.5f);q.Push("Hands full!",2,2,2.5f,true);q.Tick(3);
                Check(q.Text=="Hands full!"&&q.Until==4.5f,"Repeated warning restarts its timer");
                s.Restore(original);s.player.SetBlocked(false);
                var first=s.items.First(i=>i.location==ItemLocation.Loose&&!i.Moving&&s.ItemsOfType(i.definition).Count(x=>x.location==ItemLocation.Loose)>1);
                s.TryPick(first);DG.Tweening.DOTween.CompleteAll();s.searchDuration=1;
                Check(s.TrySearch()&&s.SearchSeconds>=29.9f,"Search clamps old durations to at least 30 seconds");
                var match=s.ItemsOfType(first.definition).First(i=>i.location==ItemLocation.Loose);
                match.Highlight(true);match.Highlight(false);yield return null;yield return null;
                Check(match.Searched&&match.outline.enabled&&!match.visual.forceRenderingOff,"Search outline survives hover exit and batching");
                yield return new WaitForSecondsRealtime(28);
                Check(s.SearchType&&match.outline.enabled,"Search remains outlined near the end of 30 seconds");
                yield return new WaitForSecondsRealtime(2.2f);
                Check(!s.SearchType&&!match.Searched,"Search expires and clears outlines");
                var sound=hud.GetComponent<LabSoundscape>();bool music=sound.MusicEnabled,enabled=sound.feedback.SoundEnabled;
                sound.SetMusic(true);sound.feedback.SetSound(true,false);sound.SendMessage("OnApplicationFocus",true);sound.SendMessage("OnApplicationPause",false);
                yield return new WaitForSecondsRealtime(3);
                Check(sound.music.isPlaying&&sound.music.clip.name=="LabMusic"&&Mathf.Abs(sound.music.volume-sound.musicVolume)<.01f,"LabMusic reaches the authored music setting");
                sound.SetMusic(music);sound.feedback.SetSound(enabled,false);
                ToastSliderChecks.Run(hud);
            }
            finally{if(panel.IsOpen)panel.Close();if(menus.IsOpen)menus.Close();s.Restore(original);s.searchDuration=30;}
        }
    }
}
#endif
