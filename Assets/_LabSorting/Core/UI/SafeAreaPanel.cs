using UnityEngine;

namespace LabSorting.UI
{
    /// <summary>
    /// Fits a full-screen Canvas child to the device-safe rectangle. One shared, low-frequency
    /// SafeAreaMonitor watches all enabled panels; this component has no per-frame Update.
    ///
    /// Egg-style hierarchy: Background Overscan=(100,80) expands this root's offsets to
    /// (-100,-80)/(100,80). Put buttons under a full-stretch Inner Content with the opposite
    /// offsets, (100,80)/(-100,-80), so their final edges follow the OS safe area. Overscan and
    /// Inner Content offsets are Canvas units, NOT physical screen pixels. Extra Padding is pixels.
    /// Keep backgrounds/fullscreen blockers outside Inner Content and the reticle outside this root.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        [Header("Edges")]
        [Tooltip("Inset from the notch/status bar side.")]
        [SerializeField] private bool applyTop = true;

        [Tooltip("Inset from the home indicator / gesture bar.")]
        [SerializeField] private bool applyBottom = true;

        [Tooltip("Inset from the left cutout. In landscape this is where the notch usually lands.")]
        [SerializeField] private bool applyLeft = true;

        [SerializeField] private bool applyRight = true;

        [Header("Extra Padding")]
        [Tooltip("Additional inset in pixels on top of the OS-reported safe area. A few pixels " +
                 "keeps buttons off the very edge of a rounded screen, where they are awkward to hit.")]
        [SerializeField, Min(0f)] private float extraPadding;

        [Header("Optional Background Overscan")]
        [Tooltip("Canvas units added outside the device-safe anchors. Leave zero for legacy behavior. " +
                 "For the Egg-style layout use (100,80), then give a full-stretch Inner Content the " +
                 "opposite positive insets: Left/Right=100, Top/Bottom=80.")]
        [SerializeField] private Vector2 backgroundOverscan;

        private RectTransform panel;

        public Vector2 BackgroundOverscan
        {
            get => backgroundOverscan;
            set
            {
                Vector2 clean = CleanOverscan(value);
                if (backgroundOverscan == clean) return;
                backgroundOverscan = clean;
                if (isActiveAndEnabled) Apply();
            }
        }

        // Caching only - no functional work.
        private void Awake()
        {
            panel = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            SafeAreaMonitor.Register(this);
            Apply();
        }

        private void OnDisable() => SafeAreaMonitor.Unregister(this);
        private void OnDestroy() => SafeAreaMonitor.Unregister(this);

        /// <summary>Recomputes immediately. Call after anything that changes the screen layout.</summary>
        public void Apply() => ApplySample(Screen.safeArea, new Vector2Int(Screen.width, Screen.height));

        /// <summary>
        /// Applies an OS-style sample. Safe rectangle/resolution use physical pixels; overscan uses
        /// Canvas units. Returns true only when RectTransform values actually changed. Invalid
        /// mid-rotation samples leave the last usable layout untouched. Also used by native tests.
        /// </summary>
        public bool ApplySample(Rect safeAreaPixels, Vector2Int screenPixels)
        {
            if (panel == null) panel = GetComponent<RectTransform>();
            if (panel == null || screenPixels.x <= 0 || screenPixels.y <= 0
                || !Finite(safeAreaPixels.x) || !Finite(safeAreaPixels.y)
                || !Finite(safeAreaPixels.width) || !Finite(safeAreaPixels.height)
                || safeAreaPixels.width <= 0 || safeAreaPixels.height <= 0) return false;

            float safeLeft = Mathf.Clamp(safeAreaPixels.xMin, 0, screenPixels.x);
            float safeRight = Mathf.Clamp(safeAreaPixels.xMax, 0, screenPixels.x);
            float safeBottom = Mathf.Clamp(safeAreaPixels.yMin, 0, screenPixels.y);
            float safeTop = Mathf.Clamp(safeAreaPixels.yMax, 0, screenPixels.y);
            if (safeRight <= safeLeft || safeTop <= safeBottom) return false;

            float baseLeft = applyLeft ? safeLeft : 0;
            float baseRight = applyRight ? safeRight : screenPixels.x;
            float baseBottom = applyBottom ? safeBottom : 0;
            float baseTop = applyTop ? safeTop : screenPixels.y;
            float inset = Finite(extraPadding) ? Mathf.Max(0, extraPadding) : 0;
            float left = baseLeft + (applyLeft ? inset : 0);
            float right = baseRight - (applyRight ? inset : 0);
            float bottom = baseBottom + (applyBottom ? inset : 0);
            float top = baseTop - (applyTop ? inset : 0);
            // Excessive optional padding must not collapse a panel or override disabled edges.
            if (right <= left) { left = baseLeft; right = baseRight; }
            if (top <= bottom) { bottom = baseBottom; top = baseTop; }

            Vector2 min = new Vector2(left / screenPixels.x, bottom / screenPixels.y);
            Vector2 max = new Vector2(right / screenPixels.x, top / screenPixels.y);
            Vector2 overscan = CleanOverscan(backgroundOverscan);
            bool changed = panel.anchorMin != min || panel.anchorMax != max
                || panel.offsetMin != -overscan || panel.offsetMax != overscan;
            if (!changed) return false;
            // Anchors first, offsets second. No changes to the artist's pivot, scale or children.
            if (panel.anchorMin != min) panel.anchorMin = min;
            if (panel.anchorMax != max) panel.anchorMax = max;
            if (panel.offsetMin != -overscan) panel.offsetMin = -overscan;
            if (panel.offsetMax != overscan) panel.offsetMax = overscan;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Vector2 CleanOverscan(Vector2 value) => new Vector2(
            Finite(value.x) ? Mathf.Max(0, value.x) : 0, Finite(value.y) ? Mathf.Max(0, value.y) : 0);

#if UNITY_EDITOR
        private void OnValidate()
        {
            extraPadding = Finite(extraPadding) ? Mathf.Max(0, extraPadding) : 0;
            backgroundOverscan = CleanOverscan(backgroundOverscan);
            if (!isActiveAndEnabled) return;
            SafeAreaMonitor.Register(this);
            Apply();
        }
#endif
    }
}
