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
    // Smart Place: PLACE on a rack that already holds a carried type selects that type and places it,
    // so the player does not open the inventory first. Empty racks are never claimed for an unselected type.
    public static class SmartPlaceChecks
    {
        const string Folder="Logs/SmartPlace";
        static void Check(bool ok,string text){File.AppendAllText(Folder+"/checks.txt",(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception("SMART PLACE: "+text);}
        static IEnumerator Wait(float seconds=.35f){yield return new WaitForSecondsRealtime(seconds);yield return new WaitForEndOfFrame();}
        static IEnumerator Aim(SortingSession s,SortRack rack,float distance)
        {
            var p=rack.transform.position-rack.transform.forward*distance;p.y=rack.transform.position.y+.03f;s.player.character.Motor.SetPosition(p);
            yield return new WaitForSecondsRealtime(.2f);
            for(int n=0;n<4;n++){s.player.look.SnapToDirection(rack.transform.position+Vector3.up*1.05f-s.player.view.transform.position);yield return null;}
            s.RefreshTarget();yield return Wait();
        }
        static Material FrameMaterial(SortRack rack)=>rack.GetComponentsInChildren<MeshRenderer>(true).First(rack.IsFrameRenderer).sharedMaterial;
        public static IEnumerator Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/checks.txt","");
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var saved=s.Capture();bool persistence=s.persistence;s.persistence=false;
            var gates=UnityEngine.Object.FindObjectsByType<ServicePresentation>(FindObjectsSortMode.None);var gateStates=gates.Select(g=>g.enabled).ToArray();foreach(var gate in gates)gate.enabled=false;
            try
            {
                s.player.SetNetworkBlocked(false);s.player.SetTutorialBlocked(false);s.player.SetBlocked(false);s.player.SetPickTutorialLocked(false);
                // Fixture: rack R holds type B. The player carries two B and one A, with A selected.
                var loose=s.items.Where(i=>i.location==ItemLocation.Loose&&!i.definition.isRat).GroupBy(i=>i.definition).Where(g=>g.Count()>=4).ToArray();
                float y=s.player.transform.position.y;
                SortRack rack=null;ItemDefinition typeB=null;
                foreach(var group in loose)
                {
                    rack=s.racks.Where(r=>r.gameObject.activeInHierarchy&&r.CanAccept(group.Key)&&Mathf.Abs(r.transform.position.y-y)<1&&(r.Type?r.Capacity:r.CapacityFor(group.Key))-r.Count>=4)
                        .OrderBy(r=>(r.transform.position-s.player.transform.position).sqrMagnitude).FirstOrDefault();
                    if(rack){typeB=group.Key;break;}
                }
                Check(rack&&typeB,"Fixture: a same-floor rack with room for four of one type");
                var typeA=loose.Select(g=>g.Key).First(t=>t!=typeB);
                var b=s.items.Where(i=>i.definition==typeB&&i.location==ItemLocation.Loose).Take(3).ToArray();
                var a=s.items.First(i=>i.definition==typeA&&i.location==ItemLocation.Loose);
                Check(s.TryPick(b[0]),"Pick the first "+typeB.displayName);DOTween.CompleteAll();b[0].CompleteMotion();
                Check(s.TryPlace(rack)&&rack.Type==typeB,"Rack now holds "+typeB.displayName);DOTween.CompleteAll();
                Check(s.TryPick(b[1])&&s.TryPick(b[2]),"Carry two more "+typeB.displayName);
                Check(s.TryPick(a),"Carry one "+typeA.displayName+" last, so it is selected");
                DOTween.CompleteAll();yield return Wait();
                Check(s.Active==a&&!rack.CanAccept(typeA)&&s.carried.Count==3,"Fixture: "+typeA.displayName+" selected, rack refuses it");

                Check(s.PlaceableType(rack)==typeB,"Rack offers the carried "+typeB.displayName);
                s.smartPlace=false;Check(!s.PlaceableType(rack),"Smart Place off keeps the old refusal");s.smartPlace=true;
                foreach(var empty in s.racks.Where(r=>r&&!r.Type&&r.gameObject.activeInHierarchy))
                {
                    var offered=s.PlaceableType(empty);
                    Check(offered==(empty.CanAccept(typeA)?typeA:null),"Empty rack "+empty.id+" offers only the selected type");
                }

                foreach(var distance in new[]{2.4f,1.8f,3f}){yield return Aim(s,rack,distance);if(s.TargetRack==rack)break;}
                Check(s.TargetRack==rack,"Aiming at the "+typeB.displayName+" rack");
                var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();
                Check(hud.target.text=="PLACE 2 "+typeB.displayName,"Label says what will land: '"+hud.target.text+"'");
                var ghost=s.GetComponent<RackPlacementPreview>();
                Check(ghost&&ghost.Visible&&Vector3.Distance(ghost.PreviewPose.position,rack.SlotPoseFor(typeB,rack.NextSlot(typeB)).position)<.001f,"Ghost shows the next "+typeB.displayName+" slot");
                Check(FrameMaterial(rack)==rack.valid,"Rack shows green, not red");
                yield return new WaitForEndOfFrame();ShotGate.Capture(Folder+"/01-smart-place-ready.png");yield return Wait(.15f);

                int before=rack.Count;
                s.BeginHold(true);s.EndHold(true);
                Check(rack.Count==before+2&&b[1].rack==rack&&b[2].rack==rack,"One PLACE tap placed both "+typeB.displayName);
                Check(s.carried.Count==1&&s.Active==a,typeA.displayName+" stays held and selected");
                DOTween.CompleteAll();yield return Wait(1.2f);

                Check(!s.PlaceableType(rack)&&!s.PlaceMatching(rack)&&s.Active==a&&s.carried.Count==1,"With no "+typeB.displayName+" left the rack refuses "+typeA.displayName);
                Check(hud.target.text.StartsWith("This rack only accepts"),"Red refusal label returns: '"+hud.target.text+"'");
                Check(FrameMaterial(rack)==rack.invalid,"Rack shows red again");

                // A PLACE tap during a pickup flight waits, then still switches type.
                var b3=s.items.First(i=>i.definition==typeB&&i.location==ItemLocation.Loose);
                Check(s.TryPick(b3),"Carry another "+typeB.displayName);DOTween.CompleteAll();
                var a2=s.items.First(i=>i.definition==typeA&&i.location==ItemLocation.Loose);
                Check(s.TryPick(a2)&&s.Active==a2&&a2.Moving,typeA.displayName+" pickup still flying");
                s.RefreshTarget();s.BeginHold(true);s.EndHold(true);
                Check(s.BufferedTap,"PLACE tap during the pickup is buffered");
                yield return Wait(1f);
                Check(b3.rack==rack&&s.BufferedTapState==SortingSession.TapBufferState.Completed,"Buffered tap placed "+typeB.displayName+" ("+s.BufferedTapState+")");
            }
            finally
            {
                DOTween.CompleteAll();s.Restore(saved);s.persistence=persistence;
                for(int i=0;i<gates.Length;i++)if(gates[i])gates[i].enabled=gateStates[i];
            }
        }
    }
}
#endif
