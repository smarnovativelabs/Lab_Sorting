using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class FrontDropChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("FRONT DROP: "+message);Debug.Log("FRONT DROP PASS: "+message);}
        static IEnumerator Wait(float seconds){float end=Time.time+seconds,limit=Time.realtimeSinceStartup+30;while(Time.time<end){if(Time.realtimeSinceStartup>limit)throw new Exception("Frame clock stopped");yield return null;}}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var initial=s.Capture();bool motor=s.player.character.Motor.enabled;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.1f,1000);floor.transform.localScale=new Vector3(16,.2f,16);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.SetActive(false);
            try
            {
                s.player.SendMessage("OnApplicationFocus",true);s.player.Clear();s.player.character.Motor.SetPosition(new Vector3(1000,.01f,1000));s.player.look.SetYawPitch(Vector2.zero);Physics.SyncTransforms();yield return Wait(.4f);s.player.character.Motor.enabled=false;
                var item=s.items.Where(i=>i.location==ItemLocation.Loose).OrderBy(i=>i.visual.bounds.size.sqrMagnitude).First();Check(s.TryPick(item),"Pick fixture item");DOTween.CompleteAll();yield return Wait(.3f);
                Check(s.TryThrowPosition(item,out var end),"Clear front floor accepts landing");
                Check(Vector3.Angle(Vector3.forward,Vector3.ProjectOnPlane(end-s.player.view.transform.position,Vector3.up))<=20.01f,"Landing is within 20 degrees of forward");
                wall.transform.position=s.player.view.transform.position+Vector3.forward*.25f;wall.transform.localScale=new Vector3(20,6,.1f);wall.SetActive(true);Physics.SyncTransforms();
                Check(!s.TryThrowPosition(item,out _),"Solid front wall cannot be bypassed even if the visual hand is beyond it");
                int count=s.carried.Count,cash=s.Cash;string message=s.Message;s.ThrowActive();yield return Wait(.3f);
                Check(!s.DropPending&&item.location==ItemLocation.Loose&&s.carried.Count==count-1&&s.Cash==cash,"Blocked front releases immediately without charging or waiting for movement");
                wall.SetActive(false);Physics.SyncTransforms();s.player.look.SetYawPitch(new Vector2(5,0));yield return Wait(2.1f);
                Check(!s.DropPending&&item.location==ItemLocation.Loose&&item.hitbox.enabled&&s.carried.Count==count-1,"Pending drop releases automatically after aim changes to safe front space");
                Check(s.TryPick(item),"Pick item again");wall.SetActive(true);Physics.SyncTransforms();s.ThrowActive();Check(!s.DropPending&&item.location==ItemLocation.Loose,"Drop during pickup also releases beside a wall");yield return Wait(2.1f);
                Check(item.hitbox.enabled&&!item.Moving,"Repeated crowded release settles and remains pickable");
            }
            finally{wall.SetActive(false);floor.SetActive(false);UnityEngine.Object.Destroy(wall);UnityEngine.Object.Destroy(floor);DOTween.CompleteAll();s.player.SetBlocked(false);s.player.character.Motor.enabled=motor;s.Restore(initial);}
        }
    }
}
