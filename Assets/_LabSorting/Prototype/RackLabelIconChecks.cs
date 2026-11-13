#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class RackLabelIconChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("RACK ICON: "+text);Debug.Log("RACK ICON PASS: "+text);}
        public static IEnumerator Run(SortingSession s)
        {
            var saved=s.Capture();var definitions=s.items.Select(i=>i.definition).Distinct().ToArray();
            try
            {
                Check(s.racks.Length==46&&s.racks.All(r=>r.labelIcon),"All 46 racks have an icon renderer");
                Check(s.racks.All(r=>!r.labelIcon.enabled&&!r.labelIcon.sprite),"Empty racks hide icon and clear old sprite");
                for(int i=0;i<s.racks.Length;i++)
                {
                    var rack=s.racks[i];var item=s.items.First(x=>x.definition==definitions[i]);Check(s.TryPick(item),"Pick icon fixture");DG.Tweening.DOTween.CompleteAll();Check(s.TryPlace(rack),"Place icon fixture");DG.Tweening.DOTween.CompleteAll();yield return null;
                    Check(rack.labelIcon.enabled&&rack.labelIcon.sprite==item.definition.icon,"Matching icon: "+item.definition.displayName);
                    var size=rack.labelIcon.sprite.bounds.size*rack.labelIcon.transform.localScale.x;Check(Mathf.Abs(Mathf.Max(size.x,size.y)-.28f)<.001f,"Icon fits without stretching");
                    Check(!rack.labelIcon.GetComponent<Collider>()&&rack.labelIcon.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off,"Icon adds no physics or shadow work");
                    float width=rack.label.GetComponent<MeshRenderer>().localBounds.size.x*rack.label.transform.localScale.x;
                    Check(rack.label.transform.localPosition.x-width*.5f>rack.labelIcon.transform.localPosition.x+.14f,"Name does not overlap icon: "+item.definition.displayName);
                    var material=rack.labelIcon.sharedMaterial;rack.Preview(item.definition);Check(rack.labelIcon.sharedMaterial==material,"Green preview preserves icon material");rack.Preview(definitions[(i+1)%definitions.Length]);Check(rack.labelIcon.sharedMaterial==material,"Red preview preserves icon material");rack.Preview(null);
                }
                Capture(s,s.racks[0],"Logs/RackLabelIcon-Assigned.png");
                var snapshot=s.Capture();Check(s.Restore(snapshot),"Snapshot restores assigned rack icons");Check(s.racks.All(r=>r.Type&&r.labelIcon.sprite==r.Type.icon),"Restored labels match saved types");
                var first=s.racks[0];var held=first.contents.First(i=>i);Check(s.TryPick(held),"Last rack item can be retrieved");DG.Tweening.DOTween.CompleteAll();Check(!first.Type&&!first.labelIcon.enabled&&!first.labelIcon.sprite,"Last retrieval clears icon");
                s.ThrowActive();DG.Tweening.DOTween.CompleteAll();
                var different=s.items.First(i=>i.definition==definitions[1]&&i.location==ItemLocation.Loose);Check(s.TryPick(different),"Different type picked");DG.Tweening.DOTween.CompleteAll();Check(s.TryPlace(first),"Empty rack accepts different type");DG.Tweening.DOTween.CompleteAll();Check(first.labelIcon.sprite==different.definition.icon,"Reassignment replaces old icon");
            }
            finally{DG.Tweening.DOTween.CompleteAll();s.Restore(saved);}
        }
        static void Capture(SortingSession s,SortRack rack,string path)
        {
            var camera=s.player.view;var position=camera.transform.position;var rotation=camera.transform.rotation;
            try{camera.transform.position=rack.transform.TransformPoint(new Vector3(0,1.8f,-2.5f));camera.transform.LookAt(rack.label.transform.position);SortingSmokeChecks.Capture(s,path);}
            finally{camera.transform.SetPositionAndRotation(position,rotation);}
        }
    }
}
#endif
