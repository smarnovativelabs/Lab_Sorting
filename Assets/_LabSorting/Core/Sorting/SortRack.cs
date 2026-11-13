using System;
using UnityEngine;
namespace LabSorting.Sorting
{
    // One usable shelf surface in rack-local space. Measured from the rack mesh by the designer tools.
    [Serializable] public struct RackShelf
    {
        public float height;
        [Tooltip("Free height above the surface.")] public float clearance;
        [Tooltip("Usable min/max along the rack width (local X).")] public Vector2 x;
        [Tooltip("Usable min/max along the rack depth (local Z). Front is -Z.")] public Vector2 z;
    }
    public sealed class SortRack : MonoBehaviour
    {
        public string id;
        [Tooltip("Fixed points (prototype racks). Ignored when shelves are set.")]
        public Transform[] slots;
        [Tooltip("Size-based points: the first item type placed fills every shelf with as many points as fit.")]
        public RackShelf[] shelves;
        [Min(0)] public float itemGap=.01f;
        [NonSerialized] public SortItem[] contents=Array.Empty<SortItem>();
        public TextMesh label;
        public SpriteRenderer labelIcon;
        public Renderer indicator;
        public Material neutral, valid, invalid, compatible;
        public bool CompatibleHint {get;private set;}
        ItemDefinition previewType;
        RackTargetFeedback targetFeedback;
        int languageRevision=-1;
        public string AvailabilityHint {get;private set;}="";
        public int RemainingSlots=>Mathf.Max(0,Capacity-Count);
        public bool AlmostFull=>Type&&RemainingSlots>0&&RemainingSlots<=Mathf.Clamp(Mathf.CeilToInt(Capacity*.1f),1,5);
        public ItemDefinition Type { get; private set; }
        public int Count { get; private set; }
        public bool SizeBased => shelves!=null && shelves.Length>0;
        // Points for the assigned type; zero while a size-based rack is empty.
        public int Capacity => SizeBased ? (Type?CapacityFor(Type):0) : (slots!=null?slots.Length:0);
        const float SurfaceOffset=.002f;
        struct Grid { public int columns,rows;public Quaternion rotation;public Bounds bounds; }
        ItemDefinition cachedType; int cachedCapacity;

