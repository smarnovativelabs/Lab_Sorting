using System;
using System.Collections;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
using UnityEngine;
namespace LabSorting.Prototype
{
    public static class SoftInteractionChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("SOFT INTERACTION FAILED: "+message);Debug.Log("SOFT PASS: "+message);}
        static IEnumerator Wait(float seconds){float end=Time.time+seconds,deadline=Time.realtimeSinceStartup+15;while(Time.time<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Player loop stopped");yield return null;}}
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;return go;}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();s.persistence=false;
            var ui=UnityEngine.Object.FindFirstObjectByType<ContextActionHUD>();var original=s.Capture();var racks=s.racks;
            var root=new GameObject("Disposable interaction checks");root.transform.position=new Vector3(1000,1000,1000);
            try
            {
                Box(root.transform,"Floor",new Vector3(0,-.1f,2),new Vector3(20,.2f,20));
                var rackObject=new GameObject("Gap test rack");rackObject.transform.SetParent(root.transform,false);rackObject.transform.localPosition=new Vector3(0,0,3);
                var rack=rackObject.AddComponent<SortRack>();rack.id="test-gap-rack";
                Box(rack.transform,"Left post",new Vector3(-.9f,1,0),new Vector3(.1f,2,.5f));
                Box(rack.transform,"Right post",new Vector3(.9f,1,0),new Vector3(.1f,2,.5f));
                for(int row=0;row<3;row++)Box(rack.transform,"Shelf",new Vector3(0,.1f+row*.65f,0),new Vector3(1.8f,.06f,.5f));
                var slot=new GameObject("Slot").transform;slot.SetParent(rack.transform,false);slot.localPosition=new Vector3(0,.2f,0);rack.slots=new[]{slot};rack.Initialize();s.racks=new[]{rack};
                s.player.Clear();s.player.character.Motor.SetPositionAndRotation(root.transform.position+Vector3.up*.03f,Quaternion.identity);Physics.SyncTransforms();yield return Wait(.2f);
                s.player.look.SetYawPitch(new Vector2(0,-75));yield return Wait(.15f);ui.Refresh();
                Check(ui.place.alpha==0&&ui.drop.alpha==0,"Empty hands hide PLACE and DROP");
                var item=s.items[0];Check(s.TryPick(item),"Pick an actual designer item");DOTween.CompleteAll();yield return Wait(.3f);ui.Refresh();
                Check(ui.place.alpha==0&&ui.drop.alpha==1,"Carrying off-rack shows DROP only");
                Check(ui.drop.GetComponentInChildren<UnityEngine.UI.Text>().text=="DROP"&&!ui.TossShown,"DROP label stays consistent");
                foreach(float x in new[]{-.7f,0,.7f})foreach(float y in new[]{.25f,.5f,.9f,1.15f,1.6f})
                {
                    var target=rack.transform.TransformPoint(x,y,0);s.player.look.SnapToDirection(target-s.player.view.transform.position);yield return Wait(.08f);s.RefreshTarget();ui.Refresh();
                    Check(s.TargetRack==rack&&ui.place.alpha==1,"Rack silhouette stays targetable at "+x+", "+y);
                }
                var behind=Box(root.transform,"Second rack behind a gap",new Vector3(0,1,3.4f),new Vector3(1.8f,2,.05f));
                var backRack=behind.AddComponent<SortRack>();backRack.slots=new[]{slot};backRack.Initialize();s.racks=new[]{rack,backRack};Physics.SyncTransforms();
                s.player.look.SnapToDirection(rack.transform.TransformPoint(0,.95f,0)-s.player.view.transform.position);yield return Wait(.12f);s.RefreshTarget();
                Check(s.TargetRack==rack,"A second rack behind a gap cannot steal the nearest rack hover");behind.SetActive(false);s.racks=new[]{rack};
                var wall=Box(root.transform,"Opaque wall",new Vector3(0,1,1.5f),new Vector3(3,3,.15f));Physics.SyncTransforms();yield return Wait(.1f);s.RefreshTarget();ui.Refresh();
                Check(!s.TargetRack&&ui.place.alpha==0,"A wall blocks rack assistance and hides PLACE");wall.SetActive(false);Physics.SyncTransforms();
                s.player.look.SetYawPitch(new Vector2(180,20));yield return Wait(.3f);
                Check(s.TryThrowPosition(item,out var destination),"A nearby safe drop point exists");
                float horizontal=Vector2.Distance(new Vector2(destination.x,destination.z),new Vector2(s.player.transform.position.x,s.player.transform.position.z));
                Check(horizontal<=1.05f,"Drop stays close to the player");
                var fx=s.GetComponent<SortingFeedback>();if(fx.dust)fx.dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                int landed=fx.Landings;float startY=item.transform.position.y;s.ThrowActive();yield return Wait(.14f);
                Check(item.location==ItemLocation.Loose&&item.Moving&&item.transform.position.y>startY,"Full-flight throw has a gentle upward release");
                Check(!item.hitbox.enabled,"Throw flight keeps its collider disabled until landing");
                yield return Wait(1.9f);ui.Refresh();
                Check(!item.Moving&&item.hitbox.enabled&&fx.Landings==landed+1,"Physics throw settles exactly once");
                Check(!fx.dust||fx.dust.particleCount<=2,"Dust is limited to two subtle particles");
                Check(ui.drop.alpha==0&&ui.place.alpha==0,"Last drop hides both carry actions");
                Check(s.TryPick(s.items[1]),"Pick another item above clutter");DOTween.CompleteAll();yield return Wait(.3f);
                Check(s.TryThrowPosition(s.Active,out var pileDestination),"Existing ground item no longer blocks DROP");
                var second=s.Active;s.ThrowActive();float settleLimit=Time.realtimeSinceStartup+10;while(second.Moving&&Time.realtimeSinceStartup<settleLimit)yield return null;Check(!s.Active&&!second.Moving,"DROP beside clutter releases and settles item");
                Check(second.hitbox.bounds.min.y-root.transform.position.y<.06f,"Repeated DROP stays on floor rather than stacking");
                Physics.SyncTransforms();bool penetration=Physics.ComputePenetration(item.hitbox,item.transform.position,item.transform.rotation,second.hitbox,second.transform.position,second.transform.rotation,out _,out float depth);
                Check(!penetration||depth<=0,"Repeated DROP has no positive overlap depth; depth="+depth+"; first="+item.transform.position+"; second="+second.transform.position+"; pool="+UnityEngine.Object.FindFirstObjectByType<DropLandingPool>().LastFinishReason);
                Debug.Log("SOFT_INTERACTION_OK");
            }
            finally{DOTween.CompleteAll();s.racks=racks;s.Restore(original);s.InitializeSaveCache();UnityEngine.Object.Destroy(root);}
        }
    }
}

