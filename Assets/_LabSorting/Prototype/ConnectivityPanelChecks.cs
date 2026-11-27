#if UNITY_EDITOR
using System;
using System.Collections;
using LabSorting.Services;
using LabSorting.UI;
using UnityEngine;

namespace LabSorting.Prototype
{
    public static class ConnectivityPanelChecks
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("CONNECTIVITY: " + message+"; status="+LabServiceRoot.Instance.internet.Status+", ready="+LabServiceRoot.Instance.internet.IsReady+", frame="+Time.frameCount);
            Debug.Log("CONNECTIVITY PASS: " + message);
        }
        static IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Check(condition(), message);
        }
        public static IEnumerator Run()
        {
            var root = LabServiceRoot.Instance;
            var network = root.internet;
            var ui = UnityEngine.Object.FindFirstObjectByType<ServicePresentation>();
            var player = ui.hud.session.player;
            bool adapter = true, internet = true;
            bool automatic = ui.automaticConnectivity;
            float timeScale = Time.timeScale;
            network.enabled = false;
            network.InterfaceAvailableForTests = () => adapter;
            network.ProbeSuccessForTests = () => internet;
            ui.automaticConnectivity = true;
            network.enabled = true;
            try
            {
                yield return Until(() => network.Status == ConnectivityStatus.Online && !ui.offlineRoot.activeSelf && !player.NetworkBlocked,
                    8, "Verified internet hides panel and releases network movement lock");
                adapter = false;
                yield return Until(() => ui.offlineRoot.activeSelf && player.NetworkBlocked, 5,
                    "Network adapter off automatically shows panel and blocks movement");
                Check(Cursor.lockState == CursorLockMode.None, "Offline panel releases pointer");
                player.SetBlocked(false);
                Check(player.blocked, "Other UI cannot bypass the network lock");
                Time.timeScale = 0;
                adapter = true;
                internet = false;
                yield return Until(() => network.Status == ConnectivityStatus.NetworkAvailable, 5,
                    "Adapter restoration alone does not pretend internet is online");
                Check(ui.offlineRoot.activeSelf && player.NetworkBlocked, "Panel stays visible without verified internet");
                // Hold a router outage long enough to expose exponential recovery backoff.
                yield return new WaitForSecondsRealtime(17);
                internet = true;
                yield return Until(() => network.Status == ConnectivityStatus.Online && !ui.offlineRoot.activeSelf && !player.NetworkBlocked,
                    8, "Router internet recovery closes panel automatically within eight seconds, even at timeScale zero");
                internet = false;
                yield return Until(() => ui.offlineRoot.activeSelf && player.NetworkBlocked, 16,
                    "Internet loss with Wi-Fi still connected shows panel after failed probes");
                internet = true;
                ui.Retry();
                yield return Until(() => !ui.offlineRoot.activeSelf && !player.NetworkBlocked, 5,
                    "Retry verifies restored internet and closes panel");
                network.SendMessage("OnApplicationPause", true);
                yield return null;
                Check(!ui.offlineRoot.activeSelf, "Background pause alone does not show a false offline panel");
                adapter = false;
                network.SendMessage("OnApplicationPause", false);
                yield return Until(() => ui.offlineRoot.activeSelf, 5, "Resuming offline shows panel");
                adapter = internet = true;
                yield return Until(() => !ui.offlineRoot.activeSelf && !player.NetworkBlocked, 8,
                    "Repeated off/on cycle recovers without a stuck panel");
                Debug.Log("CONNECTIVITY_PANEL_CHECKS_OK");
            }
            finally
            {
                Time.timeScale = timeScale;
                network.enabled = false;
                network.InterfaceAvailableForTests = null;
                network.ProbeSuccessForTests = null;
                ui.automaticConnectivity = automatic;
                network.enabled = true;
            }
        }
    }
}
#endif
