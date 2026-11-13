#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using LabSorting.Services;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class RewardReturnChecks
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static void Check(bool ok,string message){if(!ok)throw new Exception("REWARD RETURN: "+message);Debug.Log("REWARD RETURN PASS: "+message);}
        static void Set(SupplyCommerce c,string field,object value)=>typeof(SupplyCommerce).GetField(field,Private).SetValue(c,value);
        static void Call(SupplyCommerce c,string method,params object[] args)=>typeof(SupplyCommerce).GetMethod(method,Private).Invoke(c,args);
        static int Owned(SortingSession s,SupplyKind k)=>k==SupplyKind.Cash?s.Cash:k==SupplyKind.Search?s.SearchCredits:k==SupplyKind.Magnet?s.MagnetCredits:s.capacity;
        public static IEnumerator Run()
        {
            var c=LabServiceRoot.Instance.commerce;var s=c.session;var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var menus=hud.GetComponent<DesignerMenus>();var panel=hud.GetComponent<SupplyPanel>();
            var initial=s.Capture();bool persistence=s.persistence;string name=s.saveName;float clock=Time.timeScale;
            s.saveName="reward-return-test-"+Guid.NewGuid().ToString("N")+".json";string path=s.SavePath;s.persistence=true;
            string id=c.settings.androidRewardedId;
            try
            {
                Check(s.items.Length==4032,"Production item count is exercised");
                foreach(SupplyKind kind in new[]{SupplyKind.Cash,SupplyKind.Search,SupplyKind.Magnet,SupplyKind.Slot})
                {
                    if(kind==SupplyKind.Cash)menus.OpenStore();else panel.Open(kind);
                    yield return new WaitForSecondsRealtime(.3f);
                    int before=Owned(s,kind),amount=c.AdAmount(kind);string token="ad:test-"+Guid.NewGuid().ToString("N");
                    Set(c,"rewardToken",token);Set(c,"rewardKind",kind);Set(c,"rewardAmount",amount);Set(c,"rewardGranted",false);Set(c,"busy",true);
                    s.player.SetMove(Vector2.one);s.player.SendMessage("OnApplicationFocus",false);c.SendMessage("OnApplicationPause",true);
                    Time.timeScale=0;
                    // Exercise hidden-before-reward and duplicate SDK callback ordering without a live ad.
                    Call(c,"OnAdHidden",id,null);Call(c,"OnAdHidden",id,null);
                    Call(c,"OnReward",id,new MaxSdkBase.Reward(),null);Call(c,"OnReward",id,new MaxSdkBase.Reward(),null);
                    c.SendMessage("OnApplicationPause",false);c.SendMessage("OnApplicationFocus",true);s.player.SendMessage("OnApplicationFocus",true);
                    yield return new WaitForSecondsRealtime(2.3f);
                    Check(!c.Busy&&Owned(s,kind)==before+amount,"Paused-clock return grants exactly one "+kind+" reward");
                    Check(s.player.blocked&&s.player.Move==Vector2.zero,"Return keeps original offer/store modal and clears stale movement");
                    Check(s.Load()&&Owned(s,kind)==before+amount,"Reward survives disk reload: "+kind);
                    Check(s.GrantSupply(kind,amount,token)&&Owned(s,kind)==before+amount,"Receipt remains deduplicated after reload");
                    if(menus.IsOpen)menus.Close();if(panel.IsOpen)panel.Close();Check(!s.player.blocked,"Closing returned panel releases gameplay");
                    Time.timeScale=clock;
                }
                panel.OpenSearch(); // Uses owned credit, so explicitly open offers for the cancellation case.
                if(!panel.IsOpen)panel.SearchOffers();
                int cash=s.Cash;
                Set(c,"rewardToken","ad:cancel-test");Set(c,"rewardGranted",false);Set(c,"busy",true);
                Call(c,"OnAdHidden",id,null);Time.timeScale=0;yield return new WaitForSecondsRealtime(2.3f);
                Check(!c.Busy&&s.Cash==cash&&c.Status.Contains("requires completion"),"Early close releases busy state without a cash reward");panel.Close();Check(!s.player.blocked,"Early close allows leaving offer panel");
                panel.SearchOffers();Set(c,"rewardToken","ad:failed-test");Set(c,"rewardGranted",false);Set(c,"busy",true);
                Call(c,"OnAdFailed",id,null,null);Check(!c.Busy&&s.Cash==cash,"Display failure clears busy state without spending cash");panel.Close();Check(!s.player.blocked,"Display failure restores usable controls");
                Set(c,"rewardToken","ad:countdown-test");Call(c,"BeginAdCountdown",true);c.SendMessage("OnApplicationFocus",false);
                Check(!c.Busy&&c.AdCountdownSeconds==0,"Focus loss cancels countdown without launching a video");c.SendMessage("OnApplicationFocus",true);
            }
            finally
            {
                Time.timeScale=clock;c.CancelInvoke();Set(c,"busy",false);Set(c,"rewardToken",null);Set(c,"rewardGranted",false);
                if(menus.IsOpen)menus.Close();if(panel.IsOpen)panel.Close();s.FlushSaves();s.persistence=false;s.Restore(initial);s.saveName=name;s.persistence=persistence;
                foreach(string suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);
            }
        }
    }
}
#endif
