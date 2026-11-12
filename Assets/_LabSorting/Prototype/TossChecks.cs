using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
using UnityEngine;

namespace LabSorting.Prototype
{
    // Trick-shot toss and throw feel in the real lab scene.
    public static class TossChecks
    {
        static int checks;
        static void Check(bool ok,string message){if(!ok)throw new Exception("TOSS CHECK FAILED: "+message);checks++;Debug.Log("TOSS PASS: "+message);}
        static IEnumerator Wait(float seconds)
        {
            float end=Time.time+seconds,deadline=Time.realtimeSinceStartup+30;
            while(Time.time<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Toss player loop stopped");yield return null;}
        }
        static IEnumerator Teleport(SortingSession s,Vector3 feet,Vector3 lookAt)
        {
            s.player.Clear();s.player.character.Motor.BaseVelocity=Vector3.zero;s.player.character.Motor.SetPositionAndRotation(feet,Quaternion.identity);
            yield return Wait(.15f);s.player.look.SnapToDirection(lookAt-s.player.view.transform.position);yield return Wait(.3f);
        }
        static bool StandingRoom(Vector3 feet,float level)
        {
            var blockers=Physics.OverlapCapsule(feet+Vector3.up*.4f,feet+Vector3.up*1.5f,.32f,~(1<<2),QueryTriggerInteraction.Ignore);
            if(blockers.Any(b=>!b.GetComponentInParent<SortItem>()))return false;
            return Physics.RaycastAll(feet+Vector3.up*.3f,Vector3.down,.6f,~(1<<2),QueryTriggerInteraction.Ignore).Any(h=>!h.collider.GetComponentInParent<SortItem>()&&h.normal.y>.95f&&Mathf.Abs(h.point.y-level)<.1f);
        }
        static Vector3 Front(SortRack rack,float distance){var p=rack.transform.TransformPoint(new Vector3(0,0,-distance));p.y=rack.transform.position.y+.03f;return p;}
        static Vector3 AimPoint(SortRack rack)=>rack.indicator.transform.position;
        static SortItem Loose(SortingSession s,ItemDefinition type)=>s.items.First(i=>i.definition==type&&i.location==ItemLocation.Loose&&!i.Moving);
        class Setup{public SortRack rack;public Vector3 spot;}
        static IEnumerator FindTossSpot(SortingSession s,ItemDefinition type,Setup found)
        {
            foreach(var rack in s.racks.Where(r=>r.CanAccept(type)&&r.transform.position.y<1))
            foreach(var distance in new[]{6f,5f,7f,4.5f})
            {
                var spot=Front(rack,distance);if(!StandingRoom(spot,rack.transform.position.y))continue;
                yield return Teleport(s,spot,AimPoint(rack));
                if(s.TossRack==rack){found.rack=rack;found.spot=spot;yield break;}
                if(!s.TossPlan(s.Active,rack,out _,out _,out _))Debug.Log("TOSS DIAGNOSTIC: "+rack.name+" distance="+distance+" stage="+s.TossBlockedStage+" blocker="+(s.TossBlocker?s.TossBlocker.name:"none"));
            }
        }
        public static IEnumerator Run()
        {
            checks=0;var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var feedback=s.GetComponent<SortingFeedback>();
            var actions=UnityEngine.Object.FindFirstObjectByType<ContextActionHUD>();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();
            s.persistence=false;s.player.SendMessage("OnApplicationFocus",true);yield return Wait(.15f);
            var initial=s.Capture();int droppedLandings=0,tossedLandings=0;var cues=new List<SortingCue>();
            Action<SortItem,bool> onLand=(item,tossed)=>{if(tossed)tossedLandings++;else droppedLandings++;};Action<SortingCue> onCue=c=>cues.Add(c);
            s.ItemLanded+=onLand;s.Feedback+=onCue;GameObject wall=null;float range=s.tossRange;
            try
            {
                var type=s.items.Where(i=>i.location==ItemLocation.Loose).GroupBy(i=>i.definition).Where(g=>g.Count()>=8).OrderBy(g=>g.Key.id).First().Key;
                Check(s.TryPick(Loose(s,type)),"Pick a "+type.displayName);DOTween.CompleteAll();yield return Wait(.3f);
                var setup=new Setup();yield return FindTossSpot(s,type,setup);
                Check(setup.rack,"Found a far rack that can be tossed into");var rack=setup.rack;
                Check(!s.TargetRack&&Vector3.Distance(s.player.view.transform.position,rack.transform.position)>s.reach,"The rack is too far to place on");
                Check(actions.TossShown&&actions.drop.GetComponentInChildren<UnityEngine.UI.Text>().text=="TOSS!","THROW button turns into TOSS!");
                Check(hud.target.text.StartsWith("TOSS into"),"Aim label names the toss target: "+hud.target.text);
                Check(rack.indicator.sharedMaterial==rack.valid,"Target rack lights green");
                // From far away the + often falls between two shelves onto the wall behind: still counts.
                s.player.look.SnapToDirection(rack.transform.TransformPoint(new Vector3(.2f,.98f,-.27f))-s.player.view.transform.position);yield return Wait(.3f);
                Check(s.TossRack==rack,"Aiming between the shelves of the rack still offers TOSS");
                s.player.look.SnapToDirection(AimPoint(rack)-s.player.view.transform.position);yield return Wait(.3f);

                // First toss: placed on the rack at once, paid, flies with a spin, lands exactly on its point.
                var item=s.Active;int cash=s.Cash;var start=item.transform.position;s.ThrowActive();
                Check(item.location==ItemLocation.Rack&&item.rack==rack&&rack.Count==1&&rack.Type==type&&!s.Active,"TOSS puts the item on the rack");
                Check(s.TossStreak==1&&s.Cash==cash+s.placementReward+s.tossBonus&&cues.Contains(SortingCue.Toss),"First toss pays placement +$"+s.placementReward+" and toss bonus +$"+s.tossBonus);
                var pose=rack.SlotPose(item.slot);yield return Wait(.2f);
                Check(item.Moving&&Quaternion.Angle(item.transform.rotation,pose.rotation)>15,"Item spins through the air");
                Check(item.transform.position.y>Mathf.Min(start.y,pose.position.y)+.2f,"Item flies in an arc");
                float deadline=Time.time+3;while(item.Moving&&Time.time<deadline)yield return null;
                Check(!item.Moving&&Vector3.Distance(item.transform.position,pose.position)<.001f&&Quaternion.Angle(item.transform.rotation,pose.rotation)<.5f&&item.hitbox.enabled&&item.transform.localScale==item.originalScale,"Tossed item lands exactly on its rack point");
                Check(tossedLandings==1,"Landing is reported once");

                // Streak: the third toss in a row pays double.
                for(int toss=2;toss<=3;toss++)
                {
                    Check(s.TryPick(Loose(s,type)),"Pick for toss "+toss);DOTween.CompleteAll();yield return Teleport(s,setup.spot,AimPoint(rack));
                    Check(s.TossRack==rack,"Rack offered again for toss "+toss);
                    cash=s.Cash;s.ThrowActive();int bonus=s.tossBonus*(toss>=s.tossStreakForDouble?2:1);
                    Check(s.TossStreak==toss&&s.Cash==cash+s.placementReward+bonus,"Toss "+toss+" in a row pays +$"+bonus);
                    yield return Wait(1.1f);
                }
                Check(rack.Count==3,"Three tosses landed on the rack");

                // Out of range, wrong type, blocked arc and within reach never offer TOSS.
                Check(s.TryPick(Loose(s,type)),"Pick for the negative cases");DOTween.CompleteAll();yield return Teleport(s,setup.spot,AimPoint(rack));
                s.tossRange=3;yield return Wait(.3f);Check(!s.TossRack&&!actions.TossShown,"Out of range: no TOSS");s.tossRange=range;yield return Wait(.3f);
                Check(s.TossRack==rack,"Back in range");
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.localScale=new Vector3(4,6,.2f);
                wall.transform.position=Vector3.Lerp(setup.spot,rack.transform.position,.5f)+Vector3.up*2.5f;wall.transform.rotation=rack.transform.rotation;
                Physics.SyncTransforms();yield return Wait(.3f);
                Check(!s.TossRack,"Something in the way: no TOSS");
                UnityEngine.Object.Destroy(wall);wall=null;yield return Wait(.3f);
                var otherType=s.items.First(i=>i.location==ItemLocation.Loose&&i.definition!=type).definition;
                Check(s.TryPick(Loose(s,otherType)),"Pick a different type");DOTween.CompleteAll();yield return Wait(.4f);
                Check(!s.TossRack&&!actions.TossShown,"A rack holding another type is never offered");
                s.Select(s.carried.FindIndex(i=>i.definition==type));yield return Wait(.3f);Check(s.TossRack==rack,"Selecting the matching type offers it again");
                yield return Teleport(s,Front(rack,1.6f),AimPoint(rack));
                Check(s.TargetRack==rack&&!s.TossRack&&!actions.TossShown,"Within reach it is a normal PLACE, not TOSS");
                Check(s.TryPlace(rack)&&s.TossStreak==0,"Placing normally ends the toss streak");DOTween.CompleteAll();

                // Save during a toss: the item is already on its rack point.
                yield return Teleport(s,setup.spot,AimPoint(rack));
                s.Select(0);yield return Wait(.3f);
                if(s.Active&&s.Active.definition!=type){Check(s.TryPick(Loose(s,type)),"Pick for save check");DOTween.CompleteAll();yield return Teleport(s,setup.spot,AimPoint(rack));}
                var flying=s.Active;s.ThrowActive();Check(flying.Moving&&flying.location==ItemLocation.Rack,"Toss in flight");
                var snapshot=s.Capture();var entry=snapshot.items.First(e=>e.id==flying.id);
                Check(entry.location==ItemLocation.Rack&&entry.rack==rack.id&&Vector3.Distance(entry.position,rack.SlotPose(entry.slot).position)<.001f,"Saving mid-toss stores the rack point, not the mid-air position");
                Check(s.Restore(snapshot)&&!flying.Moving&&flying.rack==rack&&Vector3.Distance(flying.transform.position,rack.SlotPose(flying.slot).position)<.001f,"Loading that save puts it straight on the rack");
                Check(s.TossStreak==0,"Loading a save clears the previous transient toss streak");

                // Throw feel: tumble, touch down with the collider back on, dust, small bounce, exact landing.
                Check(s.Restore(initial),"Reset catalog");
                Check(s.TryPick(Loose(s,type)),"Pick for a normal throw");DOTween.CompleteAll();
                bool found=false;
                foreach(var forward in new[]{1.2f,.6f,1.8f})foreach(var side in new[]{0f,-1f,1f})
                {if(found)break;var feet=initial.playerPosition+new Vector3(0,0,forward);yield return Teleport(s,feet,feet+new Vector3(side,1.3f,3));found=!s.TossRack&&s.TryThrowPosition(s.Active,out _);}
                Check(found,"Found clear floor for a normal throw");
                s.TryThrowPosition(s.Active,out var end);var thrown=s.Active;int drops=droppedLandings;s.ThrowActive();
                Check(thrown.location==ItemLocation.Loose&&cues.Contains(SortingCue.Throw),"Normal THROW drops the item");
                yield return Wait(.13f);Check(Quaternion.Angle(thrown.transform.rotation,Quaternion.identity)>20,"Thrown item tumbles in the air");
                deadline=Time.time+1;while(droppedLandings==drops&&Time.time<deadline)yield return null;
                Check(droppedLandings==drops+1&&thrown.hitbox.enabled,"Touchdown reported with the collider already back on");
                Check(!feedback.dust||feedback.dust.particleCount>0,"Dust puff at the landing spot");
                yield return Wait(.25f);
                Check(!thrown.Moving&&Vector3.Distance(thrown.transform.position,end)<.001f&&thrown.transform.rotation==Quaternion.identity,"Bounce settles exactly on the landing spot");
                Debug.Log("TOSS_CHECKS_OK checks="+checks);
            }
            finally
            {
                s.ItemLanded-=onLand;s.Feedback-=onCue;s.tossRange=range;if(wall)UnityEngine.Object.Destroy(wall);
                DOTween.CompleteAll();s.Restore(initial);s.InitializeSaveCache();
            }
        }
    }
}
