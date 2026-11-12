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
                Check(SearchDirection.Bearing(new Vector3(0,-.9f,.1f),Vector3.right)=="RIGHT >"&&SearchDirection.Bearing(Vector3.forward,Vector3.back)=="TURN AROUND"&&SearchDirection.Bearing(Vector3.forward,Vector3.left)=="< LEFT","Search bearing is independent of camera pitch");
                var first=s.items.First(i=>i.location==ItemLocation.Loose);Check(s.TryPick(first),"Pick search type");DOTween.CompleteAll();Check(s.TrySearch(),"Start selected-type search");
                var economy=UnityEngine.Object.FindFirstObjectByType<EconomyHUD>();economy.SendMessage("Update");Check(economy.marker.text.Contains("FLOOR")&&economy.marker.text.Contains("remaining"),"Direction strip includes floor and remaining category count");
                SortingSmokeChecks.Capture(s,"Logs/SearchFloorFeedback.png");
                s.Restore(snapshot);s.player.SendMessage("OnApplicationFocus",true);
                var rack=s.racks.First(r=>r.CapacityFor(first.definition)>0&&r.CapacityFor(first.definition)<=s.ItemsOfType(first.definition).Count());
                var supply=s.ItemsOfType(first.definition).Take(rack.CapacityFor(first.definition)).ToArray();var feedback=UnityEngine.Object.FindFirstObjectByType<SortingFeedback>();int celebrations=feedback.Celebrations;
                foreach(var item in supply){Check(s.TryPick(item),"Collect rack fixture");DOTween.CompleteAll();Check(s.TryPlace(rack),"Fill rack fixture");DOTween.CompleteAll();}
                var pulse=rack.GetComponent<RackTargetFeedback>();rack.Preview(null);
                Check(rack.Count==rack.Capacity&&pulse.CompletionActive&&feedback.Celebrations==celebrations+1,"Full rack triggers one short pulse and existing chime");
                var rewards=UnityEngine.Object.FindFirstObjectByType<SortingRewardsHUD>();yield return null;Check(rewards.toast.text.Length>0,"Completion feedback reaches HUD");
                yield return new WaitForSecondsRealtime(.85f);Check(!pulse.CompletionActive,"Completion pulse expires");
                int cash=s.Cash;Check(s.TryPick(supply[0]),"Retrieve completed rack item");DOTween.CompleteAll();Check(s.TryPlace(rack),"Replace completed item");DOTween.CompleteAll();Check(s.Cash==cash&&feedback.Celebrations==celebrations+1,"Repeating completed rack grants no cash or repeated celebration");
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
