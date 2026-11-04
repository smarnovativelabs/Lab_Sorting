using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    // Runtime-only fixtures; no doorway or relocated item is saved into SortingTest.
    public static class HeightReachChecks
    {
        static IEnumerator Wait(float seconds)
        {
            float end=Time.time+seconds,deadline=Time.realtimeSinceStartup+20;
            while(Time.time<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Height test frame clock stopped");yield return null;}
        }
        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;return go;
        }
        static void MovePlayer(SortingSession s,Vector3 position)
        {
            s.player.Clear();s.player.character.Motor.BaseVelocity=Vector3.zero;s.player.character.Motor.SetPositionAndRotation(position,Quaternion.identity);s.player.look.SetYawPitch(Vector2.zero);Physics.SyncTransforms();
        }
        static IEnumerator Aim(SortingSession s,Vector3 point)
        {
            s.player.look.SnapToDirection(point-s.player.view.transform.position);Physics.SyncTransforms();yield return Wait(.30f);
        }
        public static IEnumerator Run(SortingSession s,Action<bool,string> check)
        {
            var initial=s.Capture();bool persist=s.persistence;s.persistence=false;var player=s.player;var capsule=player.character.Motor.Capsule;
            var bay=new GameObject("Runtime height/reach test bay");bay.transform.position=new Vector3(30,0,0);
            Box("Test floor",bay.transform,new Vector3(0,-.15f,0),new Vector3(10,.3f,12));
            var left=Box("Left jamb",bay.transform,Vector3.zero,new Vector3(.3f,2.5f,.4f));
            var right=Box("Right jamb",bay.transform,Vector3.zero,new Vector3(.3f,2.5f,.4f));
            var beam=Box("Header",bay.transform,Vector3.zero,new Vector3(2,.3f,.4f));
            try
            {
                check(Mathf.Abs(capsule.height-1.7f)<.0001f&&Mathf.Abs(capsule.center.y-.85f)<.0001f,"Runtime capsule matches 1.70m height / 0.85m center");
                check(Mathf.Abs(player.transform.InverseTransformPoint(player.view.transform.position).y-1.6f)<.001f,"Runtime camera remains 1.60m above player feet");
                float[] heights={2f,1.75f,1.65f,2f};float[] widths={1f,1f,1f,.55f};
                for(int i=0;i<heights.Length;i++)
                {
                    float h=heights[i],w=widths[i];left.transform.localPosition=new Vector3(-w*.5f-.15f,1.25f,0);right.transform.localPosition=new Vector3(w*.5f+.15f,1.25f,0);beam.transform.localPosition=new Vector3(0,h+.15f,0);beam.transform.localScale=new Vector3(w+.6f,.3f,.4f);
                    MovePlayer(s,bay.transform.position+new Vector3(0,.03f,-2));yield return Wait(.3f);player.SetMove(Vector2.up);yield return Wait(1.5f);player.SetMove(Vector2.zero);yield return Wait(.12f);
                    float z=player.transform.position.z;bool expectedPass=i<2;
                    // Rounded capsule can approach narrow jamb corners more closely than a flat wall.
                    // Its center must remain in front of the doorway's -0.20m near face.
                    check(expectedPass?z>1f:(z<-.2f&&z>-1.4f),"Doorway "+w.ToString("0.00")+"m wide / "+h.ToString("0.00")+"m high "+(expectedPass?"passes":"blocks")+" (z="+z.ToString("0.000")+")");
                    check(player.transform.position.y>-.01f&&player.transform.position.y<.04f,"Door traversal keeps feet grounded");
                }
                left.SetActive(false);right.SetActive(false);beam.SetActive(false);
                s.Restore(initial);MovePlayer(s,bay.transform.position+new Vector3(0,.03f,-2));yield return Wait(.3f);
                var item=s.items[0];item.Snap(bay.transform.position+new Vector3(0,item.originalScale.y*.5f+.02f,0),Quaternion.identity);
                yield return Aim(s,item.transform.position);check(s.TargetItem==item,"Floor item can be aimed at from 1.60m eye height");s.PickTarget();DOTween.CompleteAll();check(s.Active==item,"Actual pick action reaches floor item at 2m horizontal distance");
                s.Restore(initial);MovePlayer(s,bay.transform.position+new Vector3(0,.03f,-2));yield return Wait(.3f);
                item.Snap(player.view.transform.position+Vector3.forward*(s.reach+.5f),Quaternion.identity);yield return Aim(s,item.transform.position);
                check(s.TargetItem!=item,"Item beyond 3.5m raycast reach is not targeted");s.PickTarget();check(s.carried.Count==0,"Out-of-range pick consumes no item");
                item.Snap(player.view.transform.position+Vector3.forward*(s.reach-.15f),Quaternion.identity);yield return Aim(s,item.transform.position);s.PickTarget();DOTween.CompleteAll();check(s.Active==item,"Item just inside 3.5m raycast reach can be picked");
                s.Restore(initial);MovePlayer(s,bay.transform.position+new Vector3(0,.03f,-2));yield return Wait(.3f);
                item.Snap(player.view.transform.position+Vector3.forward*2.3f,Quaternion.identity);
                var blocker=Box("Occlusion wall",bay.transform,new Vector3(0,1.5f,-.7f),new Vector3(2,3,.2f));
                yield return Aim(s,item.transform.position);s.PickTarget();check(s.TargetItem!=item&&s.carried.Count==0,"Pick cannot pass through a wall");blocker.SetActive(false);
                // Check all real prototype racks, each shelf and each slot from a practical working distance.
                foreach(var rack in s.racks)
                {
                    s.Restore(initial);MovePlayer(s,rack.transform.position+new Vector3(0,.03f,-2.6f));yield return Wait(.3f);player.look.SetYawPitch(new Vector2(0,8));yield return Wait(.1f);
                    for(int slot=0;slot<rack.slots.Length;slot++)
                    {
                        Vector3 point=rack.slots[slot].position;Vector3 viewport=player.view.WorldToViewportPoint(point);float distance=Vector3.Distance(player.view.transform.position,point);
                        bool visible=viewport.z>0&&viewport.x>.02f&&viewport.x<.98f&&viewport.y>.02f&&viewport.y<.98f;
                        bool obstructed=Physics.Raycast(player.view.transform.position,(point-player.view.transform.position).normalized,out var obstruction,distance-.04f,~(1<<2),QueryTriggerInteraction.Ignore);
                        check(visible&&!obstructed,rack.id+" shelf "+(slot/5+1)+" slot "+slot+" visible from unchanged eye level");
                    }
                    var labelPoint=player.view.WorldToViewportPoint(rack.label.transform.position);check(labelPoint.z>0&&labelPoint.y>0&&labelPoint.y<1,rack.id+" label remains in view");
                    for(int tier=0;tier<3;tier++)
                    {
                        s.Restore(initial);MovePlayer(s,rack.transform.position+new Vector3(0,.03f,-2.6f));yield return Wait(.3f);s.TryPick(item);DOTween.CompleteAll();
                        var shelf=rack.transform.Find("Shelf "+tier);Vector3 aim=shelf.GetComponent<Collider>().bounds.center;aim.z=shelf.GetComponent<Collider>().bounds.min.z;
                        yield return Aim(s,aim);check(s.TargetRack==rack,rack.id+" tier "+(tier+1)+" reachable by placement raycast");s.PlaceTarget();DOTween.CompleteAll();check(item.location==ItemLocation.Rack&&rack.Count==1,rack.id+" tier "+(tier+1)+" accepts actual place action");
                        // Allocation is rack-wide. Relocate the same placed item to each tier to test removal reach.
                        rack.Remove(item);int slot=tier*5+2;rack.Restore(item,slot);item.Snap(rack.slots[slot].position,Quaternion.identity);Physics.SyncTransforms();
                        yield return Aim(s,item.transform.position);check(s.TargetItem==item,rack.id+" tier "+(tier+1)+" placed item can be targeted");s.PickTarget();DOTween.CompleteAll();check(s.Active==item&&rack.Count==0,rack.id+" tier "+(tier+1)+" placed item can be retrieved");
                    }
                }
                var target=s.racks[2];s.Restore(initial);MovePlayer(s,target.transform.position+new Vector3(0,.03f,-4.4f));yield return Wait(.3f);s.TryPick(item);DOTween.CompleteAll();
                var face=target.transform.Find("Shelf 1").GetComponent<Collider>().bounds;Vector3 targetPoint=new Vector3(face.center.x,face.center.y,face.min.z);
                yield return Aim(s,targetPoint);s.PlaceTarget();check(s.TargetRack!=target&&target.Count==0&&s.Active==item,"Out-of-range placement preserves carried item");
                MovePlayer(s,target.transform.position+new Vector3(0,.03f,-2.6f));yield return Wait(.3f);
                blocker.transform.position=target.transform.position+new Vector3(0,1.5f,-1.3f);blocker.SetActive(true);yield return Aim(s,targetPoint);s.PlaceTarget();check(s.TargetRack!=target&&target.Count==0&&s.Active==item,"Placement cannot pass through a wall");
            }
            finally {player.Clear();s.Restore(initial);s.persistence=persist;bay.SetActive(false);UnityEngine.Object.Destroy(bay);}
        }
    }
}
