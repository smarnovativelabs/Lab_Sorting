using System.Collections.Generic;
namespace LabSorting.Sorting
{
    public sealed class ItemProgress
    {
        public ItemDefinition type;
        public int total,sorted,held;
        public int Remaining=>total-sorted;
    }
    public sealed partial class SortingSession
    {
        public List<ItemProgress> GetItemProgress()
        {
            var rows=new List<ItemProgress>();var byType=new Dictionary<ItemDefinition,ItemProgress>();
            foreach(var item in items)
            {
                if(!byType.TryGetValue(item.definition,out var row)) {row=new ItemProgress{type=item.definition};byType.Add(item.definition,row);rows.Add(row);}
                row.total++;if(item.location==ItemLocation.Rack) row.sorted++;if(item.location==ItemLocation.Hand) row.held++;
            }
            return rows;
        }
    }
}
