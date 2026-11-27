using UnityEngine;
namespace LabSorting.Sorting
{
    // Scene items are static colliders: nothing makes a loose item fall. These helpers find a loose item left
    // hanging in the air (the item under it was picked, an old save, a collider fitted after authoring) and work
    // out where it comes to rest when lowered straight down. Floors, rack shelves and other items all count as support.
    public static class ItemSupport
    {
        // Landing targets sit up to ~4.5 cm above their surface (SafeThrow clearances); anything higher reads as floating.
        public const float Gap=.08f;
        static readonly RaycastHit[] probes=new RaycastHit[16];
        static readonly RaycastHit[] casts=new RaycastHit[64];
        public static bool Eligible(SortItem item)
        {
            if(!item||item.location!=ItemLocation.Loose||item.Moving||item.Searched||!item.gameObject.activeInHierarchy||!item.hitbox||!item.hitbox.enabled)return false;
            // Rats walk on their own; physics-driven items fall on their own.
            if(item.definition&&item.definition.isRat)return false;
            var body=item.hitbox.attachedRigidbody;return !body||body.isKinematic;
        }
        // Fast probes cover ordinary floors; an oriented collider sweep also catches narrow edge contacts
        // between the probes. Neither side walls nor empty corners of a rotated AABB count as support.
        public static bool Floating(SortItem item)
        {
            if(!Eligible(item))return false;
            var b=item.naturalSettling?item.naturalSettling.WorldBounds:item.hitbox.bounds;float reach=b.extents.y+Gap;
            for(int i=0;i<5;i++)
            {
                var origin=b.center;
                if(i>0){origin.x+=((i&1)==0?-.6f:.6f)*b.extents.x;origin.z+=(i<3?-.6f:.6f)*b.extents.z;}
                int count=Physics.RaycastNonAlloc(origin,Vector3.down,probes,reach,~0,QueryTriggerInteraction.Ignore);
                if(count>=probes.Length)return false;
                for(int h=0;h<count;h++)if(!probes[h].collider.transform.IsChildOf(item.transform))return false;
            }
            if(item.hitbox is BoxCollider box)
            {
                var scale=box.transform.lossyScale;
                var half=Vector3.Scale(box.size*.5f,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
                // Start above the current contact so a resting box produces a real hit normal.
                // Zero-distance overlaps can be side walls; they are not downward support.
                int count=Physics.BoxCastNonAlloc(box.transform.TransformPoint(box.center)+Vector3.up*Gap,half,Vector3.down,casts,box.transform.rotation,Gap*2,~0,QueryTriggerInteraction.Ignore);
                if(count>=casts.Length)return false;
                for(int h=0;h<count;h++)
                    if(casts[h].distance>0&&casts[h].normal.y>.1f&&!casts[h].collider.transform.IsChildOf(item.transform))return false;
            }
            return true;
        }
        // Pivot position after lowering the item straight down onto the first thing under its footprint.
        // False when the item is embedded in level geometry (ItemRecovery's safe points handle that), when nothing
        // is below, or when the first thing below is the player or a moving body.
        public static bool TrySettlePoint(SortItem item,Transform player,out Vector3 position)
        {
            position=default;if(!Eligible(item))return false;
            var b=item.naturalSettling?item.naturalSettling.WorldBounds:item.hitbox.bounds;var half=new Vector3(b.extents.x*.9f,Mathf.Max(.005f,b.extents.y-.002f),b.extents.z*.9f);
            int count=Physics.BoxCastNonAlloc(b.center,half,Vector3.down,casts,Quaternion.identity,8,~0,QueryTriggerInteraction.Ignore);
            if(count>=casts.Length)return false;
            float nearest=float.PositiveInfinity;Collider surface=null;
            for(int i=0;i<count;i++)
            {
                var hit=casts[i];var other=hit.collider;if(other.transform.IsChildOf(item.transform))continue;
                if(hit.distance<=0){if(other.GetComponentInParent<SortItem>())continue;return false;}
                if(hit.distance<nearest){nearest=hit.distance;surface=other;}
            }
            if(!surface||nearest<.02f)return false;
            if(player&&surface.transform.IsChildOf(player))return false;
            if(surface.attachedRigidbody&&!surface.attachedRigidbody.isKinematic)return false;
            position=item.transform.position+Vector3.down*(nearest-.004f);return true;
        }
    }
}
