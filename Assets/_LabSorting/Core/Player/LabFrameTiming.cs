using UnityEngine;

namespace LabSorting.Player
{
    // Keep the 50 Hz controller/physics precision, but do not run a long backlog
    // after an IO/GC/OS stall. Scaled gameplay deliberately slows during a hitch.
    public static class LabFrameTiming
    {
        public const float FixedStep = .02f;
        public const int MaximumCatchUpSteps = 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            Time.fixedDeltaTime = FixedStep;
            Time.maximumDeltaTime = FixedStep * MaximumCatchUpSteps;
            // Sorting does not benefit from an uncapped player loop. In particular,
            // an unfocused/batch Editor can run many render frames per physics tick.
            // Mobile ignores vSyncCount; desktop uses this explicit software cap too.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }
    }
}
