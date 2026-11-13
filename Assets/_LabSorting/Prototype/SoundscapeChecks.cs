#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using LabSorting.UI;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class SoundscapeChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("AUDIO: "+message);Debug.Log("AUDIO PASS: "+message);}
        public static IEnumerator Run(SortingSession session)
        {
            var sound=UnityEngine.Object.FindFirstObjectByType<LabSoundscape>();var feedback=sound.feedback;bool enabled=feedback.SoundEnabled;
            var settings=UnityEngine.Object.FindFirstObjectByType<SettingsTutorialPanel>();string prefix=settings.preferencesPrefix;settings.preferencesPrefix="LabSorting.SpeedTest.";
            try
            {
                // Editor screenshot/CLI activity can remove Game View focus. Exercise
                // foreground audio explicitly instead of depending on desktop focus.
                sound.SendMessage("OnApplicationFocus",true);
                Check(settings.moveSpeedSlider&&settings.moveSpeedLabel,"Movement speed slider and label wired");
                float look=session.player.look.SensitivityMultiplier;settings.ChangeMoveSpeed(99);Check(Mathf.Abs(session.player.character.MaxStableMoveSpeed-3.84f)<.001f,"Speed upper bound stays comfortable");settings.ChangeMoveSpeed(-1);Check(Mathf.Abs(session.player.character.MaxStableMoveSpeed-2.4f)<.001f,"Speed lower bound enforced");settings.ChangeMoveSpeed(float.NaN);Check(settings.MoveSpeedMultiplier==1,"Invalid speed resets to normal");settings.ChangeMoveSpeed(.9f);settings.LoadPreferences();Check(settings.MoveSpeedMultiplier==.9f,"Movement speed persists independently");settings.ChangeMoveSpeed(1);
                Check(sound.music&&sound.music.loop&&sound.music.clip&&sound.musicVolume<=.1f,"Quiet background music loop wired");
                Check(sound.ambience.loop&&sound.ambience.clip&&sound.click&&sound.close,"Loop and distinct UI cues wired");
                Check(session.items.Select(i=>i.definition).Distinct().All(d=>!Regex.IsMatch(d.displayName,@"\s+\d+$")),"No numeric suffixes remain on item display names");
                Check(session.items.Select(i=>i.definition).Distinct().Select(d=>d.id).Distinct().Count()==46,"All 46 stable item IDs retained");
                feedback.SetSound(true,false);yield return new WaitForSecondsRealtime(.3f);
                Check(sound.ambience.volume>0&&sound.ambience.volume<=sound.gameplayVolume,"Ambience fades in below gameplay level");
                Check(sound.music.isPlaying&&sound.music.volume>0&&sound.music.volume<=sound.musicVolume,"Music fades in softly below effects");
                yield return new WaitForSecondsRealtime(.1f);int count=sound.ClicksPlayed;sound.Click();sound.Click();Check(sound.ClicksPlayed==count+1,"Rapid UI clicks throttled to one voice");
                feedback.SetSound(false,false);yield return new WaitForSecondsRealtime(.05f);count=sound.ClicksPlayed;sound.Click();Check(sound.ClicksPlayed==count&&sound.ambience.volume==0,"Sound toggle mutes ambience and UI");
                feedback.SetSound(true,false);sound.SendMessage("OnApplicationPause",true);yield return new WaitForSecondsRealtime(.05f);Check(sound.ambience.volume==0,"Background silences ambience");sound.SendMessage("OnApplicationPause",false);yield return new WaitForSecondsRealtime(.2f);Check(sound.ambience.volume>0&&sound.ambience.volume<sound.gameplayVolume,"Resume fades ambience in without a jump");
                var clip=sound.ambience.clip;var data=new float[clip.samples*clip.channels];Check(clip.GetData(data,0),"Ambient PCM data readable");
                Check(Mathf.Abs(data[0]-data[data.Length-1])<.01f&&data.Max(Mathf.Abs)<.3f,"Ambient loop seam and peak stay soft");
                sound.tour.SamplePose(sound.tour.duration+.2f,out var p,out var q);var last=sound.tour.shots.Last();Check(Vector3.Distance(p,last.position)<.001f&&Quaternion.Angle(q,last.rotation)<.01f,"End of tour holds last pose instead of rotating to player");
            }
            finally{PlayerPrefs.DeleteKey(settings.preferencesPrefix+"MoveSpeed");settings.preferencesPrefix=prefix;settings.LoadPreferences();sound.SendMessage("OnApplicationPause",false);sound.SendMessage("OnApplicationFocus",Application.isFocused);feedback.SetSound(enabled,false);}
        }
    }
}
#endif
