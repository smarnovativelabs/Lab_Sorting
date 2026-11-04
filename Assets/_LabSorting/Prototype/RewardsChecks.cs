using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class RewardsChecks
    {
        static IEnumerator Wait(float seconds){float end=Time.unscaledTime+seconds,deadline=Time.realtimeSinceStartup+20;while(Time.unscaledTime<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Rewards frame clock stopped");yield return null;}}
        public static IEnumerator Run(SortingSession s,Action<bool,string> check,Action<SortingSession,string> capture)
        {
            var initial=s.Capture();bool oldPersistence=s.persistence;var ui=UnityEngine.Object.FindFirstObjectByType<SortingRewardsHUD>();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var economy=UnityEngine.Object.FindFirstObjectByType<EconomyHUD>();
            int earned=0,magnetEvents=0,searchEvents=0,collected=0;Vector3[] positions=null;
            Action<string,int> reward=(name,value)=>earned++;
            Action<ItemDefinition,Vector3[]> magnet=(type,origins)=>{magnetEvents++;collected=origins.Length;positions=origins;};
            Action<ItemDefinition,int> search=(type,count)=>searchEvents++;
            s.MilestoneEarned+=reward;s.MagnetCollected+=magnet;s.SearchStarted+=search;s.persistence=false;
            try
            {
                check(ui&&ui.milestone&&ui.selectedSwatch&&ui.ability,"Milestone and selection HUD wired in saved scene");
                int wallet=s.Cash;
                for(int i=0;i<s.items.Length;i++)
                {
                    check(s.TryPick(s.items[i]),"Milestone fixture picks item "+i);DOTween.CompleteAll();check(s.TryPlace(s.racks[i%5]),"Milestone fixture places item "+i);DOTween.CompleteAll();
                    if(i==8)check(s.MilestoneCount==0&&s.Cash==wallet+9*s.placementReward,"No milestone reward before threshold");
                    if(i==9)check(s.HasMilestone("first-ten")&&s.Cash==wallet+10*s.placementReward+s.firstTenBonus,"Ten unique items award first milestone once");
                    if(i==24)check(s.HasMilestone("half")&&!s.HasMilestone("complete"),"Half milestone triggers at 25 of 50 items");
                }
                int expected=wallet+s.items.Length*s.placementReward+s.firstTenBonus+s.categoryBonus+s.halfBonus+s.completeBonus;
                check(s.MilestoneCount==4&&earned==4&&s.Cash==expected,"All four milestones award exact configured bonuses");yield return Wait(.1f);check(ui.milestone.text=="ALL MILESTONES COMPLETE","HUD reflects completed milestones");capture(s,"Logs/MilestonesHUD.png");
                s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryPlace(s.racks[0]);DOTween.CompleteAll();check(s.Cash==expected&&earned==4,"Removing and replacing cannot farm milestone rewards");
                var saved=s.Capture();check(saved.version==3&&saved.milestones.Count==4,"Save version 3 includes claimed milestone IDs");s.Restore(initial);check(s.Restore(saved)&&s.MilestoneCount==4&&s.Cash==expected,"Milestone snapshot round trip preserves rewards");
                s.persistence=true;s.Save();s.Restore(initial);check(s.Load()&&s.MilestoneCount==4&&s.Cash==expected,"Disk save/load persists one-time bonuses");s.persistence=false;
                s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryPlace(s.racks[0]);DOTween.CompleteAll();check(s.Cash==expected&&earned==4,"Disk reload cannot repeat milestone payments");
                var legacy=s.Capture();legacy.version=2;legacy.milestones=null;check(s.Restore(legacy)&&s.MilestoneCount==4&&s.Cash==expected&&earned==4,"Version 2 migration marks existing milestones without extra payment");
                var invalid=s.Capture();invalid.milestones.Add("unknown");check(!s.Restore(invalid)&&s.Cash==expected&&s.MilestoneCount==4,"Unknown milestone ID rejected atomically");invalid=s.Capture();invalid.milestones.Add("half");check(!s.Restore(invalid)&&s.Cash==expected,"Duplicate milestone ID rejected atomically");
                s.Restore(initial);s.TryPick(s.items[0]);DOTween.CompleteAll();yield return Wait(.1f);check(hud.active.text.Contains("SELECTED")&&hud.active.text.Contains("x1")&&ui.selectedSwatch.color==s.items[0].definition.color,"Selected HUD shows category count and matching color");
                check(s.TrySearch()&&searchEvents==1,"Successful search emits one visual event");yield return Wait(.1f);check(ui.ability.text.Contains("found")&&economy.searchLabel.text.Contains(s.SearchType.displayName),"Search feedback and button name the purchased category");wallet=s.Cash;check(!s.TrySearch()&&searchEvents==1&&s.Cash==wallet,"Rejected search has no charge or success effect");
                s.TryPick(s.items[1]);DOTween.CompleteAll();yield return Wait(.1f);check(economy.searchLabel.text.Contains(s.items[0].definition.displayName)&&economy.magnetLabel.text.Contains(s.items[1].definition.displayName),"Active search retains category while magnet follows selection");s.Select(0);
                var far=s.items[5];far.Snap(new Vector3(1000,1,1000),Quaternion.identity);wallet=s.Cash;
                check(s.TryMagnet()&&magnetEvents==1&&collected==8&&s.Cash==wallet-s.magnetCost,"Magnet collection feedback matches free capacity and one payment");
                check(Array.Exists(positions,p=>p.x>900)&&far.location==ItemLocation.Hand,"Magnet visuals capture distant origins without changing unlimited collection");
                check(ui.ActiveFlights==8,"Magnet starts staggered visual chips for collected items");yield return Wait(.1f);check(ui.ability.text.Contains("+8")&&hud.active.text.Contains("x9"),"Collection count and selected category count update together");capture(s,"Logs/RewardsHUD.png");
                wallet=s.Cash;check(!s.TryMagnet()&&magnetEvents==1&&s.Cash==wallet,"Full inventory creates no magnet charge or extra visual burst");yield return Wait(2);check(ui.ActiveFlights==0,"Magnet visual chips finish and return to pool");
                s.Restore(initial);check(ui.ActiveFlights==0&&ui.ability.text==""&&ui.toast.text=="","Restore clears transient ability and milestone visuals");
                s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryMagnet();check(ui.ActiveFlights>0,"Magnet effect can be reused");s.Restore(initial);check(ui.ActiveFlights==0,"Restore during flight cancels visuals without losing logical items");
                check(!ui.selectedSwatch.raycastTarget&&!ui.milestone.raycastTarget&&!ui.ability.raycastTarget,"Feedback UI does not intercept touch controls");
            }
            finally{s.MilestoneEarned-=reward;s.MagnetCollected-=magnet;s.SearchStarted-=search;s.Restore(initial);s.persistence=oldPersistence;}
        }
    }
}
