using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
using UnityEngine;

namespace LabSorting.Prototype
{
    // Plays through the guided tutorial in the real lab scene with test preference keys.
    public static class GuidedTutorialChecks
    {
        static int checks;
        static void Check(bool ok,string message){if(!ok)throw new Exception("TUTORIAL CHECK FAILED: "+message);checks++;Debug.Log("TUTORIAL PASS: "+message);}
        static IEnumerator Wait(float seconds)
        {
            float end=Time.time+seconds,deadline=Time.realtimeSinceStartup+30;
            while(Time.time<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Tutorial player loop stopped");yield return null;}
        }
        static Vector2 Tip(GuidedTutorial g)=>g.area.InverseTransformPoint(g.hand.position);
        // Fingertip on the target control, allowing for the tap animation (the hand moves in and out by 30 px).
        static bool HandNear(GuidedTutorial g,RectTransform target,float margin)
        {
            if(!g.hand.gameObject.activeInHierarchy)return false;
            var local=(Vector2)target.InverseTransformPoint(g.hand.position);var r=target.rect;
            return local.x>=r.xMin-margin&&local.x<=r.xMax+margin&&local.y>=r.yMin-margin&&local.y<=r.yMax+margin;
        }
        static bool HandNearCenter(GuidedTutorial g,float radius)=>g.hand.gameObject.activeInHierarchy&&Vector2.Distance(Tip(g),g.area.InverseTransformPoint(new Vector3(Screen.width*.5f,Screen.height*.5f,0)))<=radius;
        static IEnumerator Teleport(SortingSession s,Vector3 feet,Vector3 lookAt)
        {
            s.player.Clear();s.player.character.Motor.BaseVelocity=Vector3.zero;s.player.character.Motor.SetPositionAndRotation(feet,Quaternion.identity);
            yield return Wait(.15f);s.player.look.SnapToDirection(lookAt-s.player.view.transform.position);yield return Wait(.2f);
        }
        static SortItem NearestLoose(SortingSession s)
        {
            var me=s.player.transform.position;
            return s.items.Where(i=>i.location==ItemLocation.Loose&&!i.Moving&&Mathf.Abs(i.transform.position.y-me.y)<1.5f).OrderBy(i=>(i.transform.position-me).sqrMagnitude).First();
        }
        static SortRack RackFor(SortingSession s)
        {
            var me=s.player.transform.position;var type=s.Active.definition;
            return s.racks.Where(r=>r.CanAccept(type)&&Mathf.Abs(r.transform.position.y-me.y)<1.5f).OrderBy(r=>(r.transform.position-me).sqrMagnitude).First();
        }
        // The spawn sits in a narrow gap between two racks; find a spot and facing where the game allows a safe throw.
        static IEnumerator ThrowSpot(SortingSession s,Vector3 spawn)
        {
            foreach(var forward in new[]{1.2f,.6f,1.8f})foreach(var side in new[]{0f,-1f,1f})
            {
                var feet=spawn+new Vector3(0,0,forward);yield return Teleport(s,feet,feet+new Vector3(side,1.3f,3));
                if(s.Active&&s.TryThrowPosition(s.Active,out _))yield break;
            }
        }
        public static IEnumerator Run()
        {
            checks=0;var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var g=UnityEngine.Object.FindFirstObjectByType<GuidedTutorial>();var settings=g.GetComponent<SettingsTutorialPanel>();
            s.persistence=false;s.player.SendMessage("OnApplicationFocus",true);yield return Wait(.15f);
            var initial=s.Capture();string prefix=g.preferencesPrefix;var spawn=s.player.transform.position;var motor=s.player.character.Motor;
            try
            {
                Check(prefix.Contains("Automated"),"Tutorial uses test preference keys");
                Check(!g.IsRunning&&!g.Completed&&g.group.alpha==0,"Nothing shows while automatic start is off");
                g.Begin(false);yield return Wait(.3f);
                Check(g.IsRunning&&g.Current==GuidedTutorial.Step.Move&&g.group.alpha==1,"Starts at step 1, Move");
                Check(g.Mode==GuidedTutorial.HandMode.Drag&&HandNear(g,g.joystick,120),"Hand drags on the joystick");
                Check(g.message.text.Contains("joystick")&&g.stepLabel.text=="STEP 1 / 5"&&!g.gotItButton.gameObject.activeSelf,"Move text and step counter");

                // Loading a save moves the player in one frame: that must not count as walking.
                motor.SetPosition(spawn+new Vector3(0,0,2.5f));yield return Wait(.2f);motor.SetPosition(spawn);yield return Wait(.2f);
                Check(g.Current==GuidedTutorial.Step.Move,"A teleport (like loading a save) does not complete Move");
                // Walking: the hand gets out of the way while the player moves.
                for(int i=1;i<=5;i++){motor.SetPosition(spawn+new Vector3(0,0,.2f*i));yield return null;yield return null;}
                Check(g.Current==GuidedTutorial.Step.Move&&g.Mode==GuidedTutorial.HandMode.Hidden,"Hand hides while the player is walking");
                yield return Wait(g.idleDelay+.4f);
                Check(g.Mode==GuidedTutorial.HandMode.Drag&&g.hand.gameObject.activeSelf,"Hand comes back after the player stops");
                for(int i=6;i<=10;i++){motor.SetPosition(spawn+new Vector3(0,0,.2f*i));yield return null;yield return null;}
                yield return Wait(.15f);
                Check(g.Current==GuidedTutorial.Step.Look,"Walking 1.5 m completes Move");
                yield return Wait(g.idleDelay+.4f);
                Check(g.Mode==GuidedTutorial.HandMode.Drag&&HandNear(g,g.lookArea,220),"Hand swipes on the look area");
                s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw+60,10));yield return Wait(.2f);
                Check(g.Current==GuidedTutorial.Step.Pick,"Turning 45 degrees completes Look");

                // Not aiming at anything: the hand points at the aim cross and stays there when the camera turns.
                s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw,-75));yield return Wait(.6f);
                Check(!g.actions.PickVisible&&g.Mode==GuidedTutorial.HandMode.Aim&&HandNearCenter(g,60)&&g.message.text.StartsWith("Aim the +"),"Nothing aimed at: hand points at the aim cross");
                var before=Tip(g);s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw+40,-70));yield return Wait(.25f);
                Check(g.Mode==GuidedTutorial.HandMode.Aim&&Vector2.Distance(before,Tip(g))<16,"Hand stays put on screen when the camera turns");
                var item=NearestLoose(s);
                for(int attempt=0;attempt<4&&!g.actions.PickVisible;attempt++)
                {
                    var me=s.player.transform.position;var to=item.transform.position-me;to.y=0;var step=to.magnitude>1.2f?to.normalized*(to.magnitude-1.0f):Vector3.zero;
                    yield return Teleport(s,me+step,item.hitbox.bounds.center);
                }
                yield return Wait(.15f);
                Check(g.actions.PickVisible&&g.Mode==GuidedTutorial.HandMode.Tap&&HandNear(g,g.pickButton,70)&&g.message.text.StartsWith("Tap PICK"),"Aiming at an item moves the hand to PICK");
                Check(s.TryPick(s.TargetItem),"Pick the aimed item");DOTween.CompleteAll();yield return Wait(.3f);

                Check(g.Current==GuidedTutorial.Step.Place,"Picking completes the Pick step"); Check(g.GuidedRack&&g.RackGuideVisible,"Tutorial marks a compatible same-floor rack");
                s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw,-75));yield return Wait(.6f);
                Check(g.Mode==GuidedTutorial.HandMode.Aim&&g.message.text.Contains("rack"),"Not at a rack: hand points at the aim cross and explains the green bar");
                var rack=RackFor(s);var front=rack.transform.TransformPoint(new Vector3(0,0,-1.6f));front.y=rack.transform.position.y+.03f;
                yield return Teleport(s,front,rack.label.transform.position);yield return Wait(.1f);
                Check(s.TargetRack==rack&&g.Mode==GuidedTutorial.HandMode.Tap&&HandNear(g,g.placeButton,70)&&g.message.text.Contains("decides"),"At the rack the hand taps PLACE and explains rack types");
                ScreenCapture.CaptureScreenshot("Logs/SoftTutorialPlace.png");yield return Wait(.1f);
                Check(s.TryPlace(rack),"Place on the rack");DOTween.CompleteAll();yield return Wait(.3f);

                Check(g.Current==GuidedTutorial.Step.Tools&&g.gotItButton.gameObject.activeSelf,"Placing completes the main task; optional tools and GOT IT appear");
                Check(g.Mode==GuidedTutorial.HandMode.Tap&&(HandNear(g,g.searchButton,70)||HandNear(g,g.magnetButton,70)),"Hand shows SEARCH and MAGNET");
                int cash=s.Cash;g.gotItButton.onClick.Invoke();yield return Wait(.2f);
                Check(g.Current==GuidedTutorial.Step.Done&&s.TutorialRewarded&&s.Cash==cash+g.reward,"Finishing pays the reward: +$"+g.reward);
                yield return Wait(.4f);
                Check(g.Completed&&g.message.text.StartsWith("Well done")&&!g.hand.gameObject.activeSelf&&!g.skipButton.gameObject.activeSelf,"Well done message, tutorial remembered as finished");
                yield return Wait(5.4f);
                Check(!g.IsRunning&&g.group.alpha==0,"Message hides after a few seconds");

                // Replay from the start, skipping ahead by doing actions early: never pays twice.
                g.Begin(true);yield return null;Check(g.IsRunning&&g.Current==GuidedTutorial.Step.Move,"Replay starts again at Move");
                Check(s.TryPick(NearestLoose(s)),"Replay: pick");DOTween.CompleteAll();yield return Wait(.2f);
                Check(g.Current==GuidedTutorial.Step.Place,"Picking during Move skips ahead to Place");
                Check(s.TryPlace(RackFor(s)),"Replay: place");DOTween.CompleteAll();yield return Wait(.2f);
                Check(s.TryPick(NearestLoose(s)),"Replay: pick again");DOTween.CompleteAll();
                yield return ThrowSpot(s,spawn);s.ThrowActive();DOTween.CompleteAll();yield return Wait(.2f);
                Check(g.Current==GuidedTutorial.Step.Tools,"Replay: throw reaches Tools");
                cash=s.Cash;g.GotIt();yield return null;
                Check(g.Current==GuidedTutorial.Step.Done&&s.Cash==cash,"Replay finishes without a second reward");
                yield return Wait(5.4f);

                // Replaying later with full hands: the Pick step cannot be practised, so it moves on.
                Check(s.Restore(initial),"Reset catalog");
                while(s.carried.Count<s.capacity){Check(s.TryPick(NearestLoose(s)),"Fill hands");DOTween.CompleteAll();}
                PlayerPrefs.SetInt(prefix+"GuidedStep",(int)GuidedTutorial.Step.Pick);PlayerPrefs.DeleteKey(prefix+"GuidedDone");
                g.Begin(false);yield return Wait(.2f);
                Check(g.Current==GuidedTutorial.Step.Place,"Full hands at the Pick step move straight on to Place");
                g.Skip();Check(s.Restore(initial),"Reset catalog");

                // Skip, resume and visibility behind menus.
                g.Begin(true);g.skipButton.onClick.Invoke();Check(!g.IsRunning&&g.Completed&&g.group.alpha==0,"SKIP ends the tutorial and remembers it");
                PlayerPrefs.SetInt(prefix+"GuidedStep",(int)GuidedTutorial.Step.Throw);PlayerPrefs.DeleteKey(prefix+"GuidedDone");
                g.Begin(false);Check(g.Current==GuidedTutorial.Step.Tools&&g.stepLabel.text=="STEP 5 / 5","Restarting the game resumes at the saved step");
                yield return Wait(.1f);settings.OpenSettings();yield return Wait(.1f);
                Check(g.group.alpha==0&&!g.group.blocksRaycasts,"Hidden while Settings is open");
                settings.Close();yield return Wait(.1f);Check(g.group.alpha==1,"Shown again after Settings closes");
                settings.OpenSettings();settings.ReplayTutorial();yield return null;
                Check(!settings.IsOpen&&g.IsRunning&&g.Current==GuidedTutorial.Step.Move,"Settings > Replay tutorial restarts the guided tutorial");
                g.Skip();settings.OpenSettings();settings.OpenHowToPlay();
                Check(settings.IsOpen&&settings.ShowingTutorial,"Settings > How to play opens the help cards");settings.Close();

                // The reward flag survives a save round trip, so a fresh session cannot pay again.
                s.ClaimTutorialReward(g.reward);var snapshot=s.Capture();Check(snapshot.tutorialRewarded,"Snapshot stores the reward flag");
                var writer=new System.IO.StringWriter();new SaveJsonStream().Write(writer,snapshot);
                var loaded=JsonUtility.FromJson<SortingSession.Snapshot>(writer.ToString());
                Check(loaded.tutorialRewarded&&s.Restore(loaded)&&s.TutorialRewarded&&!s.ClaimTutorialReward(g.reward),"Reward flag survives save and load");
                Debug.Log("GUIDED_TUTORIAL_OK checks="+checks);
            }
            finally
            {
                if(g.IsRunning)g.Skip();if(settings.IsOpen)settings.Close();DOTween.CompleteAll();
                s.Restore(initial);s.InitializeSaveCache();
                foreach(var key in new[]{"GuidedStep","GuidedDone"})PlayerPrefs.DeleteKey(prefix+key);
            }
        }
    }
}
