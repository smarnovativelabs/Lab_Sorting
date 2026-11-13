using System.Collections;
using UnityEngine;
namespace LabSorting.Services
{
    public sealed partial class SupplyCommerce
    {
        Coroutine rewardSaveRetry, rewardCloseRoutine;
        // Ad callbacks can arrive while the gameplay clock is paused.
        IEnumerator RetryAdReward()
        {
            yield return new WaitForSecondsRealtime(2);
            rewardSaveRetry=null;
            if(rewardGranted&&rewardToken!=null)SaveAdReward();
        }
        void QueueRewardClose()
        {
            if(rewardCloseRoutine==null)rewardCloseRoutine=StartCoroutine(CloseRewardAfterDelay());
        }
        IEnumerator CloseRewardAfterDelay()
        {
            // Give a completion callback arriving just after hidden time to finish saving.
            yield return new WaitForSecondsRealtime(2);
            rewardCloseRoutine=null;
            EndReward();
        }
    }
}
