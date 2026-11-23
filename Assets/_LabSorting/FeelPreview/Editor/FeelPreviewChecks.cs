using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace LabSorting.FeelPreview.Editor
{
    public static class FeelPreviewChecks
    {
        public static IEnumerator Run(InteractionFeelPreview p)
        {
            var report = new StringBuilder();
            Directory.CreateDirectory("Logs/FeelPreview");
            int firstFrame = Time.frameCount;
            for (int model = 0; model < p.templates.Length; model++)
            {
                foreach (bool proposed in new[] { false, true })
                {
                    p.model = model; p.SetMode(proposed); p.batch = proposed; p.SetEffects(proposed ? 2 : 0);
                    p.AutoDemo();
                    yield return new WaitForSecondsRealtime(8);
                    bool ok = p.HeldCount == 0 && p.rack.Count == 3 && p.BusyCount == 0;
                    report.AppendLine((ok ? "PASS " : "FAIL ") + p.templates[model].definition.name + " proposed=" + proposed + " rack=" + p.rack.Count + " held=" + p.HeldCount + " busy=" + p.BusyCount);
                    File.WriteAllText("Logs/FeelPreview/report.txt", report.ToString());
                    if (!ok) throw new Exception(report.ToString());
                }
            }
            p.model = 0; p.SetMode(true); p.batch = false; p.SetEffects(1);
            p.PickNext(); yield return new WaitForSecondsRealtime(.5f);
            if (!p.PreviewVisible) throw new Exception("Placement ghost not visible with an item in hand");
            report.AppendLine("PASS placement ghost uses next valid rack slot");
            p.Place(); yield return new WaitForSecondsRealtime(.6f);
            if (p.rack.Count != 1 || p.BusyCount != 0) throw new Exception("Single placement failed");
            report.AppendLine("PASS single placement");
            p.PickNext(); p.ResetPreview(); yield return new WaitForSecondsRealtime(.5f);
            if (p.HeldCount != 0 || p.rack.Count != 0 || p.BusyCount != 0) throw new Exception("Reset did not cancel pickup");
            report.AppendLine("PASS reset during pickup");
            report.AppendLine("PASS advancing player loop: " + firstFrame + " -> " + Time.frameCount);
            File.WriteAllText("Logs/FeelPreview/report.txt", report.ToString());
            Debug.Log("FEEL_PREVIEW_CHECKS_PASS\n" + report);
        }
    }
}
