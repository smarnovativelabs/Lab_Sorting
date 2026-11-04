using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using DG.Tweening;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class InteractionChecks
    {
        static IEnumerator Wait(float seconds){float end=Time.unscaledTime+seconds,deadline=Time.realtimeSinceStartup+20;while(Time.unscaledTime<end){if(Time.realtimeSinceStartup>deadline)throw new Exception("Interaction frame clock stopped");yield return null;}}
        static IEnumerator Until(Func<bool> done){float deadline=Time.realtimeSinceStartup+8;while(!done()&&Time.realtimeSinceStartup<deadline)yield return null;}
        static void Aim(SortingSession s,Vector3 point){s.player.look.SnapToDirection(point-s.player.view.transform.position);Physics.SyncTransforms();s.RefreshTarget();}
        static void Move(SortingSession s,Vector3 pos){s.player.Clear();s.player.character.Motor.BaseVelocity=Vector3.zero;s.player.character.Motor.SetPositionAndRotation(pos,Quaternion.identity);s.player.look.SetYawPitch(Vector2.zero);Physics.SyncTransforms();}
        public static IEnumerator Run(SortingSession s,Action<bool,string> check)
        {
            var initial=s.Capture();var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary interaction test floor";floor.transform.position=new Vector3(40,-.15f,0);floor.transform.localScale=new Vector3(10,.3f,10);
            var a=s.items[0];var b=s.items[5];var c=s.items[10];
            InputSettings originalInputSettings=null;InputSettings testInputSettings=null;Keyboard testKeyboard=null;var originalBType=b.definition;var originalSlots=s.racks[2].slots;
            var pick=Array.Find(UnityEngine.Object.FindObjectsByType<SortingHoldButton>(FindObjectsSortMode.None),x=>!x.place);
            var place=Array.Find(UnityEngine.Object.FindObjectsByType<SortingHoldButton>(FindObjectsSortMode.None),x=>x.place);
            var pointer=new PointerEventData(EventSystem.current){pointerId=17,button=PointerEventData.InputButton.Left};
            Action setup=()=>{s.Restore(initial);Move(s,new Vector3(40,.03f,0));var eye=s.player.view.transform.position;int i=0;foreach(var item in new[]{a,b,c}){item.Snap(eye+Vector3.forward*(1.4f+i++*.5f),Quaternion.identity);item.transform.localScale=Vector3.one*.08f;}Physics.SyncTransforms();s.RefreshTarget();};
            try
            {
                check(pick&&place,"Saved scene has pick/place hold controls");
                check(pick.GetComponent<UnityEngine.UI.Button>().onClick.GetPersistentEventCount()==0&&place.GetComponent<UnityEngine.UI.Button>().onClick.GetPersistentEventCount()==0,"Hold buttons have no duplicate release click actions");
                setup();yield return Wait(.3f);
                var eye=s.player.view.transform.position;
                a.Snap(eye+new Vector3(.1f,0,1.4f),Quaternion.identity);a.transform.localScale=Vector3.one*.08f;b.gameObject.SetActive(false);c.gameObject.SetActive(false);Physics.SyncTransforms();s.RefreshTarget();
                check(s.TargetItem==a,"Small item slightly outside center ray is assisted");
                a.transform.position=eye+new Vector3(.16f,0,1.4f);Physics.SyncTransforms();s.RefreshTarget();check(s.TargetItem==a,"Target survives small aim jitter within retention margin");
                a.transform.position=eye+new Vector3(.3f,0,1.4f);Physics.SyncTransforms();s.RefreshTarget();check(s.TargetItem!=a,"Target releases after deliberate aim departure");
                a.transform.position=eye+new Vector3(.1f,0,1.4f);b.gameObject.SetActive(true);b.Snap(eye+Vector3.forward*1.1f,Quaternion.identity);b.transform.localScale=Vector3.one*.08f;Physics.SyncTransforms();s.RefreshTarget();check(s.TargetItem==b,"Direct item wins over nearby assisted item");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=eye+Vector3.forward*.7f;wall.transform.localScale=new Vector3(2,2,.1f);Physics.SyncTransforms();s.RefreshTarget();check(!s.TargetItem,"Aim assistance never selects through solid wall");wall.SetActive(false);UnityEngine.Object.Destroy(wall);
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);pick.OnPointerUp(pointer);pick.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return Wait(.8f);check(s.carried.Count==1&&!s.Holding,"Pointer tap picks exactly one with no release duplicate");
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);yield return Until(()=>s.carried.Count==3||!s.Holding);check(s.carried.Count==3,"Holding pick collects consecutive aimed items");pick.OnPointerUp(pointer);check(!s.Holding,"Pointer release ends repetition");
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);pick.OnPointerExit(pointer);yield return Wait(.8f);check(s.carried.Count==1&&!s.Holding,"Dragging off button stops hold (count="+s.carried.Count+", hold="+s.Holding+", message="+s.Message+")");
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);pick.OnPointerUp(new PointerEventData(EventSystem.current){pointerId=99});check(s.Holding,"Unrelated touch release cannot cancel owned pointer");pick.OnPointerUp(pointer);
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);s.player.SetBlocked(true);yield return Wait(.65f);check(s.carried.Count==1&&!s.Holding,"Modal blocking cancels hold");s.player.SetBlocked(false);pick.OnPointerUp(pointer);
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);s.SendMessage("OnApplicationFocus",false);yield return Wait(.65f);check(s.carried.Count==1&&!s.Holding,"Focus loss cancels hold");pick.OnPointerUp(pointer);
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);s.SendMessage("OnApplicationPause",true);yield return Wait(.65f);check(s.carried.Count==1&&!s.Holding,"Application pause cancels hold");pick.OnPointerUp(pointer);
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);var changed=b.definition;b.definition=s.items[1].definition;yield return Wait(.7f);check(s.carried.Count==1&&!s.Holding,"Pick hold stops before collecting another category");b.definition=changed;pick.OnPointerUp(pointer);
                setup();for(int i=20;i<29;i++)s.TryPick(s.items[i]);DOTween.CompleteAll();yield return Wait(.3f);pick.OnPointerDown(pointer);yield return Wait(.8f);check(s.carried.Count==10&&!s.Holding,"Pick hold stops at capacity");pick.OnPointerUp(pointer);
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);s.Restore(initial);yield return Wait(.65f);check(!s.Holding&&s.carried.Count==0,"Restore cancels outstanding hold");pick.OnPointerUp(pointer);
                var rack=s.racks[2];
                Action placeSetup=()=>{s.Restore(initial);Move(s,rack.transform.position+new Vector3(0,.03f,-2.6f));foreach(var item in new[]{a,b,c})s.TryPick(item);DOTween.CompleteAll();var bounds=rack.transform.Find("Shelf 0").GetComponent<Collider>().bounds;Aim(s,new Vector3(bounds.center.x,bounds.center.y,bounds.min.z));};
                placeSetup();yield return Wait(.3f);place.OnPointerDown(pointer);place.OnPointerUp(pointer);yield return Wait(.8f);check(rack.Count==1&&s.carried.Count==2,"Place tap places exactly one item");
                placeSetup();yield return Wait(.3f);place.OnPointerDown(pointer);yield return Until(()=>!s.Holding);check(rack.Count==3&&s.carried.Count==0&&!s.Holding,"Place hold empties matching carried items and stops (rack="+rack.Count+", held="+s.carried.Count+", holding="+s.Holding+")");place.OnPointerUp(pointer);
                placeSetup();s.TryPick(s.items[1]);DOTween.CompleteAll();rack.Add(s.items[2]);yield return Wait(.3f);place.OnPointerDown(pointer);yield return Wait(.7f);check(rack.Count==1&&s.carried.Count==4&&!s.Holding,"Wrong rack rejects hold without consuming held items");place.OnPointerUp(pointer);
                placeSetup();var slots=rack.slots;rack.slots=new[]{slots[0]};rack.Initialize();yield return Wait(.3f);place.OnPointerDown(pointer);yield return Wait(.8f);check(rack.Count==1&&s.carried.Count==2&&!s.Holding,"Place hold stops when rack becomes full");place.OnPointerUp(pointer);rack.slots=slots;
                placeSetup();yield return Wait(.3f);s.ThrowActive();yield return Wait(.9f);check(s.carried.Count==2&&!s.Holding,"Single throw drops only one item without repetition");
                check(Array.Find(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None),x=>x.name=="THROW").GetComponent<SortingHoldButton>()==null,"Throw button remains single-click only");
                setup();yield return Wait(.3f);pick.OnPointerDown(pointer);s.player.look.SetYawPitch(new Vector2(90,0));yield return Wait(.7f);check(s.carried.Count==1&&!s.Holding,"Looking away cancels pick hold");Aim(s,b.transform.position);yield return Wait(.5f);check(s.carried.Count==1,"Reaiming after cancellation does not resume without new press");pick.OnPointerUp(pointer);
                placeSetup();yield return Wait(.3f);place.OnPointerDown(pointer);Aim(s,s.racks[1].transform.position+Vector3.up);yield return Wait(.7f);check(rack.Count==1&&s.racks[1].Count==0&&!s.Holding,"Looking at another rack cancels placement hold");place.OnPointerUp(pointer);
                // Batch Editor has no focused Game View. Route synthetic events to gameplay only
                // for this test, using an unsaved settings clone and restoring it in finally.
                originalInputSettings=InputSystem.settings;testInputSettings=UnityEngine.Object.Instantiate(originalInputSettings);InputSystem.settings=testInputSettings;
                testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
                testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                testKeyboard=InputSystem.AddDevice<Keyboard>();
                setup();yield return Wait(.3f);InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.E));yield return Wait(.1f);InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return Wait(.8f);check(s.carried.Count==1&&!s.Holding,"Keyboard E tap picks exactly one (count="+s.carried.Count+", hold="+s.Holding+", keyEnabled="+testKeyboard.enabled+", message="+s.Message+")");
                setup();yield return Wait(.3f);InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.E));yield return Until(()=>s.carried.Count==3);InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return Wait(.1f);check(s.carried.Count==3&&!s.Holding,"Keyboard E hold repeats and releases");
                placeSetup();yield return Wait(.3f);InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.F));yield return Until(()=>rack.Count==3);InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return Wait(.1f);check(rack.Count==3&&!s.Holding,"Keyboard F hold places and releases");
                placeSetup();yield return Wait(.3f);InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Q));yield return Wait(.9f);InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return Wait(.1f);check(s.carried.Count==2,"Holding keyboard Q still throws only once");
            }
            finally{if(testKeyboard!=null)InputSystem.RemoveDevice(testKeyboard);if(originalInputSettings)InputSystem.settings=originalInputSettings;if(testInputSettings)UnityEngine.Object.Destroy(testInputSettings);b.definition=originalBType;s.racks[2].slots=originalSlots;s.player.SetBlocked(false);s.CancelHold();s.Restore(initial);floor.SetActive(false);UnityEngine.Object.Destroy(floor);}
        }
    }
}




