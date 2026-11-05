namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        public bool CompletionAcknowledged {get;private set;}
        public void AcknowledgeCompletion()
        {
            if(CompletionAcknowledged||items.Length==0||SortedCount!=items.Length||!HasMilestone("complete"))return;
            CompletionAcknowledged=true;Notify();SaveTransaction();
        }
    }
}

