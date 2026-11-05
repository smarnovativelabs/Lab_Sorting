using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    [Serializable] public class IntegrationIssue
    {
        public string severity,code,message,objectPath;
        [NonSerialized] public UnityEngine.Object context;
    }
    [Serializable] public class IntegrationReport
    {
        public string scene;public int errors,warnings;public List<IntegrationIssue> issues=new List<IntegrationIssue>();
        public bool Has(string code)=>issues.Exists(i=>i.code==code);
    }
    public static class IntegrationValidator
    {
        static void Add(IntegrationReport r,string code,string message,UnityEngine.Object context,bool warning=false)
        {
            r.issues.Add(new IntegrationIssue{severity=warning?"Warning":"Error",code=code,message=message,context=context,objectPath=ObjectPath(context)});if(warning)r.warnings++;else r.errors++;
        }
        static string ObjectPath(UnityEngine.Object context)
        {
            var transform=context is Component component?component.transform:context is GameObject go?go.transform:null;
            if(!transform)return context?(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(context))?context.name:AssetDatabase.GetAssetPath(context)):"Scene";
            string path=transform.name;while(transform.parent){transform=transform.parent;path=transform.name+"/"+path;}return path;
        }
        static bool Unit(Transform t)=>(t.lossyScale-Vector3.one).sqrMagnitude<.0001f;
        public static IntegrationReport Validate(SortingSession s)
        {
            var r=new IntegrationReport{scene=s?s.gameObject.scene.path:"No session"};if(!s){Add(r,"SESSION_MISSING","Add and wire a SortingSession.",null);return r;}
            if(!s.player||!s.hand||!s.player.view||!s.player.character){Add(r,"PLAYER_WIRING","Player, camera, motor and hand socket are required.",s);}
            else {if(!Unit(s.player.transform))Add(r,"PLAYER_SCALE","Player hierarchy must use unit scale.",s.player);var capsule=s.player.GetComponent<CapsuleCollider>();if(!capsule||Mathf.Abs(capsule.height-1.7f)>.01f||Mathf.Abs(capsule.center.y-.85f)>.01f)Add(r,"PLAYER_HEIGHT","Expected 1.70m capsule / 0.85m center; recheck door clearance if changed.",s.player,true);if(Mathf.Abs(s.player.transform.InverseTransformPoint(s.player.view.transform.position).y-1.6f)>.01f)Add(r,"EYE_HEIGHT","Expected 1.60m eye level.",s.player,true);}
            var ids=new HashSet<string>();var registered=new HashSet<SortItem>();var definitions=new Dictionary<string,ItemDefinition>();int total=0;
            if(s.items==null||s.items.Length==0)Add(r,"ITEMS_EMPTY","No items registered.",s);
            else foreach(var item in s.items)
            {
                if(!item){Add(r,"ITEM_NULL","Item array contains a missing reference.",s);continue;}total++;if(!registered.Add(item))Add(r,"ITEM_DUPLICATE_REFERENCE","Item registered more than once.",item);
                if(string.IsNullOrWhiteSpace(item.id)||!ids.Add(item.id))Add(r,"ITEM_ID","Item IDs must be nonempty and unique.",item);
                if(!item.definition)Add(r,"ITEM_DEFINITION","Item definition is missing.",item);
                else {var type=item.definition;if(string.IsNullOrWhiteSpace(type.id))Add(r,"TYPE_ID","Definition needs a stable ID.",type);else if(definitions.TryGetValue(type.id,out var other)&&other!=type)Add(r,"TYPE_ID_DUPLICATE","Different definitions share the same ID.",type);else definitions[type.id]=type;}
                if(!item.hitbox||!item.hitbox.transform.IsChildOf(item.transform))Add(r,"ITEM_COLLIDER","Assign a collider belonging to the item.",item);
                else {if(item.hitbox.isTrigger||item.hitbox.gameObject.layer==2)Add(r,"ITEM_UNPICKABLE","Interaction collider must be solid and not Ignore Raycast layer.",item);if(!item.hitbox.enabled&&item.location==ItemLocation.Loose&&!item.Moving)Add(r,"ITEM_DISABLED_COLLIDER","Loose item collider is disabled.",item);var size=item.hitbox.bounds.size;if(item.location==ItemLocation.Loose&&!item.Moving&&(size.magnitude<.025f||Mathf.Max(size.x,size.y,size.z)>1.5f))Add(r,"ITEM_SIZE","Check model units/collider dimensions; unusual sorting-item size.",item,true);if(item.hitbox is MeshCollider mesh&&!mesh.convex)Add(r,"ITEM_MESH_COLLIDER","Prefer a primitive or convex interaction collider for reliable overlap recovery.",item,true);}
                if(!item.gameObject.activeInHierarchy&&item.location==ItemLocation.Loose)Add(r,"ITEM_INACTIVE","Authored loose item is inactive and cannot be picked.",item);
                if(!item.visual)Add(r,"ITEM_VISUAL","Assign an item renderer.",item);else if(item.visual.sharedMaterials.Any(m=>!m))Add(r,"ITEM_MATERIAL","Renderer contains missing materials.",item,true);
                if(item.originalScale.x<=0||item.originalScale.y<=0||item.originalScale.z<=0||item.transform.lossyScale.x<=0||item.transform.lossyScale.y<=0||item.transform.lossyScale.z<=0)Add(r,"ITEM_SCALE","Item scale must be positive and originalScale configured.",item);
                if(item.transform.parent&&!Unit(item.transform.parent))Add(r,"ITEM_PARENT_SCALE","Scaled item parents can change held/restored dimensions. Use unit-scale content roots.",item,true);
            }
            foreach(var item in UnityEngine.Object.FindObjectsByType<SortItem>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(item.gameObject.scene==s.gameObject.scene&&!registered.Contains(item))Add(r,"ITEM_UNREGISTERED","Scene item is not registered and will be omitted from progress/save.",item);
            ids.Clear();var slots=new HashSet<Transform>();var registeredRacks=new HashSet<SortRack>();int capacity=0;
            if(s.racks==null||s.racks.Length==0)Add(r,"RACKS_EMPTY","No racks registered.",s);
            else foreach(var rack in s.racks)
            {
                if(!rack){Add(r,"RACK_NULL","Rack array contains a missing reference.",s);continue;}if(!registeredRacks.Add(rack))Add(r,"RACK_DUPLICATE_REFERENCE","Rack registered more than once.",rack);
                if(string.IsNullOrWhiteSpace(rack.id)||!ids.Add(rack.id))Add(r,"RACK_ID","Rack IDs must be nonempty and unique.",rack);
                if(!Unit(rack.transform))Add(r,"RACK_SCALE","Check scaled rack hierarchy and slot spacing.",rack,true);
                if(!rack.GetComponentsInChildren<Collider>().Any(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer!=2))Add(r,"RACK_COLLIDER","Rack needs a solid targetable collider.",rack);
                if(!rack.label||!rack.indicator||!rack.neutral||!rack.valid||!rack.invalid)Add(r,"RACK_FEEDBACK","Rack label/indicator/material wiring incomplete.",rack);
                if(rack.slots==null||rack.slots.Length==0)Add(r,"RACK_SLOTS","Rack has no slots.",rack);
                else foreach(var slot in rack.slots){if(!slot){Add(r,"SLOT_NULL","Missing rack slot.",rack);continue;}capacity++;if(!slots.Add(slot))Add(r,"SLOT_DUPLICATE","Slot transform is used more than once.",slot);if(!slot.IsChildOf(rack.transform))Add(r,"SLOT_PARENT","Slot must belong to its rack hierarchy.",slot);}
            }
            foreach(var rack in UnityEngine.Object.FindObjectsByType<SortRack>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(rack.gameObject.scene==s.gameObject.scene&&!registeredRacks.Contains(rack))Add(r,"RACK_UNREGISTERED","Scene rack is not registered for saving.",rack);
            if(capacity<total)Add(r,"TOTAL_CAPACITY","Not enough rack slots for all registered items.",s);
            if(definitions.Count>registeredRacks.Count)Add(r,"CATEGORY_CAPACITY","Need at least one rack per item category.",s);
            var recovery=s.GetComponent<ItemRecovery>();
            if(!recovery||!recovery.Configured)Add(r,"RECOVERY_CONFIG","Configure playable bounds and recovery points.",s);
            else
            {
                if(s.player&&!recovery.Contains(s.player.transform.position))Add(r,"SPAWN_OUTSIDE","Player spawn lies outside playable bounds.",s.player);
                foreach(var point in recovery.safePoints){if(!point){Add(r,"RECOVERY_POINT_NULL","Missing recovery point.",recovery);continue;}if(!recovery.Contains(point.position))Add(r,"RECOVERY_POINT_OUTSIDE","Recovery point lies outside playable bounds.",point);if(!Physics.Raycast(point.position+Vector3.up*2,Vector3.down,4,~(1<<2),QueryTriggerInteraction.Ignore))Add(r,"RECOVERY_NO_FLOOR","Recovery point has no supporting floor within four metres.",point);}
                foreach(var item in registered)if(item.location==ItemLocation.Loose&&(item.transform.position.y<recovery.minimumY||!recovery.Contains(item.transform.position)))Add(r,"ITEM_OUTSIDE","Loose item starts outside the recovery limits.",item);
                if(registered.Count>0&&!recovery.TrySafePosition(registered.First(),out _))Add(r,"RECOVERY_OCCUPIED","No clear recovery point for a sample item; check floor and occupancy.",recovery,true);
            }
            return r;
        }
        public static IntegrationReport ValidateActiveScene()=>Validate(UnityEngine.Object.FindFirstObjectByType<SortingSession>());
        public static void Export(IntegrationReport report){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/IntegrationValidation.json",JsonUtility.ToJson(report,true));}
    }
    public sealed class IntegrationValidatorWindow : EditorWindow
    {
        Label summary;ScrollView results;IntegrationReport report;
        [MenuItem("Tools/Lab Sorting/Integration Validator")]
        public static void Open(){GetWindow<IntegrationValidatorWindow>("Lab Integration");}
        public void CreateGUI()
        {
            rootVisualElement.Add(new Label("Read-only scene validation. Select an issue to locate its object."));
            rootVisualElement.Add(new Button(Run){text="Validate active scene"});rootVisualElement.Add(new Button(()=>{if(report==null)Run();IntegrationValidator.Export(report);Debug.Log("Report saved: Logs/IntegrationValidation.json");}){text="Export JSON report"});
            summary=new Label();rootVisualElement.Add(summary);results=new ScrollView();results.style.flexGrow=1;rootVisualElement.Add(results);
        }
        void Run(){Physics.SyncTransforms();report=IntegrationValidator.ValidateActiveScene();summary.text=report.errors+" errors / "+report.warnings+" warnings";results.Clear();foreach(var issue in report.issues){var row=new Button(()=>{if(issue.context){Selection.activeObject=issue.context;EditorGUIUtility.PingObject(issue.context);}}){text=issue.severity+" ["+issue.code+"] "+issue.objectPath+": "+issue.message};row.style.whiteSpace=WhiteSpace.Normal;results.Add(row);}if(report.issues.Count==0)results.Add(new Label("No integration issues found. Verify final geometry and reach in Play mode."));}
    }
}


