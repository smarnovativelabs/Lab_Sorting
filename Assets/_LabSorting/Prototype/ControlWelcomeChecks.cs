#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using LabSorting.UI;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class ControlWelcomeChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("CONTROL WELCOME: "+text);Debug.Log("CONTROL WELCOME PASS: "+text);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var settings=UnityEngine.Object.FindFirstObjectByType<SettingsTutorialPanel>();var sizes=settings.controlSizes;var welcome=settings.GetComponent<WelcomeBackSummary>();var service=welcome.presentation;
            var snapshot=s.Capture();string prefix=settings.preferencesPrefix,saveName=s.saveName;bool persistence=s.persistence;string testName="welcome-test-"+Guid.NewGuid().ToString("N")+".json";
            var loadedProperty=typeof(SortingSession).GetProperty("LoadedAtStartup");bool loaded=s.LoadedAtStartup;
            var reset=typeof(WelcomeBackSummary).GetMethod("ResetLaunch",BindingFlags.Static|BindingFlags.NonPublic);
            settings.preferencesPrefix="LabSorting.ControlSizeTest.";
            try
            {
                s.player.SendMessage("OnApplicationFocus",true);welcome.SendMessage("OnApplicationFocus",true);
                settings.ChangeJoystickSize(99);settings.ChangeActionSize(-99);Check(sizes.JoystickScale==1.15f&&sizes.ActionScale==.85f,"Both control-size bounds enforced");
                settings.ChangeJoystickSize(float.NaN);settings.ChangeActionSize(float.PositiveInfinity);Check(sizes.JoystickScale==1&&sizes.ActionScale==1,"Invalid settings reset safely");
                settings.ChangeJoystickSize(.9f);settings.ChangeActionSize(1.1f);settings.LoadPreferences();Check(sizes.JoystickScale==.9f&&sizes.ActionScale==1.1f,"Separate size preferences persist");
                var action=sizes.actions[0];Vector3 expected=action.localScale;yield return new WaitForSecondsRealtime(.2f);Check(Vector3.Distance(expected,action.localScale)<.001f,"Button animation preserves selected size");
                settings.ResetSettings();Check(sizes.JoystickScale==1&&sizes.ActionScale==1,"Reset settings restores both sizes");
                settings.ChangeJoystickSize(1.15f);settings.ChangeActionSize(1.15f);
                yield return DesignerLayoutChecks.Run(s);
                settings.OpenSettings();yield return new WaitForSecondsRealtime(.3f);SortingSmokeChecks.Capture(s,"Logs/ControlSizeSettings.png");settings.Close();
                reset.Invoke(null,null);loadedProperty.SetValue(s,false);welcome.QueueSavedProgress();yield return new WaitForSecondsRealtime(1);Check(!welcome.Shown,"Fresh game does not show welcome-back summary");
                var item=s.items.First(i=>i.location==ItemLocation.Loose);Check(s.TryPick(item),"Pick item for saved return");DG.Tweening.DOTween.CompleteAll();Check(s.TryPlace(s.racks.First(r=>r.CanAccept(item.definition))),"Sort item for saved return");DG.Tweening.DOTween.CompleteAll();
                s.saveName=testName;s.persistence=true;s.Save();Check(s.FlushSaves(),"Isolated return save is durable");s.persistence=false;s.Restore(snapshot);bool restored=s.Load();Check(restored&&s.SortedCount==1,"Return loads actual sorted progress from disk");loadedProperty.SetValue(s,restored);
                reset.Invoke(null,null);s.player.SetBlocked(true);welcome.QueueSavedProgress();yield return new WaitForSecondsRealtime(1);Check(!welcome.Shown,"Summary waits behind modal/network control block");s.player.SetBlocked(false);
                welcome.SendMessage("OnApplicationPause",true);yield return new WaitForSecondsRealtime(1);Check(!welcome.Shown,"Summary waits while backgrounded");welcome.SendMessage("OnApplicationPause",false);
                yield return new WaitForSecondsRealtime(1.1f);Check(welcome.Shown&&!s.player.blocked&&service.toastText.text.Contains("1 sorted")&&service.toastText.text.Contains("4031 remaining"),"One nonblocking welcome summary uses restored counts");
                SortingSmokeChecks.Capture(s,"Logs/WelcomeBackSummary.png");service.ShowToast("Test notification");welcome.SendMessage("OnApplicationPause",true);welcome.SendMessage("OnApplicationPause",false);welcome.enabled=false;welcome.enabled=true;welcome.QueueSavedProgress();yield return new WaitForSecondsRealtime(1);Check(service.toastText.text=="Test notification","Resume, re-enable and repeat queue cannot show summary again");
            }
            finally
            {
                if(settings.IsOpen)settings.Close();s.FlushSaves();s.persistence=false;s.Restore(snapshot);s.InitializeSaveCache();s.saveName=saveName;s.persistence=persistence;loadedProperty.SetValue(s,loaded);s.player.SetBlocked(false);
                foreach(string key in new[]{"JoystickSize","ActionSize","LookSensitivity","MoveSpeed","Sound","Haptics"})PlayerPrefs.DeleteKey(settings.preferencesPrefix+key);settings.preferencesPrefix=prefix;settings.LoadPreferences();
                foreach(string suffix in new[]{"",".bak",".tmp"}){string path=Path.Combine(Application.persistentDataPath,testName)+suffix;if(File.Exists(path))File.Delete(path);}
            }
        }
    }
}
#endif
