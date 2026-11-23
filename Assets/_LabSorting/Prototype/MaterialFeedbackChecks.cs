using System;
using System.Collections;
using DG.Tweening;
using LabSorting.Sorting;
using UnityEngine;
namespace LabSorting.Prototype
{
    public static class MaterialFeedbackChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("MATERIAL FEEDBACK: "+message);Debug.Log("MATERIAL PASS: "+message);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var f=s.GetComponent<SortingFeedback>();
            var initial=s.Capture();bool sound=f.SoundEnabled;var go=new GameObject("Disposable completion rack");var rack=go.AddComponent<SortRack>();rack.id="feedback-test-rack";
            var label=new GameObject("Label").AddComponent<TextMesh>();label.transform.SetParent(go.transform);rack.label=label;rack.slots=new[]{go.transform};rack.Initialize();
            var definition=ScriptableObject.CreateInstance<ItemDefinition>();
            var originalRacks=s.racks;var testRacks=new SortRack[originalRacks.Length+1];Array.Copy(originalRacks,testRacks,originalRacks.Length);testRacks[testRacks.Length-1]=rack;s.racks=testRacks;
            try
            {
                Check(f.glassPlace&&f.plasticPlace&&f.metalPlace&&f.rackChime,"All four clips wired");
                definition.placementMaterial=PlacementMaterial.Glass;Check(f.PlacementClip(definition)==f.glassPlace,"Glass routing");
                definition.placementMaterial=PlacementMaterial.Plastic;Check(f.PlacementClip(definition)==f.plasticPlace,"Plastic routing");
                definition.placementMaterial=PlacementMaterial.Metal;Check(f.PlacementClip(definition)==f.metalPlace,"Metal routing");
                var metal=f.metalPlace;f.metalPlace=null;Check(f.PlacementClip(definition)==f.place,"Missing clip uses default");f.metalPlace=metal;
                f.SetSound(true,false);yield return new WaitForSeconds(.2f);
                var item=s.items[0];Check(s.TryPick(item),"Pick actual item");DOTween.CompleteAll();yield return new WaitForSeconds(.1f);
                int celebrations=f.Celebrations,sounds=f.PlayedSounds;var color=label.color;var scale=label.transform.localScale;
                Check(s.TryPlace(rack),"Place final rack item");Check(f.LastPlacedDefinition==item.definition,"Sound uses placed item, not next inventory selection");
                Check(f.Celebrations==celebrations&&f.PlayedSounds==sounds,"Placement and completion feedback wait for arrival");
                float arrivalDeadline=Time.realtimeSinceStartup+8;while(f.Celebrations==celebrations&&Time.realtimeSinceStartup<arrivalDeadline)yield return null;yield return null;
                Check(f.Celebrations==celebrations+1&&f.PlayedSounds>=sounds+1,"Arrival plays placement and completion feedback; moving="+item.Moving+", blocked="+s.player.blocked+", pending="+f.CompletionPending);
                Check(label.color!=color,"Label glow animates after arrival");
                var pose=rack.SlotPose(item.slot);Check(item.CompletionFloating,"Completed rack starts float wave");Check(Vector3.Distance(item.transform.position,pose.position)<.0001f&&item.hitbox.enabled,"Float preserves authoritative pose and collider");Check(!s.TryPick(item)&&item.PickLocked&&rack.Count==1,"Full rack rejects direct pickup without changing contents");
                f.SetSound(false,false);foreach(var audio in f.GetComponents<AudioSource>())Check(!audio.isPlaying,"Mute stops all feedback sources");
                float deadline=Time.realtimeSinceStartup+5;while((f.IsCelebrating||f.CompletionPending)&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(!f.IsCelebrating&&label.color==color&&label.transform.localScale==scale,"Glow restores label exactly (color="+label.color+", scale="+label.transform.localScale+")");
                Check(!item.CompletionFloating&&item.visual.enabled,"Wave restores original renderer");
                Check(!s.TryPick(item)&&f.Celebrations==celebrations+1,"Locked rack cannot be emptied or celebrated twice");
                var saved=s.Capture();Check(saved.completedRacks.Contains(rack.id),"Completion suppression persists in save");
                f.enabled=false;Check(label.color==color&&label.transform.localScale==scale,"Disabling feedback leaves label unchanged");f.enabled=true;
                Check(s.Restore(saved)&&item.PickLocked&&!s.TryPick(item),"Saved full rack restores pickup lock");
                rack.slots=new[]{go.transform,go.transform};Check(!item.PickLocked&&s.TryPick(item),"Incomplete rack still allows pickup");item.CompleteMotion();rack.slots=new[]{go.transform};
                Check(s.Restore(saved),"Restore completed rack for interruption check");item.PlayCompletionFloat(.1f,.03f);Check(item.CompletionFloating,"Restart visual for reset check");
                Check(s.Restore(initial)&&!item.CompletionFloating&&item.visual.enabled&&!item.PickLocked,"Reset clears float visual and pickup lock");
                definition.size=new Vector3(.2f,.2f,.2f);rack.shelves=new[]{new RackShelf{height=0,clearance=.4f,x=new Vector2(0,.5f),z=new Vector2(0,.5f)},new RackShelf{height=1,clearance=.4f,x=new Vector2(0,.5f),z=new Vector2(0,.5f)}};rack.Initialize();var oldDefinition=item.definition;item.definition=definition;rack.Add(item);rack.CompletionWave(0,out float first,out float lift);rack.CompletionWave(2,out float next,out _);rack.CompletionWave(4,out float shelf,out _);Check(next>first&&shelf>next&&lift>0&&lift<.2f,"Wave advances by row then shelf with safe lift");rack.Remove(item);item.definition=oldDefinition;
                Debug.Log("MATERIAL_FEEDBACK_OK");
            }
            finally{DOTween.CompleteAll();s.racks=originalRacks;s.Restore(initial);s.InitializeSaveCache();f.SetSound(sound,false);UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(definition);}
        }
    }
}
