using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        public int FullNoticeVersion {get;private set;}
        bool FullInventoryNotice()
        {
            FullNoticeVersion++;
            Message="Hands full! Place or drop an item.";
            MessageIsError=true;Notify();Feedback?.Invoke(SortingCue.Reject);
            return false;
        }
    }
}
