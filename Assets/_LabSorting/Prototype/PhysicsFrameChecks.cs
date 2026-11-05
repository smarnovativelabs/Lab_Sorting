using System;
using System.Collections;
using System.IO;
using UnityEngine;
using LabSorting.Player;

namespace LabSorting.Prototype
{
    public sealed class PhysicsFrameChecks : MonoBehaviour
    {
        int lastFrame = -1, steps;
        public int maximumSteps;
        void FixedUpdate()
        {
            if (lastFrame != Time.frameCount) { lastFrame = Time.frameCount; steps = 0; }
            maximumSteps = Mathf.Max(maximumSteps, ++steps);
        }

        [Serializable] sealed class Report
        {
            public float fixedStep, maximumDelta;
            public int maximumObservedSteps, injectedStalls = 3, stallMilliseconds = 200;
        }

        public static IEnumerator Run(Action<bool,string> check)
        {
            var go = new GameObject("Physics catch-up probe");
            var probe = go.AddComponent<PhysicsFrameChecks>();
            try
            {
                check(Mathf.Abs(Time.fixedDeltaTime-LabFrameTiming.FixedStep)<.00001f,"Physics retains 50 Hz precision");
                check(Mathf.Abs(Time.maximumDeltaTime-.06f)<.00001f,"Physics catch-up budget is 60ms");
                check(Application.targetFrameRate==60&&QualitySettings.vSyncCount==0,"Gameplay frame pacing targets 60 FPS");
                for(int i=0;i<60;i++)
                {
                    // Deliberate test-only stall. Physics remains in its normal player loop.
                    if(i==10||i==25||i==40) System.Threading.Thread.Sleep(200);
                    yield return null;
                }
                File.WriteAllText("Logs/PhysicsCatchUp.json",JsonUtility.ToJson(new Report{
                    fixedStep=Time.fixedDeltaTime,maximumDelta=Time.maximumDeltaTime,
                    maximumObservedSteps=probe.maximumSteps},true));
                check(probe.maximumSteps>=2&&probe.maximumSteps<=LabFrameTiming.MaximumCatchUpSteps,
                    "Real physics catch-up stays bounded after three 200ms stalls (steps="+probe.maximumSteps+")");
            }
            finally { Destroy(go); }
        }
    }
}
