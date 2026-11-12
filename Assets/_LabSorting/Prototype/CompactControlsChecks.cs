using System;
using System.Collections;
using System.IO;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using LabSorting.Sorting;
using LabSorting.UI;
using LabSorting.Services;
namespace LabSorting.Prototype
{
    public static class CompactControlsChecks
    {
        static void Check(bool value,string message){if(!value)throw new Exception("COMPACT: "+message);Debug.Log("COMPACT PASS: "+message);}
        static IEnumerator Wait(float seconds){float until=Time.time+seconds,deadline=Time.realtimeSinceStartup+30;while(Time.time<until){if(Time.realtimeSinceStartup>deadline)throw new Exception("Game clock stalled");yield return null;}}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var initial=s.Capture();string saveName=s.saveName;bool persistence=s.persistence;
            var panel=UnityEngine.Object.FindFirstObjectByType<SupplyPanel>();var commerce=UnityEngine.Object.FindFirstObjectByType<SupplyCommerce>();var locator=UnityEngine.Object.FindFirstObjectByType<ItemLocatorHUD>();
            try
            {
                s.player.SendMessage("OnApplicationFocus",true);for(int i=0;i<45;i++)yield return new WaitForFixedUpdate();
                Check(UnityEngine.Object.FindObjectsByType<ControlGlyph>(FindObjectsSortMode.None).Length>=8,"Compact icon controls wired");
                var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();
                Check(hud.target.rectTransform.anchoredPosition.y<=-110&&hud.targetBackdrop.rectTransform.anchoredPosition==hud.target.rectTransform.anchoredPosition,"Hover name/backplate sit below the aimed item");
                Check(UnityEngine.Object.FindObjectsByType<ControlGlyph>(FindObjectsSortMode.None).Any(g=>g.symbol==ControlGlyph.Symbol.Joystick)&&UnityEngine.Object.FindObjectsByType<ControlGlyph>(FindObjectsSortMode.None).Any(g=>g.symbol==ControlGlyph.Symbol.Thumb),"Round joystick base and thumb wired");
                Check(s.items.All(i=>(i.visual.bounds.extents.magnitude+Vector3.Distance(i.visual.bounds.center,i.transform.position))*i.CarryScale<=.180f),"All 4032 items fit the held presentation envelope");
                Check(!hud.GetComponent<SortingRewardsHUD>().showMagnetChips,"Magnet uses real models without floating UI squares");
                panel.OpenSearch();Check(panel.IsOpen&&s.player.blocked,"Offer panel blocks movement");int cash=s.Cash;panel.BuyCash();Check(s.SearchCredits==initial.searchCredits+1&&s.Cash==cash-s.searchCost,"Cash buys a persistent search use");
                cash=s.Cash;Check(!commerce.Watch(SupplyKind.Cash)&&!commerce.Buy(SupplyKind.Search)&&s.Cash==cash,"Unconfigured ads and IAP cannot fabricate rewards");
                SortingSmokeChecks.Capture(s,"Logs/CompactSupplyOffers.png");yield return null;panel.Close();Check(!s.player.blocked,"Closing offers restores controls");
                Check(s.GrantSupply(SupplyKind.Magnet,2,"test:magnet")&&s.GrantSupply(SupplyKind.Magnet,2,"test:magnet")&&s.MagnetCredits==initial.magnetCredits+2,"Duplicate reward receipt grants once");
                Check(s.GrantSupply(SupplyKind.Slot,1,"test:slot"),"Slot token delivered");cash=s.Cash;int capacity=s.capacity;Check(s.TryUpgradeCapacity()&&s.capacity==capacity+1&&s.Cash==cash,"Slot token upgrades capacity without cash");
                var snapshot=s.Capture();var writer=new StringWriter();new SaveJsonStream().Write(writer,snapshot);var parsed=JsonUtility.FromJson<SortingSession.Snapshot>(writer.ToString());Check(parsed.searchCredits==snapshot.searchCredits&&parsed.magnetCredits==snapshot.magnetCredits&&parsed.supplyReceipts.Contains("test:magnet"),"Streaming save contains credits and receipt ledger");
                s.saveName="compact-supply-test-"+Guid.NewGuid().ToString("N")+".json";s.persistence=true;Check(s.RequestSave()&&s.FlushSaves(),"Supply transaction durable save succeeds");s.Restore(initial);Check(s.Load()&&s.MagnetCredits==snapshot.magnetCredits&&s.SearchCredits==snapshot.searchCredits,"Disk reload restores supply credits");Check(s.GrantSupply(SupplyKind.Magnet,2,"test:magnet")&&s.MagnetCredits==snapshot.magnetCredits,"Receipt replay after load cannot duplicate reward");s.persistence=false;
                var first=s.items.First(i=>i.location==ItemLocation.Loose);cash=s.Cash;Check(s.TryPick(first),"Pickup for visible magnet");DOTween.CompleteAll();yield return Wait(.3f);
                bool motorEnabled=s.player.character.Motor.enabled;s.player.character.Motor.enabled=false;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary carry clearance wall";
                try
                {
                    wall.transform.SetPositionAndRotation(s.player.view.transform.position+s.player.view.transform.forward*.55f,s.player.view.transform.rotation);wall.transform.localScale=new Vector3(3,3,.1f);Physics.SyncTransforms();yield return Wait(.25f);
                    var near=s.player.view.WorldToViewportPoint(first.visual.bounds.center);
                    Check(near.z-first.visual.bounds.extents.magnitude>s.player.view.nearClipPlane,"Held model remains ahead of camera near plane beside wall");
                    Check(near.z+first.visual.bounds.extents.magnitude<.5f,"Held model retracts before nearby wall plane");
                    var presentation=s.hand.GetComponent<LabSorting.Player.CarryPresentation>();
                    Check(!Physics.CheckSphere(presentation.MagnetEntry(.2f),.16f,1<<wall.layer,QueryTriggerInteraction.Ignore),"Magnet entry avoids nearby wall geometry");
                    wall.transform.position=s.player.view.transform.position+s.player.view.transform.forward*.35f;Physics.SyncTransforms();yield return Wait(.25f);
                    near=s.player.view.WorldToViewportPoint(first.visual.bounds.center);
                    Check(near.z-first.visual.bounds.extents.magnitude>s.player.view.nearClipPlane&&near.z+first.visual.bounds.extents.magnitude<Vector3.Dot(wall.transform.position-s.player.view.transform.position,wall.transform.forward)-.05f,"Tight-wall presentation fits between wall and camera; depth="+near.z+", radius="+first.visual.bounds.extents.magnitude+", scale="+presentation.ClearanceScale+", hand="+s.hand.localPosition);
                }
                finally{wall.SetActive(false);UnityEngine.Object.Destroy(wall);s.player.character.Motor.enabled=motorEnabled;}
                yield return Wait(.35f);
                int uses=s.MagnetCredits;Check(s.TryMagnet()&&s.MagnetCredits==uses-1&&s.Cash==cash,"Magnet consumes one owned use");
                Check(s.carried.Count(i=>i.gameObject.activeSelf&&i.Moving)>1,"Multiple real models visibly travel into the hand");yield return Wait(.13f);SortingSmokeChecks.Capture(s,"Logs/CompactMagnetFlight.png");yield return Wait(1.3f);
                for(int frame=0;frame<120&&s.carried.Any(i=>i.Moving);frame++)yield return null;
                Check(s.Active&&s.Active.gameObject.activeSelf&&s.Active.transform.parent==s.hand&&s.carried.Count(i=>i.gameObject.activeSelf)==1,"Magnet finishes with selected model in hand and no duplicate displays; visible="+s.carried.Count(i=>i.gameObject.activeSelf)+", moving="+s.carried.Count(i=>i.Moving));
                cash=s.Cash;uses=s.MagnetCredits;Check(!s.TryMagnet()&&s.Cash==cash&&s.MagnetCredits==uses,"Full inventory consumes neither cash nor uses");
                Check(s.TrySearch()&&s.Cash==cash,"Owned search use works at unlimited range");
                var eye=s.player.view.transform.position;
                foreach(var item in s.items.Where(i=>i.location==ItemLocation.Loose&&i.definition==s.SearchType).OrderBy(i=>(i.visual.bounds.center-eye).sqrMagnitude))
                {var d=item.visual.bounds.center-eye;if(Vector3.Angle(Vector3.ProjectOnPlane(d,Vector3.up),d)>55)continue;if(Physics.Raycast(eye,d.normalized,out var hit,d.magnitude+.02f,~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<SortItem>()==item){s.player.look.SnapToDirection(d);break;}}
                yield return Wait(.4f);Check(locator.beacons.Any(t=>t.gameObject.activeSelf),"Search displays clear markers over visible matching models");
                var shown=locator.beacons.Where(t=>t.gameObject.activeSelf).ToArray();bool overlap=false;for(int a=0;a<shown.Length;a++)for(int b=a+1;b<shown.Length;b++){var d=shown[a].rectTransform.anchoredPosition-shown[b].rectTransform.anchoredPosition;if(Mathf.Abs(d.x)<104&&Mathf.Abs(d.y)<34)overlap=true;}Check(!overlap,"Search distance labels do not overlap one another");
                SortingSmokeChecks.Capture(s,"Logs/CompactSearchMarkers.png");yield return null;
                s.Restore(initial);yield return Wait(.3f);eye=s.player.view.transform.position;SortItem tap=null;Vector3 point=default;
                foreach(var item in s.items.OrderBy(i=>(i.hitbox.bounds.center-eye).sqrMagnitude)){var d=item.hitbox.bounds.center-eye;if(d.magnitude>s.reach)break;if(Vector3.Angle(Vector3.ProjectOnPlane(d,Vector3.up),d)>55)continue;if(Physics.Raycast(eye,d.normalized,out var h,d.magnitude+.01f,~(1<<2),QueryTriggerInteraction.Ignore)&&h.collider.GetComponentInParent<SortItem>()==item){tap=item;s.player.look.SnapToDirection(d);point=item.hitbox.bounds.center;break;}}
                yield return Wait(.3f);s.RefreshTarget();yield return Wait(.1f);Check(tap&&locator.focusFrame.gameObject.activeSelf,"Hover has animated focus corners; tap="+(tap?tap.name:"null")+", target="+(s.TargetItem?s.TargetItem.name:"null"));SortingSmokeChecks.Capture(s,"Logs/CompactHoverControls.png");yield return null;
                Check(s.TryTouchPick(s.player.view.WorldToScreenPoint(point))&&s.Active==tap,"Direct visible-item tap picks the intended dense-clutter item");DOTween.CompleteAll();yield return Wait(.3f);
                int successes=0;for(int yaw=0;yaw<360;yaw+=45){s.player.look.SetYawPitch(new Vector2(yaw,0));yield return Wait(.1f);if(s.TryThrowPosition(tap,out var landing)){successes++;Check(Vector3.Angle(Vector3.ProjectOnPlane(s.player.view.transform.forward,Vector3.up),Vector3.ProjectOnPlane(landing-s.player.view.transform.position,Vector3.up))<=20.01f,"Drop stays inside the front cone at yaw "+yaw);}}
                Check(successes>0,"Dense scene offers forward landing space; valid directions="+successes);
                var rack=s.racks.First(r=>r.CanAccept(tap.definition));cash=s.Cash;Check(s.TryPlace(rack)&&s.Cash==cash,"Ordinary placement awards zero cash");DOTween.CompleteAll();
                Check(SortingSession.ShortGoalBonus==2&&SortingSession.RackBonus==1&&s.completeBonus==10,"Completion rewards use the reduced economy");
                Debug.Log("COMPACT_CONTROLS_OK");
            }
            finally{if(panel.IsOpen)panel.Close();DOTween.CompleteAll();s.persistence=false;s.Restore(initial);s.InitializeSaveCache();s.saveName=saveName;s.persistence=persistence;}
        }
    }
}


