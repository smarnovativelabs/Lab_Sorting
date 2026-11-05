using System;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class IntegrationValidationChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("Integration validator check failed: "+text);Debug.Log("VALIDATION PASS: "+text);}
        public static void Run()
        {
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();Physics.SyncTransforms();var report=IntegrationValidator.Validate(s);IntegrationValidator.Export(report);Check(report.errors==0,"Authored prototype has no integration errors: "+string.Join(",",report.issues.Select(i=>i.code)));
            var item=s.items[0];var other=s.items[1];var rack=s.racks[0];var recovery=s.GetComponent<ItemRecovery>();string id=item.id;var collider=item.hitbox;var slot=rack.slots[0];var scale=s.player.transform.localScale;var points=recovery.safePoints;var parent=item.transform.parent;GameObject extra=null;
            try
            {
                item.id=other.id;Check(IntegrationValidator.Validate(s).Has("ITEM_ID"),"Duplicate item IDs found");item.id=id;
                item.hitbox=null;Check(IntegrationValidator.Validate(s).Has("ITEM_COLLIDER"),"Missing collider found");item.hitbox=collider;
                collider.isTrigger=true;Check(IntegrationValidator.Validate(s).Has("ITEM_UNPICKABLE"),"Trigger-only pickup collider found");collider.isTrigger=false;
                collider.enabled=false;Check(IntegrationValidator.Validate(s).Has("ITEM_DISABLED_COLLIDER"),"Disabled pickup collider found");collider.enabled=true;
                rack.slots[0]=null;Check(IntegrationValidator.Validate(s).Has("SLOT_NULL"),"Missing rack slot found");rack.slots[0]=rack.slots[1];Check(IntegrationValidator.Validate(s).Has("SLOT_DUPLICATE"),"Duplicate rack slot found");rack.slots[0]=slot;
                s.player.transform.localScale=Vector3.one*2;Check(IntegrationValidator.Validate(s).Has("PLAYER_SCALE"),"Player scale mismatch found");s.player.transform.localScale=scale;
                recovery.safePoints=null;Check(IntegrationValidator.Validate(s).Has("RECOVERY_CONFIG"),"Missing recovery configuration found");recovery.safePoints=points;
                extra=new GameObject("Unregistered item fixture");extra.AddComponent<SortItem>();Check(IntegrationValidator.Validate(s).Has("ITEM_UNREGISTERED"),"Unregistered scene item found");
                var before=item.id;IntegrationValidator.Validate(s);Check(item.id==before&&s.player.transform.localScale==scale,"Validation does not mutate authored IDs or transforms");
            }
            finally{item.id=id;item.hitbox=collider;collider.enabled=true;collider.isTrigger=false;rack.slots[0]=slot;s.player.transform.localScale=scale;recovery.safePoints=points;if(extra)UnityEngine.Object.DestroyImmediate(extra);}
            report=IntegrationValidator.Validate(s);Check(report.errors==0,"Fixture cleanup restores valid content");IntegrationValidator.Export(report);
        }
    }
}
