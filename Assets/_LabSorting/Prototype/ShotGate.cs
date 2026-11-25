namespace LabSorting.Prototype
{
    // Review screenshots only exist for manual inspection. An unfocused Editor logs an error on capture,
    // which fails the whole test run, so in the Editor they stay off unless SessionState "LabSorting.CaptureShots" is set.
    // Some check classes are compiled into players, so this class must exist there too.
    public static class ShotGate
    {
        public static void Capture(string path)
        {
#if UNITY_EDITOR
            if(!UnityEditor.SessionState.GetBool("LabSorting.CaptureShots",false))return;
#endif
            UnityEngine.ScreenCapture.CaptureScreenshot(path);
        }
    }
}
