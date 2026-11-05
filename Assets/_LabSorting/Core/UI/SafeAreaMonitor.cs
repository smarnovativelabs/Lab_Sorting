using System;
using System.Collections.Generic;
using UnityEngine;

namespace LabSorting.UI
{
    /// <summary>
    /// One low-frequency device-safe-area watcher shared by every active SafeAreaPanel.
    /// The hidden runtime owner is created on demand and stops Update when no panels are active.
    /// Edit-mode previews share one EditorApplication.update callback instead of per-panel polling.
    /// </summary>
    [AddComponentMenu(""), DisallowMultipleComponent]
    public sealed class SafeAreaMonitor : MonoBehaviour
    {
        private const double PollIntervalSeconds = .25;
        private static readonly List<SafeAreaPanel> Panels = new List<SafeAreaPanel>(16);
        private static SafeAreaPanel[] subscribers = Array.Empty<SafeAreaPanel>();
        private static SafeAreaMonitor driver;
        private static bool quitting;
        private static bool hasSample;
        private static Rect lastSafeArea;
        private static Vector2Int lastSize;
        private static ScreenOrientation lastOrientation;
        private static double nextRuntimePoll;
#if UNITY_EDITOR
        private static bool editorWatching;
        private static double nextEditorPoll;
#endif

        internal static void Register(SafeAreaPanel panel)
        {
            if (panel == null || !panel.isActiveAndEnabled || quitting) return;
            if (!Panels.Contains(panel)) { Panels.Add(panel); subscribers = Panels.ToArray(); }
            if (Application.isPlaying)
            {
                if (driver == null)
                {
                    var owner = new GameObject("Safe Area Monitor (Shared)");
                    owner.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    driver = owner.AddComponent<SafeAreaMonitor>();
                }
                driver.enabled = true;
            }
#if UNITY_EDITOR
            else StartEditorWatch();
#endif
        }

        internal static void Unregister(SafeAreaPanel panel)
        {
            if (ReferenceEquals(panel, null)) return;
            if (Panels.Remove(panel)) subscribers = Panels.ToArray();
            if (Panels.Count != 0) return;
            if (driver != null) driver.enabled = false;
#if UNITY_EDITOR
            StopEditorWatch();
#endif
        }

        private void Awake()
        {
            if (driver != null && driver != this) { Destroy(gameObject); return; }
            driver = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable() { nextRuntimePoll = 0; }

        private void Update()
        {
            if (driver != this || quitting) return;
            double now = Time.unscaledTimeAsDouble;
            if (now < nextRuntimePoll) return;
            nextRuntimePoll = now + PollIntervalSeconds;
            Poll();
        }

        private void OnApplicationPause(bool paused) { if (!paused && !quitting) Poll(); }
        private void OnApplicationFocus(bool focused) { if (focused && !quitting) Poll(); }
        private void OnApplicationQuit() { quitting = true; }
        private void OnDestroy() { if (driver == this) driver = null; }

        private static void Poll()
        {
            if (subscribers.Length == 0) return;
            Rect area = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            ScreenOrientation orientation = Screen.orientation;
            if (hasSample && area == lastSafeArea && size == lastSize && orientation == lastOrientation) return;
            lastSafeArea = area; lastSize = size; lastOrientation = orientation; hasSample = true;
            SafeAreaPanel[] snapshot = subscribers;
            bool prune = false;
            for (int i = 0; i < snapshot.Length; i++)
            {
                SafeAreaPanel panel = snapshot[i];
                if (panel == null || !panel.isActiveAndEnabled) { prune = true; continue; }
                panel.ApplySample(area, size);
            }
            if (!prune) return;
            Panels.RemoveAll(value => value == null || !value.isActiveAndEnabled);
            subscribers = Panels.ToArray();
            if (subscribers.Length == 0)
            {
                if (driver != null) driver.enabled = false;
#if UNITY_EDITOR
                StopEditorWatch();
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            Panels.Clear(); subscribers = Array.Empty<SafeAreaPanel>();
            driver = null; quitting = false; hasSample = false; nextRuntimePoll = 0;
#if UNITY_EDITOR
            StopEditorWatch();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreActivePanels()
        {
            // OnEnable need not run when both domain and scene reload are disabled. Recover the
            // live owner/registrations once at startup, never with scene searches during Update.
            // Include hidden/DontSave owners, which ordinary FindObjectsByType can omit.
            SafeAreaMonitor[] owners = Resources.FindObjectsOfTypeAll<SafeAreaMonitor>();
            for (int i = 0; i < owners.Length; i++)
            {
                if (!owners[i].gameObject.scene.IsValid()) continue;
                if (driver == null) driver = owners[i];
                else if (owners[i] != driver) Destroy(owners[i].gameObject);
            }
            foreach (SafeAreaPanel panel in FindObjectsByType<SafeAreaPanel>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (panel.isActiveAndEnabled) { Register(panel); panel.Apply(); }
            if (driver != null) driver.enabled = Panels.Count > 0;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeEditorWatch()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= HandlePlayMode;
            UnityEditor.EditorApplication.playModeStateChanged += HandlePlayMode;
        }

        private static void StartEditorWatch()
        {
            if (editorWatching) return;
            editorWatching = true;
            nextEditorPoll = 0;
            UnityEditor.EditorApplication.update += EditorTick;
        }

        private static void StopEditorWatch()
        {
            UnityEditor.EditorApplication.update -= EditorTick;
            editorWatching = false;
        }

        private static void EditorTick()
        {
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now < nextEditorPoll) return;
            nextEditorPoll = now + PollIntervalSeconds;
            Poll();
        }

        private static void HandlePlayMode(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingEditMode) StopEditorWatch();
            else if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode) quitting = true;
            else if (change == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                Panels.Clear(); subscribers = Array.Empty<SafeAreaPanel>();
                driver = null; quitting = false; hasSample = false;
                foreach (SafeAreaPanel panel in FindObjectsByType<SafeAreaPanel>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (panel.isActiveAndEnabled) { Register(panel); panel.Apply(); }
            }
        }
#endif
    }
}
