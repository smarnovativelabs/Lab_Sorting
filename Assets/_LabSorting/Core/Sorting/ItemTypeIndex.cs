using System.Collections.Generic;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        readonly Dictionary<ItemDefinition,List<SortItem>> typeIndex=new Dictionary<ItemDefinition,List<SortItem>>();
        static readonly List<SortItem> emptyType=new List<SortItem>(0);
        SortItem[] indexedItems;
        public void RebuildTypeIndex()
        {
            typeIndex.Clear();indexedItems=items;if(items==null)return;
            foreach(var item in items){if(!item||!item.definition)continue;if(!typeIndex.TryGetValue(item.definition,out var bucket))typeIndex[item.definition]=bucket=new List<SortItem>(96);bucket.Add(item);}
        }
        public List<SortItem> ItemsOfType(ItemDefinition type)
        {
            if(indexedItems!=items)RebuildTypeIndex();
            return type&&typeIndex.TryGetValue(type,out var bucket)?bucket:emptyType;
        }
    }
}
