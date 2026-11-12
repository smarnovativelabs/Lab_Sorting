using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class LandingFeelChecks
    {
        static void Check(bool value,string message){if(!value)throw new Exception("LANDING FEEL: "+message);Debug.Log("LANDING FEEL PASS: "+message);}
        public static IEnumerator Run()
        {
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var f=s.GetComponent<SortingFeedback>();var initial=s.Capture();bool sound=f.SoundEnabled;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1500,-.1f,1500);floor.transform.localScale=new Vector3(20,.2f,20);
            var notify=typeof(SortingSession).GetMethod("NotifyLanded",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var definition=ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                definition.size=Vector3.one*.1f;definition.placementMaterial=PlacementMaterial.Plastic;float light=ThrowFeel.Mass(definition),pitch=ThrowFeel.Pitch(definition);definition.size=Vector3.one*.3f;definition.placementMaterial=PlacementMaterial.Metal;
                Check(ThrowFeel.Mass(definition)>light&&ThrowFeel.Pitch(definition)<pitch,"Heavy metal has greater mass and deeper pitch than small plastic");
                Check(ThrowFeel.Gain(definition,4)>ThrowFeel.Gain(definition,.2f),"Impact speed controls bounded loudness");
                int n=0;f.SetSound(true,false);s.player.SendMessage("OnApplicationFocus",true);
                foreach(PlacementMaterial material in Enum.GetValues(typeof(PlacementMaterial)))
                {
                    var item=s.items.First(i=>i.definition.placementMaterial==material);var bounds=s.ReleaseBounds(item);float x=1496+n++*2;
                    item.Snap(new Vector3(x,1.2f,1498),Quaternion.identity);item.location=ItemLocation.Loose;
                    var target=new Vector3(x,bounds.extents.y+.035f-(bounds.center.y-initial.items.First(e=>e.id==item.id).position.y),1500);
                    int cues=f.ImpactCues,sounds=f.PlayedSounds;item.ThrowWithPhysics(target,()=>notify.Invoke(s,new object[]{item}));
                    bool early=false;float deadline=Time.time+2.1f,realDeadline=Time.realtimeSinceStartup+30;
                    while(Time.time<deadline){if(Time.realtimeSinceStartup>realDeadline)throw new Exception("Clock stalled");if(f.ImpactCues>cues&&item.Moving)early=true;yield return null;}
                    Check(early,"Material "+material+" sounds at collision before settling");
                    Check(f.ImpactCues==cues+1&&f.PlayedSounds==sounds+1,"Material "+material+" plays once without settle duplicate");
                    Check(!item.Moving&&f.LastLandingClip==f.LandingClip(item.definition)&&Mathf.Approximately(f.LastLandingPitch,ThrowFeel.Pitch(item.definition)),"Material "+material+" uses correct clip/pitch and settles");
                }
                f.SetSound(false,false);Check(f.GetComponents<AudioSource>().All(v=>!v.isPlaying),"Mute immediately stops every landing voice");
                var muted=s.items[0];muted.Snap(new Vector3(1500,1.2f,1497),Quaternion.identity);muted.location=ItemLocation.Loose;int before=f.PlayedSounds;
                muted.ThrowWithPhysics(new Vector3(1500,.4f,1499),()=>notify.Invoke(s,new object[]{muted}));
                float until=Time.realtimeSinceStartup+10;while(muted.Moving&&Time.realtimeSinceStartup<until)yield return null;
                Check(f.PlayedSounds==before,"Muted collision and fallback stay silent");
                var saved=f.metalPlace;f.metalPlace=null;Check(f.LandingClip(definition)==f.drop,"Missing material clip falls back to default drop");f.metalPlace=saved;
                Check(DropLandingPool.ActiveCount==0,"All bodies released after material tests");
            }
            finally{s.Restore(initial);f.SetSound(sound,false);floor.SetActive(false);UnityEngine.Object.Destroy(floor);UnityEngine.Object.Destroy(definition);}
        }
    }
}
