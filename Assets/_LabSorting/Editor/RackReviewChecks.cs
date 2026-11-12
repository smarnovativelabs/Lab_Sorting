using System;
using LabSorting.Sorting;
using UnityEngine;
using Object=UnityEngine.Object;
namespace LabSorting.Editor
{
    public static class RackReviewChecks
    {
        static void Require(bool ok,string message){if(!ok)throw new Exception("REVIEW CHECK FAILED: "+message);Debug.Log("REVIEW PASS: "+message);}
        public static void Run()
        {
            var root=new GameObject("Disposable rack review");root.transform.position=new Vector3(1000,1000,1000);
            var type=ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                var rack=root.AddComponent<SortRack>();rack.shelves=new[]{new RackShelf{height=.2f,clearance=.1f,x=new Vector2(-.75f,.75f),z=new Vector2(-.25f,.25f)}};
                type.size=new Vector3(.4f,.1f,.12f);type.center=new Vector3(0,.05f,0);rack.Initialize();
                Require(rack.CapacityFor(type)==0,"Surface offset cannot push a tightly fitting item into the upper shelf");
                type.size=new Vector3(.4f,.08f,.12f);type.center=new Vector3(0,.04f,0);rack.Initialize();
                Require(rack.CapacityFor(type)>0,"A shorter item fits the same shelf");
                bool rejected=false;try{rack.SlotPoseFor(type,-1);}catch(ArgumentOutOfRangeException){rejected=true;}
                Require(rejected,"Negative shelf point is rejected");
                var session=root.AddComponent<SortingSession>();
                var itemObject=new GameObject("Review item");itemObject.transform.SetParent(root.transform,false);itemObject.transform.localPosition=new Vector3(0,2,-4);
                var item=itemObject.AddComponent<SortItem>();item.definition=type;item.originalScale=Vector3.one;
                Require(session.TossPlan(item,rack,out _,out var pose,out var entry),"Clear arc and shelf slide accepted");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform,true);
                wall.transform.position=Vector3.Lerp(entry,SortItem.TossSettlePoint(entry,pose.position),.7f)+pose.rotation*type.center;wall.transform.localScale=new Vector3(.03f,.03f,.03f);Physics.SyncTransforms();
                Require(!session.TossPlan(item,rack,out _,out _,out _),"Blocker on the final shelf slide rejects the toss");
                wall.transform.position=SortItem.ArcPoint(item.transform.position,entry,SortItem.TossApex(item.transform.position,entry),.5f)+Vector3.right*.18f;Physics.SyncTransforms();
                Require(!session.TossPlan(item,rack,out _,out _,out _),"Wide tumbling item cannot clip a side obstacle");
                Require(!session.TossPlan(null,rack,out _,out _,out _),"Missing toss item safely rejected");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(type);Physics.SyncTransforms();}
        }
    }
}