        public void Initialize() { cachedType=null; Type=null; Count=0; contents=SizeBased?Array.Empty<SortItem>():new SortItem[slots!=null?slots.Length:0]; Refresh(); }
        public bool CanAccept(ItemDefinition type) => type && (!Type || Type==type) && Count<(Type?Capacity:CapacityFor(type));
        public int CapacityFor(ItemDefinition type)
        {
            if(!type) return 0;
            if(!SizeBased) return slots!=null?slots.Length:0;
            if(type==cachedType) return cachedCapacity;
            int total=0; for(int i=0;i<shelves.Length;i++) if(Layout(type,i,out var grid)) total+=grid.columns*grid.rows;
            cachedType=type; cachedCapacity=total; return total;
        }
        // The point the next item of this type would take (same rule as Add), or -1 if it cannot go here.
        public int NextSlot(ItemDefinition type)
        {
            if(!CanAccept(type))return -1;if(Count==0)return 0;
            for(int i=0;i<contents.Length;i++)if(!contents[i])return i;
            return -1;
        }
        public int Add(SortItem item)
        {
            if(!CanAccept(item.definition)) return -1;
            if(Count==0) Assign(item.definition);
            for(int i=0;i<contents.Length;i++) if(!contents[i])
            { contents[i]=item; Count++; item.rack=this; item.slot=i; item.location=ItemLocation.Rack; Refresh(); return i; }
            return -1;
        }
        public void Remove(SortItem item)
        {
            if(item.slot<0 || item.slot>=contents.Length || contents[item.slot]!=item) return;
            contents[item.slot]=null; Count--; item.rack=null; item.slot=-1;
            if(Count==0) { Type=null; if(SizeBased) contents=Array.Empty<SortItem>(); }
            Refresh();
        }
        public void Restore(SortItem item,int index)
        {
            if(!CanAccept(item.definition) || index<0 || index>=(Type?Capacity:CapacityFor(item.definition))) throw new InvalidOperationException("Invalid saved rack slot");
            if(Count==0) Assign(item.definition);
            if(contents[index]) throw new InvalidOperationException("Invalid saved rack slot");
            contents[index]=item; Count++; item.rack=this;item.slot=index;item.location=ItemLocation.Rack;Refresh();
        }
        void Assign(ItemDefinition type) { Type=type; if(SizeBased) contents=new SortItem[CapacityFor(type)]; }
        // World pose for a point of the assigned type.
        public Pose SlotPose(int index) => SlotPoseFor(Type,index);
        public Pose SlotPoseFor(ItemDefinition type,int index)
        {
            if(index<0 || index>=CapacityFor(type)) throw new ArgumentOutOfRangeException(nameof(index));
            var shelfEuler=type?Quaternion.Euler(type.shelfEuler):Quaternion.identity;
            if(!SizeBased) { var slot=slots[index]; return new Pose(slot.position,slot.rotation*shelfEuler); }
            for(int i=0;i<shelves.Length;i++)
            {
                if(!Layout(type,i,out var grid)) continue;
                int count=grid.columns*grid.rows;
                if(index>=count) { index-=count; continue; }
                var shelf=shelves[i]; var b=grid.bounds;
                // Fill the back row first, left to right as seen from the front (-Z). Spread evenly over the shelf.
                int row=index/grid.columns, column=index%grid.columns;
                float x=grid.columns>1?Mathf.Lerp(shelf.x.x+b.extents.x,shelf.x.y-b.extents.x,column/(float)(grid.columns-1)):(shelf.x.x+shelf.x.y)*.5f;
                float z=grid.rows>1?Mathf.Lerp(shelf.z.y-b.extents.z,shelf.z.x+b.extents.z,row/(float)(grid.rows-1)):(shelf.z.x+shelf.z.y)*.5f;
                var local=new Vector3(x-b.center.x,shelf.height+SurfaceOffset-b.min.y,z-b.center.z);
                return new Pose(transform.TransformPoint(local),transform.rotation*grid.rotation);
            }
            throw new ArgumentOutOfRangeException(nameof(index),"Rack point "+index+" does not exist for "+(type?type.displayName:"no type"));
        }
        bool Layout(ItemDefinition type,int shelfIndex,out Grid grid)
        {
            grid=default; if(!type || type.size.x<=0 || type.size.y<=0 || type.size.z<=0) return false;
            var shelf=shelves[shelfIndex]; float width=shelf.x.y-shelf.x.x, depth=shelf.z.y-shelf.z.x;
            var straight=Quaternion.Euler(type.shelfEuler); var turned=Quaternion.Euler(0,90,0)*straight;
            var a=RotatedBounds(type,straight); var b=RotatedBounds(type,turned);
            int ca=a.size.y+SurfaceOffset>shelf.clearance?0:Fit(width,a.size.x)*Fit(depth,a.size.z);
            int cb=b.size.y+SurfaceOffset>shelf.clearance?0:Fit(width,b.size.x)*Fit(depth,b.size.z);
            if(ca==0&&cb==0) return false;
            // Keep the authored facing unless turning the item fits more.
            bool turn=cb>ca; var bounds=turn?b:a;
            grid=new Grid{columns=Fit(width,bounds.size.x),rows=Fit(depth,bounds.size.z),rotation=turn?turned:straight,bounds=bounds};
            return true;
        }
        int Fit(float length,float size)=>size<=0||length<size?0:Mathf.FloorToInt((length+itemGap)/(size+itemGap)+.0001f);
        // Axis-aligned bounds of the item model after rotation, relative to its pivot.
        public static Bounds RotatedBounds(ItemDefinition type,Quaternion rotation)
        {
            var e=type.size*.5f; var result=new Bounds(rotation*type.center,Vector3.zero);
            for(int i=0;i<8;i++) result.Encapsulate(rotation*(type.center+new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z)));
            return result;
        }
        public void Refresh()
        {
            if(labelIcon)
            {
                var sprite=Type?Type.icon:null;
                labelIcon.sprite=sprite;labelIcon.enabled=sprite;
                if(sprite){float scale=.28f/Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y);labelIcon.transform.localScale=Vector3.one*scale;}
            }
            AvailabilityHint=Type?(RemainingSlots==0?"FULL":AlmostFull?RemainingSlots+ (RemainingSlots==1?" SLOT LEFT":" SLOTS LEFT"):""):"";
            if(!label) return;
            label.text=Type?Type.displayName+"\n"+Count+" / "+Capacity:SizeBased?"EMPTY / ANY ITEM":"EMPTY / ANY ITEM\n0 / "+Capacity;
            if(AvailabilityHint.Length>0)label.text+="  |  "+AvailabilityHint;
            label.text=LabSorting.Services.LabLocalization.Text(label.text);
            languageRevision=LabSorting.Services.LabLocalization.Revision;
        }
        public void SetCompatibleHint(bool value){if(CompatibleHint==value)return;CompatibleHint=value;ApplyIndicator();}
        public void Preview(ItemDefinition type) {previewType=type;ApplyIndicator();if(!targetFeedback&&type)targetFeedback=GetComponent<RackTargetFeedback>()??gameObject.AddComponent<RackTargetFeedback>();if(targetFeedback)targetFeedback.Show(this,type);}
        void LateUpdate(){if(languageRevision!=LabSorting.Services.LabLocalization.Revision)Refresh();}
        void OnDisable(){if(targetFeedback)targetFeedback.Show(this,null);}
        void ApplyIndicator(){if(indicator)indicator.sharedMaterial=previewType?(CanAccept(previewType)?valid:invalid):CompatibleHint&&compatible?compatible:neutral;}
        void OnValidate(){cachedType=null;}
        void OnDrawGizmosSelected()
        {
            if(!SizeBased) return; Gizmos.matrix=transform.localToWorldMatrix; Gizmos.color=Color.cyan;
            foreach(var s in shelves){var c=new Vector3((s.x.x+s.x.y)*.5f,s.height+s.clearance*.5f,(s.z.x+s.z.y)*.5f);Gizmos.DrawWireCube(c,new Vector3(s.x.y-s.x.x,s.clearance,s.z.y-s.z.x));}
            Gizmos.matrix=Matrix4x4.identity;
        }
    }
}
