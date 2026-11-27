using System;
using System.Collections;
using System.Linq;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using LabSorting.Player;

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
        static bool HandNearCenter(GuidedTutorial g,float radius)=>g.hand.gameObject.activeInHierarchy&&Vector2.Distance(Tip(g),g.area.InverseTransformPoint(g.area.parent.Find("Crosshair").position))<=radius;
        static IEnumerator Teleport(SortingSession s,Vector3 feet,Vector3 lookAt)
        {
            s.player.Clear();s.player.character.Motor.BaseVelocity=Vector3.zero;s.player.character.Motor.SetPositionAndRotation(feet,Quaternion.identity);
            yield return Wait(.15f);s.player.look.SnapToDirection(lookAt-s.player.view.transform.position);yield return Wait(.4f);
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
        public static IEnumerator Run()=>RunFlow(60);
        public static IEnumerator RunLowFrameRate()=>RunFlow(30);
        public static IEnumerator Readability()
        {
            checks=0;var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();
            var g=UnityEngine.Object.FindFirstObjectByType<GuidedTutorial>();
            while(!s.Initialized)yield return null;
            s.persistence=false;s.player.SendMessage("OnApplicationFocus",true);s.player.SetBlocked(false);s.player.SetNetworkBlocked(false);
            yield return Wait(.3f);var initial=s.Capture();var prefix=g.preferencesPrefix;
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var economy=UnityEngine.Object.FindFirstObjectByType<EconomyHUD>();
            var carry=s.hand.GetComponent<LabSorting.Player.CarryPresentation>();
            try
            {
                Check(prefix.Contains("Automated"),"Isolated test preferences");g.Begin(true);
                var item=NearestLoose(s);Check(s.TryPick(item),"Pick first item");item.CompleteMotion();yield return Wait(.4f);
                Check(g.Current==GuidedTutorial.Step.Place&&g.GuidedRack,"Pickup selects a suitable rack");
                Check(g.GuidedRack.CanAccept(item.definition)&&Mathf.Abs(g.GuidedRack.transform.position.y-s.player.transform.position.y)<.8f,"Guide chooses compatible rack on same floor");
                Check(carry.OverlayActive&&item.gameObject.layer==LayerMask.NameToLayer("HeldItem"),"Held geometry uses overlay camera");
                Check(hud.active.text==item.definition.displayName+" ×1","Holding card names item and carried count");
                Check(!hud.GetComponent<DesignerToasts>().ShownText.StartsWith("Picked"),"Pickup does not produce repeated toast");
                var rack=g.GuidedRack;s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw,75));yield return Wait(.3f);
                Check(g.GuidedRack==rack&&carry.OverlayActive,"Turning down preserves rack target and held rendering");
                ShotGate.Capture("Logs/ReadabilityHeldDown.png");yield return Wait(.2f);
                s.ThrowActive();yield return Wait(2);item.CompleteMotion();yield return null;
                Check(g.Current==GuidedTutorial.Step.Pick&&!g.GuidedRack&&!g.RackGuideVisible,"Last-item drop returns to Pick and hides rack marker");
                Check(!carry.OverlayActive&&item.gameObject.layer==LayerMask.NameToLayer("SortingItem")&&hud.active.text=="HANDS EMPTY","Drop restores world rendering and empty indicator");
                Check(s.TryPick(item),"Repick dropped item");item.CompleteMotion();yield return Wait(.4f);
                Check(g.Current==GuidedTutorial.Step.Place&&g.GuidedRack,"Repick restores rack guidance");
                var second=NearestLoose(s);Check(s.TryPick(second),"Pick another item");second.CompleteMotion();yield return Wait(.3f);
                s.ThrowActive();yield return Wait(.4f);
                Check(s.Active&&g.Current==GuidedTutorial.Step.Place,"Drop with another held item keeps Place guidance");
                s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw,0));yield return Wait(.4f);
                ShotGate.Capture("Logs/ReadabilityRack.png");yield return Wait(.2f);
                Check(s.TryPlace(g.GuidedRack),"Place carried item on guided rack");DOTween.CompleteAll();yield return Wait(.4f);
                int search=s.SearchCredits,magnet=s.MagnetCredits,capacity=s.capacity;
                for(int page=0;page<3;page++)
                {
                    Check(g.Current==GuidedTutorial.Step.Tools&&g.ToolPage==page&&g.ToolModalActive,"Tool explanation page "+page);
                    var control=page==0?g.searchButton:page==1?g.magnetButton:g.capacityButton;
                    Canvas.ForceUpdateCanvases();
                    Check(!RectTransformUtility.RectangleContainsScreenPoint(g.bubble,control.position),"Bubble does not obscure tool "+page);
                    Check(Vector3.Distance(g.bubble.position,control.position)<g.bubble.rect.width*g.bubble.lossyScale.x,"Bubble stays beside tool "+page);
                    Check(g.Mode==GuidedTutorial.HandMode.Tap&&HandNear(g,(RectTransform)g.gotItButton.transform,70),"Hand indicates NEXT inside callout "+page);
                    ShotGate.Capture("Logs/ReadabilityTool"+page+".png");yield return Wait(.2f);
                    g.GotIt();yield return Wait(.4f);
                }
                Check(g.Current==GuidedTutorial.Step.Done&&s.SearchCredits==search&&s.MagnetCredits==magnet&&s.capacity==capacity,"Explanations finish without using tools");
                g.Skip();var empty=s.Capture();empty.searchCredits=0;empty.magnetCredits=0;s.Restore(empty);yield return Wait(.2f);
                Check(economy.searchPlus.gameObject.activeSelf&&economy.magnetPlus.gameObject.activeSelf,"Refill plus visible at zero");
                s.GrantSupply(SupplyKind.Search,1,"readability-search");s.GrantSupply(SupplyKind.Magnet,1,"readability-magnet");yield return Wait(.2f);
                Check(!economy.searchPlus.gameObject.activeSelf&&!economy.magnetPlus.gameObject.activeSelf,"Refill plus hidden with owned uses");
                Check(Mathf.Abs(UnityEngine.Object.FindFirstObjectByType<LabSoundscape>().musicVolume-.12f)<.001f,"Music is 12 percent");
                Debug.Log("READABILITY_OK checks="+checks);
            }
            finally
            {
                if(g.IsRunning)g.Skip();DOTween.CompleteAll();s.Restore(initial);s.InitializeSaveCache();
                foreach(var key in new[]{"GuidedStep","GuidedDone","GuidedToolPage","RackTurnShown.v1"})PlayerPrefs.DeleteKey(prefix+key);
            }
        }
        static IEnumerator RunFlow(int frameRate)
        {
            checks=0;var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var g=UnityEngine.Object.FindFirstObjectByType<GuidedTutorial>();var settings=g.GetComponent<SettingsTutorialPanel>();
            int previousFrameRate=Application.targetFrameRate,previousVsync=QualitySettings.vSyncCount;
            float previousFixedDelta=Time.fixedDeltaTime;
            Application.targetFrameRate=frameRate;QualitySettings.vSyncCount=0;Time.fixedDeltaTime=frameRate==30?1f/30:.02f;
            s.persistence=false;s.player.SendMessage("OnApplicationFocus",true);yield return Wait(.15f);
            var initial=s.Capture();string prefix=g.preferencesPrefix;var spawn=s.player.transform.position;var motor=s.player.character.Motor;
            try
            {
                Check(prefix.Contains("Automated"),"Tutorial uses test preference keys");
                Check(!g.IsRunning&&!g.Completed&&g.group.alpha==0,"Nothing shows while automatic start is off");
                g.Begin(false);yield return Wait(.3f);
                Check(g.IsRunning&&g.Current==GuidedTutorial.Step.Move&&g.group.alpha==1,"Starts at step 1, Move");
                Check(g.Mode==GuidedTutorial.HandMode.Drag&&HandNear(g,g.joystick,120),"Hand drags on the joystick");
                Check(!g.GetComponent<DesignerToasts>().ShownText.StartsWith("Pick an item, then"),"Move shows only the current tutorial instruction");
                Check(g.message.text.Contains("joystick")&&g.stepLabel.text=="STEP 1 / 7"&&!g.gotItButton.gameObject.activeSelf,"Move text and step counter");

                // Loading a save moves the player in one frame: that must not count as walking.
                motor.SetPosition(spawn+new Vector3(0,0,2.5f));yield return Wait(.4f);motor.SetPosition(spawn);yield return Wait(.4f);
                Check(g.Current==GuidedTutorial.Step.Move,"A teleport (like loading a save) does not complete Move");
                ShotGate.Capture("Logs/Tutorial01Move.png");yield return Wait(.1f);
                // Use the same pointer events as a thumb on the real joystick. SetPosition is a
                // teleport, and two test-runner yields are not guaranteed to contain a game Update.
                var stick=g.joystick.GetComponent<LabTouchArea>();
                var pointer=new PointerEventData(EventSystem.current){pointerId=91,position=g.joystick.position};
                var start=motor.TransientPosition;float deadline=Time.realtimeSinceStartup+15;
                stick.OnPointerDown(pointer);
                pointer.position=g.joystick.TransformPoint(new Vector3(0,g.joystick.rect.width*.35f,0));
                while(Vector3.Distance(start,motor.TransientPosition)<.45f)
                {
                    if(Time.realtimeSinceStartup>deadline)throw new Exception("Joystick did not move player: "+motor.TransientPosition+" blocked="+s.player.blocked);
                    stick.OnDrag(pointer);yield return null;
                }
                stick.OnPointerUp(pointer);yield return Wait(.12f);
                Check(g.Current==GuidedTutorial.Step.Move&&g.Mode==GuidedTutorial.HandMode.Hidden,"Real joystick walking hides the hand before the distance threshold");
                yield return Wait(g.idleDelay+.4f);
                Check(g.Current==GuidedTutorial.Step.Move&&g.Mode==GuidedTutorial.HandMode.Drag&&g.hand.gameObject.activeSelf,"Stopping below the walking threshold brings the hint back");
                pointer.position=g.joystick.position;stick.OnPointerDown(pointer);
                pointer.position=g.joystick.TransformPoint(new Vector3(0,g.joystick.rect.width*.35f,0));
                deadline=Time.realtimeSinceStartup+15;
                while(g.Current==GuidedTutorial.Step.Move)
                {
                    if(Time.realtimeSinceStartup>deadline)throw new Exception("Walking never completed: "+motor.TransientPosition+" blocked="+s.player.blocked);
                    stick.OnDrag(pointer);yield return null;
                }
                stick.OnPointerUp(pointer);yield return Wait(.15f);
                Check(g.Current==GuidedTutorial.Step.Look,"Walking 1.5 m through joystick input completes Move");
                yield return Wait(g.idleDelay+.4f);
                Check(g.Mode==GuidedTutorial.HandMode.Drag&&HandNear(g,g.lookArea,220),"Hand swipes on the look area");
                ShotGate.Capture("Logs/Tutorial02Look.png");yield return Wait(.1f);
                var lookTouch=g.lookArea.GetComponent<LabTouchArea>();
                var swipe=new PointerEventData(EventSystem.current){pointerId=92,position=g.lookArea.position};
                lookTouch.OnPointerDown(swipe);deadline=Time.realtimeSinceStartup+15;
                while(g.Current==GuidedTutorial.Step.Look)
                {
                    if(Time.realtimeSinceStartup>deadline)throw new Exception("Look swipe never completed");
                    swipe.delta=new Vector2(30,0);lookTouch.OnDrag(swipe);yield return Wait(.08f);
                }
                lookTouch.OnPointerUp(swipe);yield return Wait(.2f);
                Check(g.Current==GuidedTutorial.Step.Pick,"Turning 45 degrees through touch swipes completes Look");

                // Approved PICK tutorial rotates to a reachable item and exposes its own exclusive button.
                UnityEngine.UI.Button guidedPick=null;
                yield return CurrentGameplayChecks.Until(()=>{
                    guidedPick=g.group.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(b=>b.name=="Tutorial PICK");
                    return guidedPick&&guidedPick.interactable&&s.TargetItem;
                },"Guided PICK automatically aims at reachable item",15);
                Check(s.player.PickTutorialLocked&&g.Mode==GuidedTutorial.HandMode.Tap,"Guided PICK locks other controls and points at PICK");
                s.player.SetMove(Vector2.one);
                Check(s.player.Move==Vector2.zero,"Guided PICK rejects movement input");
                var item=s.TargetItem;guidedPick.onClick.Invoke();yield return Wait(.7f);
                Check(s.Active==item&&!s.player.PickTutorialLocked,"Guided PICK collects the aimed item and releases its input lock");
                Check(g.Current==GuidedTutorial.Step.Place,"Picking completes the Pick step"); Check(g.GuidedRack&&g.RackGuideVisible,"Tutorial marks a compatible same-floor rack");
                var carry=s.hand.GetComponent<LabSorting.Player.CarryPresentation>();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();
                Check(carry.OverlayActive&&s.Active.gameObject.layer==LayerMask.NameToLayer("HeldItem"),"Held model renders separately from floor geometry");
                Check(hud.active.text==item.definition.displayName+" ×1","Holding card identifies selected type and carried count");
                Check(Mathf.Abs(UnityEngine.Object.FindFirstObjectByType<LabSoundscape>().musicVolume-.12f)<.001f,"Music target is 12 percent");
                var dropTap=new PointerEventData(EventSystem.current){pointerId=95,position=g.throwButton.position};
                ExecuteEvents.Execute(g.throwButton.gameObject,dropTap,ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(g.throwButton.gameObject,dropTap,ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(g.throwButton.gameObject,dropTap,ExecuteEvents.pointerClickHandler);
                yield return Wait(2f);item.CompleteMotion();yield return null;
                Check(g.Current==GuidedTutorial.Step.Pick&&!g.GuidedRack&&!g.RackGuideVisible,"Dropping the last item returns to Pick and removes rack guidance");
                Check(item.gameObject.layer==LayerMask.NameToLayer("SortingItem")&&!carry.OverlayActive&&hud.active.text=="HANDS EMPTY","Dropped item returns to world rendering and empty-hands indicator");
                Check(s.TryPick(item),"Pick the dropped item again");item.CompleteMotion();yield return Wait(.4f);
                Check(g.Current==GuidedTutorial.Step.Place&&g.GuidedRack,"Picking again resumes rack guidance");
                s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw,75));yield return Wait(.3f);
                Check(carry.OverlayActive&&Mathf.Max(carry.HeldScreenRect.width,carry.HeldScreenRect.height)>s.player.view.pixelHeight*.1f,"Held item stays readable while looking at the floor");
                ShotGate.Capture("Logs/ReadabilityHeldDown.png");yield return Wait(.1f);
                s.player.look.SetYawPitch(new Vector2(s.player.look.Yaw,-75));yield return Wait(.6f);
                Check(g.Mode==GuidedTutorial.HandMode.Aim&&g.message.text.Contains("rack"),"Not at a rack: hand points at the aim cross and explains the green bar");
                var rack=RackFor(s);var front=rack.transform.TransformPoint(new Vector3(0,0,-1.6f));front.y=rack.transform.position.y+.03f;
                yield return Teleport(s,front,rack.label.transform.position);yield return Wait(.1f);
                Check(s.TargetRack==rack&&g.Mode==GuidedTutorial.HandMode.Tap&&HandNear(g,g.placeButton,70)&&g.message.text.Contains("decides"),"At the rack the hand taps PLACE and explains rack types");
                ShotGate.Capture("Logs/SoftTutorialPlace.png");yield return Wait(.1f);
                var placeTap=new PointerEventData(EventSystem.current){pointerId=94};
                g.actions.placeHold.OnPointerDown(placeTap);g.actions.placeHold.OnPointerUp(placeTap);yield return Wait(.4f);
                Check(item.location==ItemLocation.Rack&&item.rack==rack,"PLACE button places item on the aimed rack");

                Check(g.Current==GuidedTutorial.Step.Tools&&g.gotItButton.gameObject.activeSelf,"Placing completes the main task; optional tools and GOT IT appear");
                yield return CurrentGameplayChecks.Until(()=>g.Mode==GuidedTutorial.HandMode.Tap&&HandNear(g,g.gotItButton.transform as RectTransform,70),"Tools hand reaches NEXT after modal layout",3);
                Check(g.ToolModalActive&&s.player.TutorialBlocked&&g.Mode==GuidedTutorial.HandMode.Tap&&HandNear(g,g.gotItButton.transform as RectTransform,70)&&!g.skipButton.gameObject.activeSelf,"Tools highlight their button but hand points only to NEXT");
                var bubbleCorners=new Vector3[4];g.bubble.GetWorldCorners(bubbleCorners);
                Check(!RectTransformUtility.RectangleContainsScreenPoint(g.bubble,g.searchButton.position),"Search callout does not cover highlighted control");
                Check(Vector3.Distance(g.bubble.position,g.searchButton.position)<g.bubble.rect.width*g.bubble.lossyScale.x,"Search explanation sits beside its button");
                ShotGate.Capture("Logs/ReadabilitySearch.png");yield return Wait(.1f);
                int cash=s.Cash;g.gotItButton.onClick.Invoke();yield return Wait(.4f);
                int search=s.SearchCredits,magnet=s.MagnetCredits,capacity=s.capacity;
                Check(g.ToolPage==1&&g.ToolModalActive,"NEXT explains magnet without using it");
                g.gotItButton.onClick.Invoke();yield return Wait(.4f);
                Check(g.ToolPage==2&&g.capacityButton&&g.ToolModalActive,"Capacity explanation points at collected-items plus");
                Check(g.gotItButton.GetComponentInChildren<UnityEngine.UI.Text>().text=="DONE","Final explanation clearly offers DONE");
                g.Begin(false);yield return Wait(.4f);Check(g.ToolPage==2,"Capacity explanation resumes safely");
                Check(s.Cash==cash&&s.SearchCredits==search&&s.MagnetCredits==magnet&&s.capacity==capacity,"Explanations consume no cash, tools or capacity upgrades");
                g.gotItButton.onClick.Invoke();yield return Wait(.4f);
                Check(g.Current==GuidedTutorial.Step.Done&&s.TutorialRewarded&&s.Cash==cash+g.reward,"Finishing pays the reward: +$"+g.reward);
                yield return Wait(.4f);
                Check(g.Completed&&g.message.text.StartsWith("Well done")&&!g.hand.gameObject.activeSelf&&!g.skipButton.gameObject.activeSelf,"Well done message, tutorial remembered as finished");
                ShotGate.Capture("Logs/Tutorial07Complete.png");yield return Wait(.1f);
                yield return Wait(5.4f);
                Check(!g.IsRunning&&g.group.alpha==0,"Message hides after a few seconds");

                // Replay from the start, skipping ahead by doing actions early: never pays twice.
                g.Begin(true);yield return null;Check(g.IsRunning&&g.Current==GuidedTutorial.Step.Move,"Replay starts again at Move");
                Check(s.TryPick(NearestLoose(s)),"Replay: pick");DOTween.CompleteAll();yield return Wait(.4f);
                Check(g.Current==GuidedTutorial.Step.Place,"Picking during Move skips ahead to Place");
                Check(s.TryPlace(RackFor(s)),"Replay: place");DOTween.CompleteAll();yield return Wait(.4f);
                Check(!s.TryPick(NearestLoose(s)),"Replay: tools block extra pickups");DOTween.CompleteAll();
                yield return ThrowSpot(s,spawn);s.ThrowActive();DOTween.CompleteAll();yield return Wait(.4f);
                Check(g.Current==GuidedTutorial.Step.Tools,"Replay: throw reaches Tools");
                cash=s.Cash;g.GotIt();yield return Wait(.4f);g.GotIt();yield return Wait(.4f);g.GotIt();yield return null;
                Check(g.Current==GuidedTutorial.Step.Done&&s.Cash==cash,"Replay finishes without a second reward");
                yield return Wait(5.4f);

                // Replaying later with full hands: the Pick step cannot be practised, so it moves on.
                Check(s.Restore(initial),"Reset catalog");
                while(s.carried.Count<s.capacity){Check(s.TryPick(NearestLoose(s)),"Fill hands");DOTween.CompleteAll();}
                PlayerPrefs.SetInt(prefix+"GuidedStep",(int)GuidedTutorial.Step.Pick);PlayerPrefs.DeleteKey(prefix+"GuidedDone");
                g.Begin(false);yield return Wait(.4f);
                Check(g.Current==GuidedTutorial.Step.Place,"Full hands at the Pick step move straight on to Place");
                g.Skip();Check(s.Restore(initial),"Reset catalog");

                // Skip, resume and visibility behind menus.
                g.Begin(true);g.skipButton.onClick.Invoke();Check(!g.IsRunning&&g.Completed&&g.group.alpha==0,"SKIP ends the tutorial and remembers it");
                PlayerPrefs.SetInt(prefix+"GuidedStep",(int)GuidedTutorial.Step.Throw);PlayerPrefs.DeleteKey(prefix+"GuidedDone");
                g.Begin(false);Check(g.Current==GuidedTutorial.Step.Tools&&g.stepLabel.text=="STEP 5 / 7","Restarting the game resumes at the saved step");
                yield return Wait(.1f);settings.OpenSettings();yield return Wait(.1f);
                Check(g.group.alpha==1&&!settings.IsOpen,"Settings cannot interrupt mandatory guidance");
                settings.Close();yield return Wait(.1f);Check(g.group.alpha==1,"Shown again after Settings closes");
                g.GotIt();yield return Wait(.4f);g.GotIt();yield return Wait(.4f);g.GotIt();settings.OpenSettings();settings.ReplayTutorial();yield return null;
                Check(!settings.IsOpen&&g.IsRunning&&g.Current==GuidedTutorial.Step.Move,"Settings > Replay tutorial restarts the guided tutorial");
                g.Skip();settings.OpenSettings();settings.OpenHowToPlay();
                Check(settings.IsOpen&&settings.ShowingTutorial,"Settings > How to play opens the help cards");settings.Close();

                // The reward flag survives a save round trip, so a fresh session cannot pay again.
                s.ClaimTutorialReward(g.reward);var snapshot=s.Capture();Check(snapshot.tutorialRewarded,"Snapshot stores the reward flag");
                var writer=new System.IO.StringWriter();new SaveJsonStream().Write(writer,snapshot);
                var loaded=JsonUtility.FromJson<SortingSession.Snapshot>(writer.ToString());
                Check(loaded.tutorialRewarded&&s.Restore(loaded)&&s.TutorialRewarded&&!s.ClaimTutorialReward(g.reward),"Reward flag survives save and load");
                var economy=UnityEngine.Object.FindFirstObjectByType<EconomyHUD>();
                var emptyTools=s.Capture();emptyTools.searchCredits=0;emptyTools.magnetCredits=0;s.Restore(emptyTools);yield return Wait(.2f);
                Check(economy.searchPlus.gameObject.activeSelf&&economy.magnetPlus.gameObject.activeSelf,"Zero credits reveal refill plus buttons");
                s.GrantSupply(SupplyKind.Search,1,"readability-search");s.GrantSupply(SupplyKind.Magnet,1,"readability-magnet");yield return Wait(.2f);
                Check(!economy.searchPlus.gameObject.activeSelf&&!economy.magnetPlus.gameObject.activeSelf,"Owned credits hide refill plus buttons");
                s.Restore(initial);yield return Wait(.2f);
                Debug.Log("GUIDED_TUTORIAL_OK checks="+checks+" targetFPS="+frameRate+" fixedDelta="+Time.fixedDeltaTime);
            }
            finally
            {
                s.player.Clear();Application.targetFrameRate=previousFrameRate;QualitySettings.vSyncCount=previousVsync;Time.fixedDeltaTime=previousFixedDelta;
                if(g.IsRunning)g.Skip();if(settings.IsOpen)settings.Close();DOTween.CompleteAll();
                s.Restore(initial);s.InitializeSaveCache();
                foreach(var key in new[]{"GuidedStep","GuidedDone","GuidedToolPage","RackTurnShown.v1"})PlayerPrefs.DeleteKey(prefix+key);
            }
        }
    }
}

