using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed class SortRack : MonoBehaviour
    {
        public string id;
        public Transform[] slots;
        public SortItem[] contents;
        public TextMesh label;
        public Renderer indicator;
        public Material neutral, valid, invalid;
        public ItemDefinition Type { get; private set; }
        public int Count { get; private set; }
        public void Initialize() { contents=new SortItem[slots.Length]; Type=null; Count=0; Refresh(); }
        public bool CanAccept(ItemDefinition type) => type && Count<slots.Length && (!Type || Type==type);
        public int Add(SortItem item)
        {
            if(!CanAccept(item.definition)) return -1;
            for(int i=0;i<contents.Length;i++) if(!contents[i])
            { contents[i]=item; Count++; Type=item.definition; item.rack=this; item.slot=i; item.location=ItemLocation.Rack; Refresh(); return i; }
            return -1;
        }
        public void Remove(SortItem item)
        {
            if(item.slot<0 || item.slot>=contents.Length || contents[item.slot]!=item) return;
            contents[item.slot]=null; Count--; if(Count==0) Type=null; item.rack=null; item.slot=-1; Refresh();
        }
        public void Restore(SortItem item,int index)
        {
            if(index<0 || index>=contents.Length || contents[index] || !CanAccept(item.definition)) throw new System.InvalidOperationException("Invalid saved rack slot");
            contents[index]=item; Count++; Type=item.definition; item.rack=this;item.slot=index;item.location=ItemLocation.Rack;Refresh();
        }
        public void Refresh() { if(label) label.text=(Type?Type.displayName:"EMPTY / ANY ITEM")+"\n"+Count+" / "+slots.Length; }
        public void Preview(ItemDefinition type) { if(indicator) indicator.sharedMaterial=type?(CanAccept(type)?valid:invalid):neutral; }
    }
}
