using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        float nextFullNotice;
        public int FullNoticeVersion {get;private set;}
        bool FullInventoryNotice()
        {
            if(Time.unscaledTime<nextFullNotice)return false;
            nextFullNotice=Time.unscaledTime+3;
            FullNoticeVersion++;
            return Fail("Hands full — place items on a matching rack. No charge.");
        }
    }
}
