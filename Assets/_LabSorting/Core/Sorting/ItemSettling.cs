using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        readonly List<Bounds> supportChecks=new List<Bounds>();
        readonly Collider[] supportOverlaps=new Collider[64];
        float supportCheckAt;
        public int SettledItems {get;private set;}
        // A drop can land on top of another loose item. When the one underneath leaves (picked, or set down
        // itself), check the space it left: anything now hanging there is set down onto what is below.
        void QueueSupportCheck(Bounds vacated){supportChecks.Add(vacated);supportCheckAt=Time.unscaledTime+.05f;}
        void TickSupportChecks()
        {
            if(supportChecks.Count==0||Time.unscaledTime<supportCheckAt)return;
            var pending=supportChecks.ToArray();supportChecks.Clear();Physics.SyncTransforms();
            int layer=LayerMask.NameToLayer("SortingItem");int mask=layer<0?~(1<<2):1<<layer;
            foreach(var vacated in pending)
            {
                var top=new Vector3(vacated.center.x,vacated.max.y+.02f,vacated.center.z);
                int count=Physics.OverlapBoxNonAlloc(top,new Vector3(vacated.extents.x+.03f,.06f,vacated.extents.z+.03f),supportOverlaps,Quaternion.identity,mask,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++){var item=supportOverlaps[i].GetComponentInParent<SortItem>();if(ItemSupport.Floating(item))SettleDown(item,true);}
            }
        }
        // Lowers a floating loose item straight down onto whatever is below it. Animated during play;
        // instant while loading a save. Returns false (item untouched) when there is no safe resting point.
        public bool SettleDown(SortItem item,bool animate)
        {
            if(!ItemSupport.TrySettlePoint(item,player?player.transform:null,out var position))return false;
            var vacated=item.hitbox.bounds;SettledItems++;
            if(animate)item.Travel(position,item.transform.rotation,()=>QueueSupportCheck(vacated));
            else{item.Snap(position,item.transform.rotation);Physics.SyncTransforms();QueueSupportCheck(vacated);}
            if(saveAt<0)saveAt=Time.unscaledTime+.6f;
            return true;
        }
        int SettleFloatingLooseItems()
        {
            Physics.SyncTransforms();int settled=0;
            foreach(var item in items)if(ItemSupport.Floating(item)&&SettleDown(item,false))settled++;
            return settled;
        }
    }
}
