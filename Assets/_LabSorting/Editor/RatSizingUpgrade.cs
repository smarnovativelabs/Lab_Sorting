using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;

namespace LabSorting.Editor
{
    public static class RatSizingUpgrade
    {
        [MenuItem("Lab Sorting/Use Full Size Rats")]
        public static void Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop play mode before resizing rats");
            var colony=UnityEngine.Object.FindFirstObjectByType<RatColony>();
            if(!colony||colony.rats.Length!=10)throw new InvalidOperationException("Open the configured Lab_Sorting scene first");
            var rack=colony.session.racks.Single(r=>r.ratsOnly);
            var glass=rack.GetComponent<Renderer>();
            Undo.RecordObject(rack.transform,"Move rat container to upper landing");
            rack.transform.localScale=Vector3.one;
            var unit=glass.bounds.size;
            rack.transform.localScale=new Vector3(1.30f/unit.z,.65f/unit.y,1.15f/unit.x);
            var expanded=glass.bounds;
            rack.transform.position+=new Vector3(6.5f-expanded.center.x,3.31f-expanded.min.y,2.25f-expanded.center.z);
            var enclosure=rack.GetComponent<BoxCollider>();Undo.RecordObject(enclosure,"Resize container collider");
            enclosure.center=glass.localBounds.center;enclosure.size=glass.localBounds.size;
            foreach(var rat in colony.rats)
            {
                var item=rat.GetComponent<SortItem>();var model=rat.animator.transform;
                Undo.RecordObject(model,"Restore full rat size");Undo.RecordObject(item,"Adjust rat carry size");Undo.RecordObject(item.hitbox,"Resize rat collider");
                var rotation=item.transform.rotation;item.transform.rotation=Quaternion.identity;
                item.transform.localScale=Vector3.one;model.localScale=Vector3.one;model.localPosition=Vector3.zero;
                model.position+=Vector3.up*(item.transform.position.y-item.visual.bounds.min.y+.004f);
                var b=item.visual.bounds;item.definition.center=b.center-item.transform.position;item.definition.size=b.size;
                var box=(BoxCollider)item.hitbox;box.center=item.definition.center;box.size=item.definition.size;
                item.originalScale=Vector3.one;item.heldScale=.65f;item.transform.rotation=rotation;
                EditorUtility.SetDirty(item.definition);
            }
            var bounds=glass.bounds;var definition=colony.rats[0].GetComponent<SortItem>().definition;
            for(int i=0;i<10;i++)
            {
                var slot=rack.slots[i];Undo.RecordObject(slot,"Resize rat placement grid");
                var center=new Vector3(Mathf.Lerp(bounds.min.x+.10f+definition.size.x*.5f,bounds.max.x-.10f-definition.size.x*.5f,(i%5)/4f),
                    bounds.min.y+.055f,Mathf.Lerp(bounds.min.z+.10f+definition.size.z*.5f,bounds.max.z-.10f-definition.size.z*.5f,(i/5)));
                slot.SetPositionAndRotation(center-new Vector3(definition.center.x,0,definition.center.z),Quaternion.identity);
            }
            if(rack.label)
            {
                Undo.RecordObject(rack.label.transform,"Adjust container label");
                rack.label.transform.position=new Vector3(bounds.min.x-.01f,bounds.max.y+.12f,bounds.center.z);
                rack.label.transform.rotation=Quaternion.Euler(0,90,0);
                var scale=rack.transform.lossyScale;rack.label.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                rack.label.characterSize=.014f;EditorUtility.SetDirty(rack.label);
            }
            Physics.SyncTransforms();
            var occupied=colony.session.items.Where(i=>i.location==ItemLocation.Loose&&i.visual).ToList();
            int cleared=0;
            foreach(var item in occupied.Where(i=>!i.definition.isRat&&i.visual.bounds.Intersects(bounds)).ToArray())
            {
                var original=item.transform.position;var ib=item.visual.bounds;bool found=false;
                for(float z=-7.3f;z<7.3f&&!found;z+=.3f)
                for(float x=-10.6f;x<10.6f&&!found;x+=.3f)
                {
                    var delta=new Vector3(x-ib.center.x,.015f-ib.min.y,z-ib.center.z);
                    var candidate=new Bounds(ib.center+delta,ib.size+Vector3.one*.025f);
                    if(occupied.Any(other=>other!=item&&other.visual.bounds.Intersects(candidate)))continue;
                    if(Physics.OverlapBox(candidate.center,candidate.extents,Quaternion.identity,~(1<<LayerMask.NameToLayer("SortingItem")),QueryTriggerInteraction.Ignore).Length>0)continue;
                    Undo.RecordObject(item.transform,"Clear rat container footprint");item.transform.position=original+delta;Physics.SyncTransforms();found=true;cleared++;
                }
                if(!found)throw new InvalidOperationException("No clear floor position for "+item.id);
            }

            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(colony.gameObject.scene);EditorSceneManager.SaveScene(colony.gameObject.scene);
            File.WriteAllText("Logs/RatSizing.txt","Rat model scale: 1\nRat bounds: "+definition.size+"\nContainer bounds: "+bounds+"\nCleared loose items: "+cleared+"\nSlots: 10; IDs and save layout revision retained.");
        }
    }
}
