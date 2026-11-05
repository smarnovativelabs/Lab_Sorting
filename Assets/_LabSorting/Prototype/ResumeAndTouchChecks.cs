using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.Player;
namespace LabSorting.Prototype
{
    public static class ResumeAndTouchChecks
    {
        static IEnumerator Wait(float seconds){float end=Time.unscaledTime+seconds,limit=Time.realtimeSinceStartup+120;while(Time.unscaledTime<end){if(Time.realtimeSinceStartup>limit)throw new Exception("Resume test clock stopped");yield return null;}}
        public static IEnumerator Run(SortingSession s,Action<bool,string> check)
        {
            var initial=s.Capture();string oldName=s.saveName;bool persistence=s.persistence;s.saveName="sorting-resume-test-"+Guid.NewGuid().ToString("N")+".json";string path=s.SavePath;s.persistence=true;
            try
            {
                s.TryPick(s.items[0]);s.SendMessage("OnApplicationPause",true);s.Restore(initial);check(s.Load()&&s.carried.Count==1&&s.Active==s.items[0]&&!s.Active.Moving,"Pause during pickup restores exact held item and finished pose");
                int cash=s.Cash;s.TryPlace(s.racks[0]);s.SendMessage("OnApplicationPause",true);s.Restore(initial);check(s.Load()&&s.SortedCount==1&&s.racks[0].Count==1&&s.Cash==cash+s.placementReward,"Pause during placement keeps rack ownership and one reward");
                s.Restore(initial);s.TryPick(s.items[0]);DOTween.CompleteAll();yield return Wait(.3f);s.ThrowActive();var destination=s.items[0].SavedPosition;check(s.items[0].Moving,"Throw resume fixture is in flight");s.SendMessage("OnApplicationFocus",false);s.Restore(initial);check(s.Load()&&s.items[0].location==ItemLocation.Loose&&Vector3.Distance(s.items[0].transform.position,destination)<.001f,"Focus loss during throw saves final safe destination");
                s.Restore(initial);s.TryPick(s.items[0]);DOTween.CompleteAll();cash=s.Cash;check(s.TryMagnet()&&s.Active.Moving,"Immediate paid save preserves magnet animation");int held=s.carried.Count;s.Restore(initial);check(s.Load()&&s.carried.Count==held&&s.Cash==cash-s.magnetCost&&s.carried.Select(i=>i.id).Distinct().Count()==held,"Immediate magnet disk save is atomic with inventory and cash");
                s.Restore(initial);check(s.TryUpgradeCapacity(),"Capacity upgrade succeeds for resume fixture");cash=s.Cash;s.Restore(initial);check(s.Load()&&s.capacity==11&&s.Cash==cash,"Capacity and payment survive immediate reload");
                s.Restore(initial);s.TryPick(s.items[0]);DOTween.CompleteAll();check(s.TrySearch(),"Search purchased for resume fixture");cash=s.Cash;s.Restore(initial);check(s.Load()&&s.SearchType==s.items[0].definition&&s.SearchSeconds>0&&s.Cash==cash,"Paid search restores target and remaining time without charging again");check(!s.TrySearch()&&s.Cash==cash,"Reload cannot buy duplicate active search");
                s.Save();s.Save();File.WriteAllText(path,"{broken");s.Restore(initial);check(s.Load()&&s.SearchType&&s.Cash==cash,"Corrupted primary save recovers valid backup including paid search");
                var bad=s.Capture();bad.searchRemaining=float.NaN;check(!s.Restore(bad)&&s.Cash==cash,"Invalid search timer rejected before live mutation");
                s.persistence=false;s.Restore(initial);s.player.SendMessage("OnApplicationFocus",true);
                var areas=UnityEngine.Object.FindObjectsByType<LabTouchArea>(FindObjectsSortMode.None);var move=areas.Single(a=>a.movement);var look=areas.Single(a=>!a.movement);var rect=(RectTransform)move.transform;
                var screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(rect.rect.width*.3f,0)));
                var left=new PointerEventData(EventSystem.current){pointerId=31,position=screen,button=PointerEventData.InputButton.Left};var right=new PointerEventData(EventSystem.current){pointerId=32,delta=new Vector2(80,0),button=PointerEventData.InputButton.Left};var other=new PointerEventData(EventSystem.current){pointerId=33};
                move.OnPointerDown(left);float yaw=s.player.look.Yaw;look.OnPointerDown(right);look.OnDrag(right);yield return Wait(.1f);check(s.player.Move.x>.1f&&Mathf.Abs(Mathf.DeltaAngle(yaw,s.player.look.Yaw))>1,"Independent move and look fingers operate simultaneously");
                move.OnPointerUp(other);look.OnPointerUp(other);check(s.player.Move.x>.1f,"Third finger release does not cancel movement ownership");move.OnPointerUp(left);check(s.player.Move==Vector2.zero,"Movement owner release stops movement");
                yaw=s.player.look.Yaw;look.OnDrag(right);yield return Wait(.1f);check(Mathf.Abs(Mathf.DeltaAngle(yaw,s.player.look.Yaw))>1,"Look finger continues after movement finger releases");
                s.player.SetBlocked(true);yield return Wait(.1f);s.player.SetBlocked(false);yaw=s.player.look.Yaw;look.OnDrag(right);yield return Wait(.1f);check(Mathf.Abs(Mathf.DeltaAngle(yaw,s.player.look.Yaw))<.01f,"Modal interruption invalidates old look gesture until new press");
                move.OnPointerDown(left);move.SendMessage("OnApplicationPause",true);check(s.player.Move==Vector2.zero,"Touch pause resets joystick and pointer ownership");look.OnPointerUp(right);
            }
            finally{s.FlushSaves();s.persistence=false;s.Restore(initial);s.player.SetBlocked(false);s.player.SendMessage("OnApplicationFocus",true);s.saveName=oldName;s.persistence=persistence;foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
        }
    }
}

