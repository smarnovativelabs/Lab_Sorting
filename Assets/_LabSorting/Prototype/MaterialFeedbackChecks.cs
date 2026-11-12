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
                Check(f.Celebrations==celebrations+1&&f.PlayedSounds>=sounds+1,"Completion event plays feedback");
                yield return new WaitForSeconds(.3f);Check(label.color!=color,"Label glow animates");
                f.SetSound(false,false);foreach(var audio in f.GetComponents<AudioSource>())Check(!audio.isPlaying,"Mute stops all feedback sources");
                float deadline=Time.realtimeSinceStartup+5;while(f.IsCelebrating&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(!f.IsCelebrating&&label.color==color&&label.transform.localScale==scale,"Glow restores label exactly (color="+label.color+", scale="+label.transform.localScale+")");
                Check(s.TryPick(item),"Retrieve completed rack item");DOTween.CompleteAll();yield return new WaitForSeconds(.1f);sounds=f.PlayedSounds;
                Check(s.TryPlace(rack),"Refill completed rack");Check(f.Celebrations==celebrations+1&&f.PlayedSounds==sounds,"No repeated celebration and muted placement stays silent");
                var saved=s.Capture();Check(saved.completedRacks.Contains(rack.id),"Completion suppression persists in save");
                f.enabled=false;Check(label.color==color&&label.transform.localScale==scale,"Disabling feedback leaves label unchanged");f.enabled=true;
                Debug.Log("MATERIAL_FEEDBACK_OK");
            }
            finally{DOTween.CompleteAll();s.Restore(initial);s.InitializeSaveCache();f.SetSound(sound,false);UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(definition);}
        }
    }
}
