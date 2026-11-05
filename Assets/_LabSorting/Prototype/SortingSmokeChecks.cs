using System;
using System.Collections;
using System.IO;
using DG.Tweening;
using UnityEngine;
using LabSorting.Sorting;

namespace LabSorting.Prototype
{
    // Invoked by the isolated Unity Test Framework bridge; no NUnit in player code.
    public static class SortingSmokeChecks
    {
        static int checks;
        static void Check(bool value,string message) { if(!value) throw new Exception("SORTING CHECK FAILED: "+message);checks++;Debug.Log("PASS: "+message); }
        public static IEnumerator Run()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<SortingSession>();
            session.persistence=false;
            Application.runInBackground=true;
            // Batch Editor has no focused window; simulate foreground input explicitly.
            session.player.SendMessage("OnApplicationFocus",true);
            yield return null;yield return null;
            var initial=session.Capture();checks=0;
            Check(session.items.Length==50 && session.racks.Length==5,"50 items and 5 racks authored");
            Check(Time.frameCount>1,"Play mode frames advancing");
            for(int i=0;i<10;i++) {Check(session.TryPick(session.items[i]),"Pickup "+i); DOTween.CompleteAll();}
            Check(!session.TryPick(session.items[10])&&session.carried.Count==10,"Capacity rejects item 11");
            Check(session.Active==session.items[9],"Latest pickup selected");
            Check(!session.TryPick(session.items[9]),"Duplicate pickup rejected");
            session.Select(0);Check(session.Active==session.items[0],"Manual selection");
            var rack=session.racks[0];Check(session.TryPlace(rack),"First placement assigns rack");DOTween.CompleteAll();
            Check(rack.Type==session.items[0].definition&&rack.Count==1,"Rack-wide type/count");
            Check(session.Active.definition==session.items[0].definition,"Placement keeps selected category while copies remain");
            session.Select(session.carried.IndexOf(session.items[1]));
            Check(!session.TryPlace(rack),"Wrong category rejected without inventory loss");
            Check(session.carried.Count==9,"Rejected place preserves inventory");
            Check(session.TryPick(session.items[0]),"Placed item can be picked");DOTween.CompleteAll();
            Check(rack.Type==null&&rack.Count==0,"Last removal resets rack");
            session.Select(0);var reassigned=session.Active.definition;
            Check(session.TryPlace(rack),"Empty rack accepts new category");DOTween.CompleteAll();
            Check(rack.Type==reassigned,"Rack reassigned");
            var snapshot=session.Capture();string json=JsonUtility.ToJson(snapshot);
            Check(session.Restore(initial),"Reset to initial snapshot");
            Check(session.Restore(JsonUtility.FromJson<SortingSession.Snapshot>(json)),"Serialized snapshot restores");
            Check(session.carried.Count==9 && rack.Count==1 && session.Active.id==snapshot.active,"Inventory/order/active/rack round trip");
            var bad=JsonUtility.FromJson<SortingSession.Snapshot>(json);bad.items[0].id=bad.items[1].id;
            Check(!session.Restore(bad)&&session.carried.Count==9,"Invalid save rejected without mutation");
            yield return WaitGameSeconds(.3f);
            int count=session.carried.Count;var thrown=session.Active;session.ThrowActive();yield return WaitGameSeconds(.35f);
            Check(session.carried.Count==count-1 && thrown.location==ItemLocation.Loose && thrown.hitbox.enabled,"Throw returns one reachable world item [before="+count+", after="+session.carried.Count+", location="+thrown.location+", moving="+thrown.Moving+", collider="+thrown.hitbox.enabled+", message="+session.Message+", player="+session.player.transform.position+"]");
            Check(session.TryPick(thrown),"Thrown item can be recovered");DOTween.CompleteAll();
            Check(session.Restore(initial),"Clean reset for movement checks");
            var pos=session.player.transform.position;session.player.SetMove(Vector2.up);
            yield return WaitGameSeconds(.6f);session.player.SetMove(Vector2.zero);
            Check(Vector3.Distance(pos,session.player.transform.position)>.5f,"KCC moves player [from="+pos+", to="+session.player.transform.position+", velocity="+session.player.character.Motor.BaseVelocity+"]");
            float yaw=session.player.look.Yaw;session.player.AddLook(new Vector2(100,0));yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw,session.player.look.Yaw))>1,"Drag rotates camera");
            session.player.SetBlocked(true);yield return WaitGameSeconds(.2f);pos=session.player.transform.position;
            session.player.SetMove(Vector2.up);session.player.AddLook(new Vector2(100,0));yield return WaitGameSeconds(.3f);
            Check(Vector3.Distance(pos,session.player.transform.position)<.03f,"Blocked UI stops movement");
            session.player.SetBlocked(false);
            Check(session.Restore(initial),"Final reset");
            yield return EconomyChecks(session,initial);
            yield return PolishChecks(session,initial);
            yield return HeightReachChecks.Run(session,Check);
            yield return InteractionChecks.Run(session,Check);
            yield return RewardsChecks.Run(session,Check,(s,path)=>Capture(s,path));
            yield return SettingsChecks.Run(session,Check,(s,path)=>Capture(s,path));
            yield return RecoveryChecks.Run(session,Check);
            yield return CompletionChecks.Run(session,Check,(s,path)=>Capture(s,path));
            yield return InventoryPolishChecks.Run(session,Check,(s,path)=>Capture(s,path));
            yield return ResumeAndTouchChecks.Run(session,Check);
            yield return AsyncSaveChecks.Run(session,Check);
            yield return FunChecks.Run(session,Check,(s,path)=>Capture(s,path));
            yield return ViewportChecks.Run(session,Check);
            // Disk round trip uses an isolated test save, never the user's progress.
            session.saveName="sorting-automated-test.json";session.persistence=true;
            session.TryPick(session.items[0]);DOTween.CompleteAll();session.Save();session.Restore(initial);
            Check(session.Load()&&session.carried.Count==1,"Disk save/load restores held item");
            session.persistence=false;session.Restore(initial);
            session.TryPick(session.items[0]);DOTween.CompleteAll();session.TrySearch();
            session.player.look.SetYawPitch(new Vector2(0,15));yield return WaitGameSeconds(.2f);
            Capture(session);
            session.Restore(initial);
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/SortingSmokeReport.txt",checks+" checks passed in Unity Play mode. No physical Android device tested.");
            Debug.Log("SORTING_SMOKE_OK: "+checks+" checks");
        }
        static IEnumerator WaitGameSeconds(float seconds)
        {
            float end=Time.time+seconds; float deadline=Time.realtimeSinceStartup+120; int frame=Time.frameCount;
            while(Time.time<end) { if(Time.realtimeSinceStartup>deadline) throw new Exception("Play mode clock stopped"); yield return null; }
            if(Time.frameCount<=frame) throw new Exception("No advancing gameplay frames");
        }
        static IEnumerator EconomyChecks(SortingSession s,SortingSession.Snapshot initial)
        {
            int wallet=s.Cash;
            Check(!s.TrySearch()&&!s.TryMagnet()&&s.Cash==wallet,"No selection means no ability charge");
            s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryPick(s.items[1]);DOTween.CompleteAll();s.Select(0);
            Check(s.TrySearch()&&s.SearchType==s.items[0].definition,"Search follows manual selection instead of newest pickup");
            Check(s.TryMagnet()&&s.Active.definition==s.items[0].definition,"Magnet follows manual selection instead of newest pickup");
            DOTween.CompleteAll();s.Restore(initial);wallet=s.Cash;
            s.TryPick(s.items[0]);DOTween.CompleteAll();
            var type=s.Active.definition;SortItem far=null;
            foreach(var item in s.items) if(item.location==ItemLocation.Loose&&item.definition==type){far=item;break;}
            far.Snap(new Vector3(1000,1,1000),Quaternion.identity);
            Check(s.TrySearch()&&s.SearchType==type&&s.Cash==wallet-s.searchCost,"Search uses selected type and charges once");
            Check(far.outline.enabled,"Search highlights distant matching item");
            wallet=s.Cash;Check(!s.TrySearch()&&s.Cash==wallet,"Repeated active search does not charge");
            Check(s.TryMagnet()&&s.carried.Count==10&&s.Cash==wallet-s.magnetCost,"Unlimited magnet fills free slots for one charge");
            DOTween.CompleteAll();Check(far.location==ItemLocation.Hand,"Magnet reaches distant item beyond walls");
            foreach(var item in s.carried) Check(item.definition==type,"Magnet collects only selected category");
            wallet=s.Cash;Check(!s.TryMagnet()&&s.Cash==wallet,"Full inventory magnet never charges");
            int price=s.UpgradeCost;Check(s.TryUpgradeCapacity()&&s.capacity==11&&s.Cash==wallet-price,"Capacity purchase adds one persistent slot");
            Check(s.UpgradeCost==price+s.upgradeStepCost,"Next upgrade price increases");
            wallet=s.Cash;Check(!s.TryMagnet()&&s.Cash==wallet,"No matching loose items means no charge");
            var saved=s.Capture();s.Restore(initial);Check(s.Restore(saved)&&s.capacity==11&&s.Cash==wallet&&s.carried.Count==10,"Economy and capacity snapshot round trip");
            s.Restore(initial);s.TryPick(s.items[0]);DOTween.CompleteAll();wallet=s.Cash;
            s.TryPlace(s.racks[0]);DOTween.CompleteAll();Check(s.Cash==wallet+s.placementReward,"First placement earns reward");
            s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryPlace(s.racks[0]);DOTween.CompleteAll();Check(s.Cash==wallet+s.placementReward,"Repeated placement cannot farm cash");
            foreach(var item in s.items) if(item.location==ItemLocation.Loose&&item.definition==type){s.TryPick(item);break;}
            DOTween.CompleteAll();Check(s.TryMagnet()&&s.carried.Count==9&&s.racks[0].Count==1,"Magnet leaves placed rack items untouched");
            DOTween.CompleteAll();wallet=s.Cash;Check(!s.TrySearch()&&s.Cash==wallet,"Search without loose matches does not charge");
            s.Restore(initial);
            foreach(var item in s.items) if(item.definition!=type&&s.carried.Count<8){s.TryPick(item);DOTween.CompleteAll();}
            s.TryPick(s.items[0]);DOTween.CompleteAll();wallet=s.Cash;
            Check(s.TryMagnet()&&s.carried.Count==10&&s.Cash==wallet-s.magnetCost,"Partial magnet respects one remaining slot");
            DOTween.CompleteAll();var poor=s.Capture();poor.cash=0;s.Restore(poor);
            Check(!s.TrySearch()&&!s.TryUpgradeCapacity()&&s.Cash==0,"Insufficient funds reject search and upgrade");
            s.Restore(initial);s.TryPick(s.items[0]);DOTween.CompleteAll();poor=s.Capture();poor.cash=0;s.Restore(poor);
            Check(!s.TryMagnet()&&s.carried.Count==1&&s.Cash==0,"Insufficient funds never collect magnet items");
            var capped=s.Capture();capped.cash=10000;capped.capacity=s.maxCapacity;s.Restore(capped);wallet=s.Cash;
            Check(!s.TryUpgradeCapacity()&&s.Cash==wallet,"Maximum capacity prevents extra purchase");
            s.Restore(initial);s.TryPick(s.items[0]);DOTween.CompleteAll();s.searchDuration=1;s.TrySearch();
            yield return WaitGameSeconds(1.2f);Check(!s.SearchType,"Search expires cleanly");s.searchDuration=8;
            s.Restore(initial);s.TryUpgradeCapacity();s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryPlace(s.racks[0]);DOTween.CompleteAll();wallet=s.Cash;
            s.saveName="sorting-automated-test.json";s.persistence=true;s.Save();s.Restore(initial);
            Check(s.Load()&&s.Cash==wallet&&s.capacity==11,"Disk restores cash and purchased capacity");
            s.TryPick(s.items[0]);DOTween.CompleteAll();s.TryPlace(s.racks[0]);DOTween.CompleteAll();Check(s.Cash==wallet,"Reward history persists across disk load");
            s.persistence=false;s.Restore(initial);
            var ui=UnityEngine.Object.FindFirstObjectByType<LabSorting.UI.EconomyHUD>();Check(ui&&ui.searchButton&&ui.magnetButton&&ui.upgradeButton,"Economy controls wired in scene");
            ui.upgradeButton.onClick.Invoke();Check(s.capacity==11,"Upgrade UI invokes purchase");
            s.TryPick(s.items[0]);DOTween.CompleteAll();ui.searchButton.onClick.Invoke();Check(s.SearchType==type,"Search UI invokes ability");
            ui.magnetButton.onClick.Invoke();DOTween.CompleteAll();Check(s.carried.Count==10,"Magnet UI invokes collection");
            var legacy=s.Capture();legacy.version=1;legacy.cash=-10;Check(s.Restore(legacy)&&s.Cash==200,"Legacy save migrates to starting cash");
            var invalid=s.Capture();invalid.cash=-1;Check(!s.Restore(invalid)&&s.Cash==200,"Negative wallet save rejected atomically");
            s.Restore(initial);
        }
        static IEnumerator PolishChecks(SortingSession s,SortingSession.Snapshot initial)
        {
            var panel=UnityEngine.Object.FindFirstObjectByType<LabSorting.UI.ItemsProgressPanel>();var feedback=s.GetComponent<SortingFeedback>();
            Check(panel&&feedback&&feedback.pick&&feedback.place&&feedback.drop&&feedback.reject,"Polish UI and four audio clips wired");
            var rows=s.GetItemProgress();int total=0;foreach(var row in rows){total+=row.total;Check(row.sorted==0&&row.Remaining==row.total,"Initial per-type remaining count");}Check(total==s.items.Length,"Progress totals conserve all items");
            var cues=new System.Collections.Generic.List<SortingCue>();System.Action<SortingCue> listener=cue=>cues.Add(cue);s.Feedback+=listener;
            bool oldSound=feedback.SoundEnabled,oldHaptics=feedback.HapticsEnabled;
            try
            {
                feedback.SetSound(true,false);feedback.SetHaptics(false,false);yield return WaitGameSeconds(.2f);int sounds=feedback.PlayedSounds;
                s.TryPick(s.items[0]);Check(s.Active.Moving&&!s.Active.hitbox.enabled,"Pickup animation locks collider until ownership is settled");
                var socket=s.hand.localPosition;s.hand.localPosition+=Vector3.right*.04f;yield return WaitGameSeconds(.25f);
                Check(Vector3.Distance(s.Active.transform.position,s.hand.position)<.005f,"Pickup follows moving hand socket");s.hand.localPosition=socket;
                Check(Vector3.Distance(s.Active.transform.localScale,s.Active.originalScale*s.Active.heldScale)<.001f,"Held model uses compact scale");
                Check(feedback.PlayedSounds==sounds+1&&cues.Contains(SortingCue.Pick),"Successful pickup emits sound and feedback event");
                rows=s.GetItemProgress();var selected=rows.Find(r=>r.type==s.items[0].definition);Check(selected.held==1&&selected.sorted==0&&selected.Remaining==selected.total,"Held items remain unsorted in panel");
                s.TryPlace(s.racks[0]);DOTween.CompleteAll();Check(cues.Contains(SortingCue.Place)&&s.items[0].transform.localScale==s.items[0].originalScale,"Placement feedback and world scale restored");
                selected=s.GetItemProgress().Find(r=>r.type==s.items[0].definition);Check(selected.sorted==1&&selected.Remaining==selected.total-1&&selected.held==0,"Placement updates sorted and remaining counts");
                s.TryPick(s.items[1]);DOTween.CompleteAll();Check(!s.TryPlace(s.racks[0])&&cues.Contains(SortingCue.Reject),"Invalid placement emits rejection feedback");
                s.TryPick(s.items[0]);DOTween.CompleteAll();selected=s.GetItemProgress().Find(r=>r.type==s.items[0].definition);Check(selected.sorted==0&&selected.Remaining==selected.total,"Taking a rack item reverses sorted count");
                yield return WaitGameSeconds(.3f);var thrown=s.Active;s.ThrowActive();yield return WaitGameSeconds(.35f);
                Check(cues.Contains(SortingCue.Throw)&&thrown.location==ItemLocation.Loose&&thrown.hitbox.enabled&&thrown.transform.localScale==thrown.originalScale,"Throw finishes with feedback, world scale and recoverable collider");
                panel.openButton.onClick.Invoke();yield return WaitGameSeconds(.18f);Check(panel.IsOpen&&s.player.blocked&&panel.group.alpha>.99f,"Progress button opens modal and blocks gameplay");
                int count=s.carried.Count,cash=s.Cash;var active=s.Active;s.player.SetMove(Vector2.one);
                Check(!s.TryPick(thrown)&&!s.TryPlace(s.racks[0])&&!s.TryMagnet()&&!s.TrySearch()&&!s.TryUpgradeCapacity(),"Modal rejects all sorting/economy actions");
                s.ThrowActive();s.Cycle();Check(s.carried.Count==count&&s.Cash==cash&&s.Active==active&&s.player.Move==Vector2.zero,"Modal preserves inventory, cash, selection and movement");
                Check(panel.rows[0].text.Contains("remaining")&&panel.summary.text.Contains("/ 50"),"Progress rows and total summary populated");
                Capture(s,"Logs/ItemsProgress.png");
                panel.closeButton.onClick.Invoke();Check(!panel.IsOpen&&!s.player.blocked,"Close button restores gameplay");
                s.player.SetBlocked(true);panel.Open();panel.Close();Check(s.player.blocked,"Panel preserves a pre-existing input block");s.player.SetBlocked(false);
                s.Restore(initial);s.TryPick(s.items[0]);s.Restore(initial);Check(!s.items[0].Moving&&s.items[0].location==ItemLocation.Loose&&s.items[0].hitbox.enabled,"Restore safely cancels in-flight pickup");
                yield return WaitGameSeconds(.2f);feedback.SetSound(false,false);sounds=feedback.PlayedSounds;s.TryPick(s.items[0]);DOTween.CompleteAll();Check(feedback.PlayedSounds==sounds,"Sound mute suppresses playback without blocking pickup");
                yield return WaitGameSeconds(.2f);feedback.SetSound(true,false);int accepted=feedback.AcceptedCues;s.TryMagnet();DOTween.CompleteAll();Check(feedback.AcceptedCues==accepted+1,"Magnet batch feedback is throttled to one response");
                total=0;int sorted=0,held=0;foreach(var row in s.GetItemProgress()){total+=row.total;sorted+=row.sorted;held+=row.held;}
                Check(total==50&&sorted==s.SortedCount&&held==s.carried.Count,"Progress remains consistent after bulk collection");
                s.Restore(initial);s.TryPick(s.items[0]);s.saveName="sorting-automated-test.json";s.persistence=true;s.Save();s.Restore(initial);Check(s.Load()&&s.Active&&!s.Active.Moving&&s.Active.transform.localScale==s.Active.originalScale*s.Active.heldScale,"Save during pickup restores completed compact hand pose");s.persistence=false;s.Restore(initial);
                var originals=new ItemDefinition[7];var temporary=new ItemDefinition[7];
                try
                {
                    for(int i=0;i<7;i++){originals[i]=s.items[5+i].definition;temporary[i]=ScriptableObject.CreateInstance<ItemDefinition>();temporary[i].displayName="Test type "+i;s.items[5+i].definition=temporary[i];}
                    panel.Open();Check(panel.nextButton.interactable&&panel.pageLabel.text=="1 / 2","Progress paginates more than six categories");panel.nextButton.onClick.Invoke();Check(panel.pageLabel.text=="2 / 2"&&!panel.nextButton.interactable,"Next page shows remaining categories");panel.previousButton.onClick.Invoke();Check(panel.pageLabel.text=="1 / 2","Previous page returns correctly");panel.Close();
                }
                finally {for(int i=0;i<7;i++){s.items[5+i].definition=originals[i];if(temporary[i]) UnityEngine.Object.Destroy(temporary[i]);}}
            }
            finally {s.Feedback-=listener;feedback.SetSound(oldSound,false);feedback.SetHaptics(oldHaptics,false);panel.Close();s.player.SetBlocked(false);s.persistence=false;s.Restore(initial);}
        }
        static void Capture(SortingSession session,string path="Logs/SortingTest.png")
        {
            var camera=session.player.view;var canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>();var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;
            var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            float oldPlane=canvas.planeDistance;
            try {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.01f;Canvas.ForceUpdateCanvases();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally {camera.targetTexture=oldTarget;RenderTexture.active=oldActive;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(texture);}
        }
    }
}









