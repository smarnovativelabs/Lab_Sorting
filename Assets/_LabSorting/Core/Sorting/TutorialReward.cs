namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        // One-time cash for finishing the guided tutorial. Stored in the save, so replaying the tutorial never pays twice.
        public bool TutorialRewarded {get;private set;}
        public bool ClaimTutorialReward(int amount)
        {
            if(TutorialRewarded||amount<=0)return false;
            TutorialRewarded=true;cash+=amount;
            SmallGoalEarned?.Invoke("Tutorial complete!",amount);
            Notify();SaveTransaction();return true;
        }
    }
}
