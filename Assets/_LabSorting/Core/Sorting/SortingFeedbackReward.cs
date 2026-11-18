using Lofelt.NiceVibrations;
namespace LabSorting.Sorting
{
    public sealed partial class SortingFeedback
    {
        // Light "you got something" tap for cash and supply rewards (respects the Haptics setting).
        public void Reward()
        {
            if(!isActiveAndEnabled)return;
            #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if(HapticsEnabled)HapticPatterns.PlayPreset(HapticPatterns.PresetType.Success);
            #endif
        }
    }
}
