#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class RapidResumeChecks
    {
        static void Check(bool value,string message){if(!value)throw new Exception("RAPID RESUME: "+message);Debug.Log("RAPID RESUME PASS: "+message);}
        static IEnumerator Wait(Func<bool> done){float deadline=Time.realtimeSinceStartup+5;while(!done()&&Time.realtimeSinceStartup<deadline)yield return null;}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var initial=s.Capture();var racks=s.racks;bool motor=s.player.character.Motor.enabled,persistence=s.persistence;string saveName=s.saveName;
            string testName="rapid-resume-"+Guid.NewGuid().ToString("N")+".json";var report=new System.Text.StringBuilder();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.1f,1000);floor.transform.localScale=new Vector3(20,.2f,20);
            var fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.name="Temporary buffered place rack";fixture.SetActive(false);
            try
            {
                Check(s.items.Length==4032,"4032 designer items");s.player.SendMessage("OnApplicationFocus",true);s.player.Clear();s.player.character.Motor.enabled=false;
                var chosen=s.items.Where(i=>i.location==ItemLocation.Loose).GroupBy(i=>i.definition).Take(3).Select(g=>g.First()).ToArray();
                foreach(var item in chosen){Check(s.TryPick(item),"Pick rapid-switch item");DOTween.CompleteAll();}
                for(int i=0;i<30;i++)
                {
                    s.Select(i%3);Check(s.Active==chosen[i%3]&&!s.Active.Moving,"Inventory selection commits immediately without a gameplay animation lock");
                    Check(s.carried.Count(c=>c.gameObject.activeSelf)==1,"Only selected held item remains visible");
                }
                var selected=s.Active;int revision=s.Revision;s.Select(s.carried.IndexOf(selected));Check(s.Revision==revision&&s.Active==selected,"Repeated selected slot is a no-op");
                var rack=racks.First(r=>r.CanAccept(selected.definition));Check(s.TryPlace(rack),"Immediate place interrupts selection presentation safely");Check(selected.location==ItemLocation.Rack&&!selected.Presenting,"Placement owns transform after selection");DOTween.CompleteAll();
                Check(s.Active&&s.Active.Presenting==false,"Replacement presentation settles cleanly");
                s.Restore(initial);s.player.SendMessage("OnApplicationFocus",true);s.player.character.Motor.SetPosition(new Vector3(1000,.01f,1000));s.player.look.SetYawPitch(Vector2.zero);
                yield return new WaitForSecondsRealtime(.4f); // Let Cinemachine follow the teleported player before placing the aim fixture.
                fixture.SetActive(true);fixture.transform.position=s.player.view.transform.position+Vector3.forward*2;fixture.transform.localScale=new Vector3(2,2,.1f);
                var bufferRack=fixture.AddComponent<SortRack>();bufferRack.id="buffer-fixture";bufferRack.slots=new[]{fixture.transform,fixture.transform,fixture.transform};bufferRack.neutral=racks[0].neutral;bufferRack.valid=racks[0].valid;bufferRack.invalid=racks[0].invalid;bufferRack.Initialize();s.racks=new[]{bufferRack};Physics.SyncTransforms();
                var item0=chosen[0];Check(s.TryPick(item0),"Begin pickup before rapid place tap");s.RefreshTarget();Check(s.TargetRack==bufferRack,"Temporary rack is actually targeted");
                s.BeginHold(true);s.EndHold(true);Check(s.BufferedTap,"One place tap waits for pickup/cooldown");
                yield return Wait(()=>item0.location==ItemLocation.Rack||!s.BufferedTap);Check(item0.location==ItemLocation.Rack&&!s.BufferedTap&&!s.Holding&&bufferRack.Count==1,"Released tap executes exactly once without starting repetition; location="+item0.location+", target="+s.TargetRack+", buffer="+s.BufferedTapState+", moving="+item0.Moving+", blocked="+s.player.blocked+", dt="+Time.unscaledDeltaTime);
                DOTween.CompleteAll();var cancelItem=s.ItemsOfType(item0.definition).First(i=>i.location==ItemLocation.Loose);Check(s.TryPick(cancelItem),"Pickup for cancel test");s.ThrowActive();Check(s.DropPending,"Busy drop queues");s.RefreshTarget();s.BeginHold(true);Check(s.BufferedTap&&!s.DropPending,"New place intent replaces queued drop");s.player.look.SetYawPitch(new Vector2(90,0));s.RefreshTarget();
                yield return Wait(()=>!s.BufferedTap);Check(!s.BufferedTap&&cancelItem.location==ItemLocation.Hand&&bufferRack.Count==1,"Changing aim cancels buffered place without dropping the item");
                s.CancelHold();fixture.SetActive(false);s.racks=racks;s.Restore(initial);s.player.SendMessage("OnApplicationFocus",true);

                // Use a separate real disk save, never the player's save file.
                s.saveName=testName;s.persistence=true;
                Check(s.TryPick(chosen[0]),"Pick selected magnet type");DOTween.CompleteAll();int beforeCash=s.Cash,beforeCredits=s.MagnetCredits;
                Check(s.TryMagnet(),"Magnet begins real held-model arrivals");var expectedHand=s.carried.Select(i=>i.id).ToArray();string expectedActive=s.Active.id;int expectedCash=s.Cash,expectedCredits=s.MagnetCredits;
                Check(s.carried.Any(i=>i.Moving),"Pause occurs during magnet animation");var timer=System.Diagnostics.Stopwatch.StartNew();s.SendMessage("OnApplicationPause",true);timer.Stop();report.AppendLine("Magnet pause+durable save ms="+timer.Elapsed.TotalMilliseconds.ToString("F2"));
                Check(s.carried.All(i=>!i.Moving&&!i.Presenting)&&s.carried.Count(i=>i.gameObject.activeSelf)==1,"Pause settles magnet batch to one visible active item");
                Check(!s.BufferedTap&&!s.Holding&&!s.DropPending&&s.FlushSaves(),"Pause cancels input and flushes save");
                s.Restore(initial);Check(s.Load(),"Load actual magnet pause save");Check(s.Cash==expectedCash&&s.MagnetCredits==expectedCredits&&s.carried.Select(i=>i.id).SequenceEqual(expectedHand)&&s.Active.id==expectedActive,"Cash, credits, inventory order and active item survive disk reload");
                s.SendMessage("OnApplicationPause",false);s.SendMessage("OnApplicationFocus",true);Check(s.Cash==expectedCash&&s.MagnetCredits==expectedCredits,"Resume cannot charge magnet a second time");
                Check(beforeCredits>0?expectedCredits==beforeCredits-1:expectedCash==beforeCash-s.magnetCost,"Magnet consumed exactly one payment");

                s.player.character.Motor.SetPosition(new Vector3(1000,.01f,1000));s.player.look.SetYawPitch(Vector2.zero);s.player.SendMessage("OnApplicationFocus",true);Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.3f);
                var dropped=s.Active;s.ThrowActive();yield return Wait(()=>dropped.location==ItemLocation.Loose);Check(dropped.location==ItemLocation.Loose&&dropped.Moving,"Pause exercise begins during real throw");
                var safeTarget=dropped.SavedPosition;int handCount=s.carried.Count,cash=s.Cash;s.SendMessage("OnApplicationFocus",false);s.SendMessage("OnApplicationPause",true);
                Check(DropLandingPool.ActiveCount==0&&!dropped.Moving&&dropped.hitbox.enabled,"Focus loss and pause settle thrown physics once");Check(Vector3.Distance(dropped.transform.position,safeTarget)<.001f,"Background throw settles at reserved safe landing");
                string droppedId=dropped.id;s.Restore(initial);Check(s.Load(),"Load actual throw pause save");var loaded=s.items.First(i=>i.id==droppedId);Check(loaded.location==ItemLocation.Loose&&Vector3.Distance(loaded.transform.position,safeTarget)<.001f&&s.carried.Count==handCount&&s.Cash==cash,"Drop pose, hand count and cash survive reload");
                s.SendMessage("OnApplicationPause",false);s.SendMessage("OnApplicationFocus",true);s.player.SendMessage("OnApplicationFocus",true);
                s.Select(0);Check(s.Active.Presenting||s.carried.IndexOf(s.Active)==0,"Selection remains usable after resume");s.SendMessage("OnApplicationPause",true);Check(s.carried.All(i=>!i.Moving&&!i.Presenting),"Background during selection leaves no stale tween");s.SendMessage("OnApplicationPause",false);
                var settings=UnityEngine.Object.FindFirstObjectByType<SettingsTutorialPanel>();settings.OpenSettings();s.SendMessage("OnApplicationPause",true);Check(settings.IsOpen&&settings.root.activeSelf&&s.player.blocked,"Background preserves open settings/modal input block");s.SendMessage("OnApplicationPause",false);settings.Close();

                s.persistence=false;s.Restore(initial);s.player.SendMessage("OnApplicationFocus",true);
                var type=chosen[0].definition;var goalRack=racks.First(r=>r.CapacityFor(type)>=25);int startCash=s.Cash;
                foreach(var item in s.ItemsOfType(type).Take(25)){Check(s.TryPick(item),"Short-goal pickup");DOTween.CompleteAll();Check(s.TryPlace(goalRack),"Short-goal placement");DOTween.CompleteAll();}
                var rewards=UnityEngine.Object.FindFirstObjectByType<SortingRewardsHUD>();rewards.SendMessage("Update");Check(rewards.milestone.text.Contains("GOAL COMPLETE")&&s.ShortGoalProgress==0&&s.ShortGoalTarget==25,"Goal completes visibly and advances to next batch");
                yield return new WaitForSecondsRealtime(1.2f);rewards.SendMessage("Update");Check(rewards.milestone.text.Contains("25 new items")&&rewards.shortGoalFill,"Next goal explains remaining items and has a progress bar");
                int goalCash=s.Cash;var repeat=s.ItemsOfType(type).First(i=>i.location==ItemLocation.Rack);Check(s.TryPick(repeat),"Retrieve rewarded item");DOTween.CompleteAll();Check(s.TryPlace(goalRack),"Replace rewarded item");DOTween.CompleteAll();Check(s.Cash==goalCash&&s.ShortGoalProgress==0,"Repeated placement cannot advance or repay short goal");
                SortingSmokeChecks.Capture(s,"Logs/ShortGoalPresentation.png");report.AppendLine("4032-item lifecycle tests passed; unique item IDs="+s.items.Select(i=>i.id).Distinct().Count());File.WriteAllText("Logs/RapidResumeMeasurements.txt",report.ToString());
            }
            finally
            {
                s.CancelHold();s.FlushSaves();s.persistence=false;s.racks=racks;fixture.SetActive(false);floor.SetActive(false);DOTween.CompleteAll();s.player.character.Motor.enabled=motor;s.player.SetBlocked(false);s.Restore(initial);s.InitializeSaveCache();s.saveName=saveName;s.persistence=persistence;UnityEngine.Object.Destroy(fixture);UnityEngine.Object.Destroy(floor);
                foreach(var suffix in new[]{"",".bak",".tmp"}){var path=Path.Combine(Application.persistentDataPath,testName)+suffix;if(File.Exists(path))File.Delete(path);}
            }
        }
    }
}
#endif
