using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace LabSorting.Services
{
    public enum ConnectivityStatus { Unknown, Offline, NetworkAvailable, Online }

    /// <summary>Advisory signal. A network adapter or successful probe does not guarantee any SDK's service.</summary>
    [DisallowMultipleComponent]
    public sealed class InternetConnectivity : MonoBehaviour
    {
        [Tooltip("Optional primary HTTPS HEAD endpoint. No HTTP requests are made when both probe URLs are blank.")]
        [SerializeField] private string probeUrl;
        [SerializeField, Range(200, 299)] private int expectedResponseCode = 204;
        [Tooltip("Optional HTTPS HEAD fallback, checked only when the primary probe fails.")]
        [SerializeField] private string fallbackProbeUrl;
        [SerializeField, Range(200, 299)] private int fallbackExpectedResponseCode = 200;
        [SerializeField, Min(2f)] private float checkIntervalSeconds = 15f;
        [SerializeField, Min(2f)] private float maximumBackoffSeconds = 120f;
        [SerializeField, Range(1, 30)] private int requestTimeoutSeconds = 5;

        private Coroutine pollingRoutine;
        private UnityWebRequest activeRequest;
        private bool paused;
        private string validatedProbeUrl;
        private string validatedFallbackProbeUrl;
        private int consecutiveProbeFailures;
        private float nextManualCheckTime;

        public bool IsReady { get; private set; }
        public ConnectivityStatus Status { get; private set; } = ConnectivityStatus.Unknown;
        public bool HasNetworkInterface => Status == ConnectivityStatus.NetworkAvailable || Status == ConnectivityStatus.Online;
        public event Action<ConnectivityStatus> StatusChanged;

        public IEnumerator InitializeRoutine()
        {
            if (IsReady) yield break;
            validatedProbeUrl = ValidateProbeUrl(probeUrl);
            validatedFallbackProbeUrl = ValidateProbeUrl(fallbackProbeUrl);
            if (validatedFallbackProbeUrl == validatedProbeUrl) validatedFallbackProbeUrl = null;
            IsReady = true;
            SampleInterface();
            ResumePolling();
        }

        private void OnEnable()
        {
            if (IsReady) ResumePolling();
        }

        private void OnDisable() => StopPolling();

        /// <summary>UI retry. One in-flight request, with a short throttle against repeated taps.</summary>
        public void CheckNow()
        {
            if (!IsReady || paused || !isActiveAndEnabled || Time.realtimeSinceStartup < nextManualCheckTime) return;
            nextManualCheckTime = Time.realtimeSinceStartup + 2f;
            SampleInterface();
            if (activeRequest != null) return;
            if (pollingRoutine != null) StopCoroutine(pollingRoutine);
            pollingRoutine = null;
            consecutiveProbeFailures = 0;
            ResumePolling();
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            if (!IsReady) return;
            if (paused) StopPolling();
            else ResumePolling();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Android's quick settings can change focus without an application pause.
            if (hasFocus) CheckNow();
        }

        private string ValidateProbeUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri)
                && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo))
                return uri.AbsoluteUri;
            Debug.LogWarning("Connectivity probe must be a valid HTTPS URL without credentials. Invalid probe disabled.", this);
            return null;
        }

        private void ResumePolling()
        {
            if (!IsReady || paused || !isActiveAndEnabled || pollingRoutine != null) return;
            pollingRoutine = StartCoroutine(PollRoutine());
        }

        private void StopPolling()
        {
            if (pollingRoutine != null) StopCoroutine(pollingRoutine);
            pollingRoutine = null;
            if (activeRequest != null)
            {
                activeRequest.Abort();
                activeRequest.Dispose();
                activeRequest = null;
            }
            SetStatus(ConnectivityStatus.Unknown);
        }

        private IEnumerator PollRoutine()
        {
            var interfaceWait = new WaitForSecondsRealtime(2f);
            while (true)
            {
                bool hasInterface = SampleInterface();
                if (hasInterface && (validatedProbeUrl != null || validatedFallbackProbeUrl != null))
                {
                    bool succeeded = false;
                    // At most two sequential, header-only requests. Stop at the first valid response.
                    for (int probeIndex = 0; probeIndex < 2; probeIndex++)
                    {
                        string url = probeIndex == 0 ? validatedProbeUrl : validatedFallbackProbeUrl;
                        if (url == null) continue;
                        if (!SampleInterface()) break;
                        activeRequest = UnityWebRequest.Head(url);
                        activeRequest.timeout = Mathf.Clamp(requestTimeoutSeconds, 1, 30);
                        activeRequest.redirectLimit = 0;
                        yield return activeRequest.SendWebRequest();
                        int expectedCode = probeIndex == 0 ? expectedResponseCode : fallbackExpectedResponseCode;
                        succeeded = activeRequest.result == UnityWebRequest.Result.Success
                                    && activeRequest.responseCode == expectedCode;
                        activeRequest.Dispose();
                        activeRequest = null;
                        if (succeeded) break;
                    }
                    hasInterface = SampleInterface();
                    if (succeeded && hasInterface)
                    {
                        consecutiveProbeFailures = 0;
                        SetStatus(ConnectivityStatus.Online);
                    }
                    else
                    {
                        consecutiveProbeFailures = Mathf.Min(8, consecutiveProbeFailures + 1);
                        // A failed endpoint may be down while the rest of the internet works.
                        if (hasInterface) SetStatus(ConnectivityStatus.NetworkAvailable);
                    }
                }
                else consecutiveProbeFailures = 0;

                float interval = Mathf.Max(2f, checkIntervalSeconds);
                float delay = Mathf.Min(interval * Mathf.Pow(2f, consecutiveProbeFailures),
                    Mathf.Max(interval, maximumBackoffSeconds));
                float nextProbeTime = Time.realtimeSinceStartup + delay;
                // Sample the local adapter during HTTP backoff so reconnection does not wait up to
                // two minutes. This does not issue network requests or depend on gameplay timeScale.
                while (Time.realtimeSinceStartup < nextProbeTime)
                {
                    interfaceWait.waitTime = Mathf.Min(2f, nextProbeTime - Time.realtimeSinceStartup);
                    yield return interfaceWait;
                    if (SampleInterface() == hasInterface) continue;
                    consecutiveProbeFailures = 0;
                    break;
                }
            }
        }

        private bool SampleInterface()
        {
            bool available = Application.internetReachability != NetworkReachability.NotReachable;
            if (!available) SetStatus(ConnectivityStatus.Offline);
            else if (Status != ConnectivityStatus.Online) SetStatus(ConnectivityStatus.NetworkAvailable);
            return available;
        }

        private void SetStatus(ConnectivityStatus value)
        {
            if (Status == value) return;
            Status = value;
            if (StatusChanged == null) return;
            foreach (Action<ConnectivityStatus> listener in StatusChanged.GetInvocationList())
            {
                try { listener(value); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
