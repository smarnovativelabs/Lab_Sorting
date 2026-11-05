using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class FunChecks
    {
        public static IEnumerator Run(SortingSession s,Action<bool,string> check,Action<SortingSession,string> capture)
        {
            var initial=s.Capture();bool oldPersistence=s.persistence;string oldName=s.saveName;s.persistence=false;s.saveName="fun-check-"+Guid.NewGuid().ToString("N")+".json";string path=s.SavePath;var ui=UnityEngine.Object.FindFirstObjectByType<ContextActionHUD>();var rewards=UnityEngine.Object.FindFirstObjectByType<SortingRewardsHUD>();var rack=s.racks[0];var slots=rack.slots;
            int goals=0,racks=0;Action<string,int> goal=(name,value)=>{if(name.StartsWith("Sorting"))goals++;};Action<SortRack> completed=r=>racks++;
            s.SmallGoalEarned+=goal;s.RackCompleted+=completed;
            try
            {
                check(ui&&ui.pick&&ui.place&&ui.drop,"Context action groups wired in saved scene");
                s.player.look.SetYawPitch(new Vector2(0,-80));s.RefreshTarget();ui.Refresh();
                check(ui.pick.alpha==0&&ui.place.alpha==0&&ui.drop.alpha==0&&!ui.pick.blocksRaycasts,"Empty hands and no item target hide all three actions");
                var item=s.items[0];item.Snap(s.player.view.transform.position+s.player.view.transform.forward*2,Quaternion.identity);Physics.SyncTransforms();s.RefreshTarget();ui.Refresh();
                check(s.TargetItem==item&&ui.pick.alpha==1&&ui.place.alpha==0,"Hover exposes Pick only");
                yield return new WaitForSecondsRealtime(.3f);
                var pointer=new PointerEventData(EventSystem.current){pointerId=71,button=PointerEventData.InputButton.Left};ui.pickHold.OnPointerDown(pointer);s.RefreshTarget();ui.Refresh();
                check(s.carried.Count==1&&ui.pick.alpha==0&&ui.place.alpha==1&&ui.drop.alpha==1,"Pickup hides unavailable Pick and exposes Place/Throw");
                check(s.Holding&&ui.pickHold.PointerHeld&&ui.pick.blocksRaycasts,"Hidden Pick preserves existing hold pointer between targets");ui.pickHold.OnPointerUp(pointer);ui.Refresh();check(!s.Holding&&!ui.pick.blocksRaycasts,"Releasing hidden Pick releases capture without a stuck hold");
                s.player.SetBlocked(true);s.RefreshTarget();ui.Refresh();check(ui.place.alpha==0&&ui.drop.alpha==0,"Modal blocking hides contextual carry actions");s.player.SetBlocked(false);s.Restore(initial);
                for(int i=0;i<24;i++){s.TryPick(s.items[i]);DOTween.CompleteAll();s.TryPlace(s.racks[i%5]);DOTween.CompleteAll();}
                check(goals==0&&s.ShortGoalText.Contains("24 / 25"),"Short goal tracks unique placements before threshold");int cash=s.Cash;s.TryPick(s.items[24]);DOTween.CompleteAll();s.TryPlace(s.racks[4]);DOTween.CompleteAll();
                check(goals==1&&s.Cash==cash+s.placementReward+s.halfBonus+SortingSession.ShortGoalBonus&&s.ShortGoalText.Contains("0 / 25"),"25 unique placements pay exactly one small bonus and advance goal");
                yield return null;capture(s,"Logs/ShortGoalsHUD.png");check(rewards.milestone.preferredHeight<=rewards.milestone.rectTransform.rect.height,"Short goal and milestone fit existing HUD region");
                var saved=s.Capture();s.Restore(initial);check(s.Restore(saved)&&s.ShortGoalText.Contains("0 / 25"),"Short goal progress restores from persistent unique-item history");cash=s.Cash;s.TryPick(s.items[24]);DOTween.CompleteAll();s.TryPlace(s.racks[4]);DOTween.CompleteAll();check(goals==1&&s.Cash==cash,"Re-sorting cannot farm short-goal bonuses");
                s.Restore(initial);rack.slots=new[]{slots[0]};rack.Initialize();s.TryPick(s.items[0]);DOTween.CompleteAll();cash=s.Cash;s.TryPlace(rack);DOTween.CompleteAll();
                check(racks==1&&s.CompletedRackCount==1&&s.Cash==cash+s.placementReward+SortingSession.RackBonus,"First full rack pays exactly one reward and emits celebration");
                saved=s.Capture();s.Restore(initial);check(s.Restore(saved)&&s.CompletedRackCount==1,"Rack reward history restores");cash=s.Cash;s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryPlace(rack);DOTween.CompleteAll();check(racks==1&&s.Cash==cash,"Empty/refill cannot repeat rack payment or celebration");
                s.persistence=true;s.RequestSave();check(s.FlushSaves(),"Rack reward checkpoint reaches disk");s.persistence=false;s.Restore(initial);check(s.Load()&&s.Cash==cash&&s.CompletedRackCount==1,"Cash and rack reward ledger survive disk reload together");
                saved=s.Capture();saved.completedRacks=null;check(s.Restore(saved)&&s.Cash==cash&&s.CompletedRackCount==1,"Old save migrates full rack without retroactive cash");
                var bad=s.Capture();bad.completedRacks=new List<string>{"missing-rack"};check(!s.Restore(bad)&&s.Cash==cash,"Unknown rack reward ID rejected without mutation");bad=s.Capture();bad.completedRacks.Add(rack.id);check(!s.Restore(bad),"Duplicate rack reward ID rejected");
            }
            finally{s.FlushSaves();s.persistence=false;foreach(var suffix in new[]{"",".bak",".tmp"})if(System.IO.File.Exists(path+suffix))System.IO.File.Delete(path+suffix);s.saveName=oldName;s.SmallGoalEarned-=goal;s.RackCompleted-=completed;rack.slots=slots;s.player.SetBlocked(false);s.Restore(initial);s.persistence=oldPersistence;}
        }
    }
}


