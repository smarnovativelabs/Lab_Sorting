#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
using LabSorting.Services;
namespace LabSorting.Prototype
{
    public static class GameplayPolishChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("POLISH: "+message);Debug.Log("POLISH PASS: "+message);}
        static SortItem Fixture(Vector3 position,ItemDefinition type)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Target jitter fixture";go.transform.position=position;go.transform.localScale=new Vector3(.1f,.2f,.1f);
            var item=go.AddComponent<SortItem>();item.definition=type;item.hitbox=go.GetComponent<Collider>();item.visual=go.GetComponent<Renderer>();return item;
        }
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var snapshot=s.Capture();bool motor=s.player.character.Motor.enabled;SortItem a=null,b=null;GameObject wall=null;
            try
            {
                Check(s.items.Length==4032,"4032-item designer scene");
                yield return InventorySearchFinishChecks.Run(s);
                s.player.SendMessage("OnApplicationFocus",true);s.player.Clear();s.player.character.Motor.SetPosition(new Vector3(1000,1000,1000));s.player.character.Motor.enabled=false;s.player.look.SetYawPitch(Vector2.zero);
                var origin=s.player.view.transform.position;a=Fixture(origin+new Vector3(-.05f,0,2),s.items[0].definition);b=Fixture(origin+new Vector3(.052f,0,2),s.items[1].definition);Physics.SyncTransforms();
                s.player.look.SetYawPitch(new Vector2(-.15f,0));s.RefreshTarget();Check(s.TargetItem==a,"Acquire left item at dense boundary");
                s.player.look.SetYawPitch(new Vector2(.3f,0));s.RefreshTarget();Check(s.TargetItem==a,"Tiny edge wobble retains previous visible item");
                yield return new WaitForSecondsRealtime(.1f);s.player.SendMessage("OnApplicationFocus",true);s.RefreshTarget();Check(s.TargetItem==b,"Stable new aim switches after bounded grace period");
                s.player.look.SetYawPitch(new Vector2(-2,0));s.RefreshTarget();Check(s.TargetItem==a,"Deliberate camera turn switches immediately");
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=origin+Vector3.forward;wall.transform.localScale=new Vector3(2,2,.1f);Physics.SyncTransforms();s.RefreshTarget();Check(!s.TargetItem,"Retention cannot pick through obstruction");
                wall.SetActive(false);s.player.SetBlocked(true);s.RefreshTarget();Check(!s.TargetItem,"Modal clears target");s.player.SetBlocked(false);
                a.gameObject.SetActive(false);b.gameObject.SetActive(false);s.Restore(snapshot);s.player.SendMessage("OnApplicationFocus",true);Physics.SyncTransforms();
                Check(SearchDirection.Floor(0,0)=="SAME FLOOR"&&SearchDirection.Floor(4,0)=="UPPER FLOOR"&&SearchDirection.Floor(0,4)=="LOWER FLOOR","Search floor classification uses item base and player feet");
                Check(SearchDirection.Bearing(new Vector3(0,-.9f,.1f),Vector3.right)=="TURN RIGHT"&&SearchDirection.Bearing(Vector3.forward,Vector3.back)=="TURN AROUND"&&SearchDirection.Bearing(Vector3.forward,Vector3.left)=="TURN LEFT","Search bearing is independent of camera pitch");
                UnityEngine.Object.FindFirstObjectByType<SortingHUD>().SendMessage("Update");
                var first=s.items.First(i=>i.location==ItemLocation.Loose);Check(s.TryPick(first),"Pick search type");DOTween.CompleteAll();Check(s.TrySearch(),"Start selected-type search");
                var rackGuide=UnityEngine.Object.FindFirstObjectByType<MatchingRackGuide>();Check(rackGuide&&rackGuide.label,"Matching rack guidance wired");
                var suggested=MatchingRackGuide.FindRack(s,first.definition);Check(suggested&&suggested.CanAccept(first.definition),"Held item gets a compatible rack destination");
                var carry=s.hand.GetComponent<LabSorting.Player.CarryPresentation>();
                var carryTick=typeof(LabSorting.Player.CarryPresentation).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var markerTick=typeof(MatchingRackGuide).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var rotation=s.player.view.transform.rotation;float angularSize=0;
                try
                {
                    for(int turn=0;turn<24;turn++)
                    {
                        s.player.view.transform.rotation=Quaternion.Euler(turn%2==0?20:-20,turn*15,0);carryTick.Invoke(carry,null);
                        float depth=s.player.view.WorldToViewportPoint(first.transform.position).z;
                        float projected=first.transform.localScale.magnitude/depth;
                        if(turn==0)angularSize=projected;
                        Check(depth>s.player.view.nearClipPlane&&Mathf.Abs(projected/angularSize-1)<.002f,"Held projected size stable during yaw/pitch and wall clearance "+turn);
                    }
                }
                finally{s.player.view.transform.rotation=rotation;carryTick.Invoke(carry,null);}
                typeof(MatchingRackGuide).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(rackGuide,null);markerTick.Invoke(rackGuide,null);
                Check(rackGuide.MarkerRack==suggested,"Subtle marker uses the actual guided compatible rack");
                var eyePosition=s.player.view.transform.position;var eyeRotation=s.player.view.transform.rotation;
                var markerPoint=(suggested.label?suggested.label.transform.position:suggested.transform.position+Vector3.up*1.4f)+Vector3.up*.22f;
                try
                {
                    s.player.view.transform.position=markerPoint+suggested.transform.forward*.7f;
                    s.player.view.transform.LookAt(markerPoint);typeof(MatchingRackGuide).GetField("nextVisibilityCheck",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(rackGuide,0f);markerTick.Invoke(rackGuide,null);
                    Check(rackGuide.MarkerVisible,"Visible guided rack has a non-interactive diamond marker");
                    SortingSmokeChecks.Capture(s,"Logs/GuidedRackDiamond.png");
                }
                finally{s.player.view.transform.SetPositionAndRotation(eyePosition,eyeRotation);}
                var clearanceWall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    foreach(float wallDistance in new[]{.55f,.35f})
                    {
                        clearanceWall.transform.SetPositionAndRotation(eyePosition+s.player.view.transform.forward*wallDistance,eyeRotation);clearanceWall.transform.localScale=new Vector3(3,3,.1f);Physics.SyncTransforms();carryTick.Invoke(carry,null);
                        var center=s.player.view.WorldToViewportPoint(first.visual.bounds.center);float radius=first.visual.bounds.extents.magnitude;
                        Check(center.z-radius>s.player.view.nearClipPlane&&center.z+radius<wallDistance-.05f,"Stable-size carry clears wall and camera at "+wallDistance);
                    }
                }
                finally{clearanceWall.SetActive(false);UnityEngine.Object.Destroy(clearanceWall);Physics.SyncTransforms();}
                s.player.SetBlocked(true);markerTick.Invoke(rackGuide,null);Check(!rackGuide.MarkerVisible&&!rackGuide.MarkerRack,"Rack marker hides behind modals");s.player.SetBlocked(false);
                s.player.SetBlocked(true);typeof(MatchingRackGuide).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(rackGuide,null);Check(!rackGuide.label.gameObject.activeSelf,"Rack guidance hides behind panels");s.player.SetBlocked(false);
                var economy=UnityEngine.Object.FindFirstObjectByType<EconomyHUD>();economy.SendMessage("Update");Check(economy.marker.text.Contains("FLOOR")&&economy.marker.text.Contains("remaining"),"Direction strip includes floor and remaining category count");
                Check(SearchDirection.Route(4,0)=="FIND STAIRS UP"&&SearchDirection.Route(0,4)=="FIND STAIRS DOWN"&&SearchDirection.Route(0,0)=="FOLLOW THE GLOW","Search explains vertical route without pretending to route through walls");
                var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();hud.SendMessage("Update");Check(hud.ActiveInventoryPulses>0,"Inventory change triggers bounded icon feedback");
                yield return new WaitForSecondsRealtime(.35f);Check(hud.ActiveInventoryPulses==0,"Inventory feedback settles after rapid changes");
                foreach(var icon in hud.inventoryIcons)if(icon)Check((icon.transform.localScale-Vector3.one).sqrMagnitude<.00001f,"Inventory icon returns to undistorted size");
                var guided=economy.GuidedSearchItem;yield return new WaitForSecondsRealtime(.12f);Check(guided&&economy.GuidedSearchItem==guided,"Stationary search retains a stable beacon");
                SortingSmokeChecks.Capture(s,"Logs/SearchFloorFeedback.png");
                s.Restore(snapshot);s.player.SendMessage("OnApplicationFocus",true);
                var rack=s.racks.First(r=>r.CapacityFor(first.definition)>0&&r.CapacityFor(first.definition)<=s.ItemsOfType(first.definition).Count());
                var supply=s.ItemsOfType(first.definition).Take(rack.CapacityFor(first.definition)).ToArray();var feedback=UnityEngine.Object.FindFirstObjectByType<SortingFeedback>();int celebrations=feedback.Celebrations;
                foreach(var item in supply){Check(s.TryPick(item),"Collect rack fixture");DOTween.CompleteAll();Check(s.TryPlace(rack),"Fill rack fixture");DOTween.CompleteAll();}
                var pulse=rack.GetComponent<RackTargetFeedback>();rack.Preview(null);
                Check(rack.Count==rack.Capacity&&pulse.CompletionActive&&feedback.Celebrations==celebrations+1,"Full rack triggers one short pulse and existing chime");
                Check(MatchingRackGuide.FindRack(s,first.definition)!=rack,"Rack guidance excludes full racks");
                var rewards=UnityEngine.Object.FindFirstObjectByType<SortingRewardsHUD>();yield return null;Check(rewards.ability.text.Contains("Rack complete!"),"Rack completion feedback reaches dedicated ability HUD");
                yield return new WaitForSecondsRealtime(.85f);Check(!pulse.CompletionActive,"Completion pulse expires");
                int cash=s.Cash;Check(s.TryPick(supply[0]),"Retrieve completed rack item");DOTween.CompleteAll();Check(s.TryPlace(rack),"Replace completed item");DOTween.CompleteAll();Check(s.Cash==cash&&feedback.Celebrations==celebrations+1,"Repeating completed rack grants no cash or repeated celebration");
                Check(s.TryPick(supply[0]),"Open matching rack slot for guidance");DG.Tweening.DOTween.CompleteAll();Check(MatchingRackGuide.FindRack(s,first.definition)==rack,"Assigned matching rack wins over empty racks");
                for(int i=0;i<8;i++)rewards.SendMessage("Announce","Fixture "+i);
                Check(rewards.PendingAnnouncements==3,"Rapid reward queue is bounded");
                s.player.SetBlocked(true);rewards.SendMessage("Update");Check(rewards.PendingAnnouncements==0&&rewards.toast.text==""&&rewards.ability.text=="","Modal clears stale celebrations");s.player.SetBlocked(false);
                s.Restore(snapshot);s.player.SendMessage("OnApplicationFocus",true);Physics.SyncTransforms();for(int i=0;i<20;i++)s.RefreshTarget();
                long before=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<2000;i++)s.RefreshTarget();watch.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                File.WriteAllText("Logs/GameplayPolishPerformance.txt","4032 items; 2000 warmed target refreshes; mean ms="+(watch.Elapsed.TotalMilliseconds/2000).ToString("F4")+"; allocated bytes="+bytes);
                Check(bytes<4096,"Steady targeting avoids per-refresh managed allocations");
                Check(watch.Elapsed.TotalMilliseconds/2000<5,"Editor target refresh under generous 5ms regression budget");
            }
            finally{if(a)UnityEngine.Object.Destroy(a.gameObject);if(b)UnityEngine.Object.Destroy(b.gameObject);if(wall)UnityEngine.Object.Destroy(wall);DOTween.CompleteAll();s.player.SetBlocked(false);s.player.character.Motor.enabled=motor;s.Restore(snapshot);}
        }
    }
}
#endif
