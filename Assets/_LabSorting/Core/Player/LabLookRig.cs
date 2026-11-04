using System.Collections.Generic;
using UnityEngine;

namespace LabSorting.Player
{
    /// <summary>
    /// Describes how a two-dimensional look value should be interpreted.
    /// Delta values (mouse or touch drag deltas) are applied once per frame,
    /// while rate values (sticks or virtual joysticks) are scaled by delta time.
    /// </summary>
    public enum LabLookInputMode
    {
        Delta,
        Rate,
    }

    /// <summary>
    /// Owns the yaw and pitch of the first-person camera target without depending
    /// on a specific camera package. Cinemachine can track <see cref="CameraTarget"/>.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class LabLookRig : MonoBehaviour
    {
        private const float DirectionEpsilon = 0.0001f;

        [Header("Rig")]
        [SerializeField]
        [Tooltip("A dedicated child pivot used for yaw. Do not assign the Kinematic Character Motor root.")]
        private Transform yawPivot;

        [SerializeField]
        [Tooltip("The pitched camera target. A Cinemachine camera should track this Transform.")]
        private Transform cameraTarget;

        [SerializeField]
        [Tooltip("Optional forward reference used by Recenter. Usually the character or motor root.")]
        private Transform recenterReference;

        [Header("Delta Look (mouse / touch drag)")]
        [SerializeField, Min(0f)] private float deltaYawSensitivity = 0.12f;
        [SerializeField, Min(0f)] private float deltaPitchSensitivity = 0.12f;

        [Header("Rate Look (stick / virtual joystick)")]
        [SerializeField, Min(0f)] private float yawDegreesPerSecond = 180f;
        [SerializeField, Min(0f)] private float pitchDegreesPerSecond = 140f;

        [Header("Pitch")]
        [SerializeField, Range(-89f, 0f)] private float minimumPitch = -80f;
        [SerializeField, Range(0f, 89f)] private float maximumPitch = 80f;
        [SerializeField] private bool invertVertical;
        [SerializeField] private float recenterPitch;

        private float yaw;
        private float pitch;
        private float initialYaw;
        private float initialPitch;
        private bool lookLocked;
        private readonly HashSet<object> lookLockOwners = new HashSet<object>();

        public Transform CameraTarget => cameraTarget != null ? cameraTarget : transform;
        public Quaternion CameraRotation => CameraTarget.rotation;
        public Vector3 CameraForward => CameraTarget.forward;
        public float Yaw => yaw;
        public float Pitch => pitch;
        public bool IsLookLocked => lookLocked || lookLockOwners.Count > 0;

        private void Reset()
        {
            yawPivot = transform;
            cameraTarget = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        private void Awake()
        {
            ResolveReferences();
            ReadAnglesFromTarget();

            initialYaw = yaw;
            initialPitch = pitch;

            ApplyRotation();
        }

        private void LateUpdate()
        {
            // The motor may rotate the target's parent during its simulation or
            // interpolation. Reapplying the world-space look before Cinemachine's
            // normal LateUpdate keeps the camera target frame-perfect.
            ApplyRotation();
        }

        private void OnValidate()
        {
            if (minimumPitch > maximumPitch)
            {
                minimumPitch = maximumPitch;
            }

            recenterPitch = Mathf.Clamp(recenterPitch, minimumPitch, maximumPitch);
        }

        /// <summary>
        /// Applies look input with the sensitivity appropriate to its source.
        /// Positive vertical input looks up unless vertical look is inverted.
        /// </summary>
        public void ApplyLookInput(Vector2 input, LabLookInputMode inputMode, float deltaTime)
        {
            if (IsLookLocked || input.sqrMagnitude <= 0f)
            {
                return;
            }

            float yawDelta;
            float pitchDelta;

            if (inputMode == LabLookInputMode.Rate)
            {
                float safeDeltaTime = Mathf.Max(0f, deltaTime);
                yawDelta = input.x * yawDegreesPerSecond * safeDeltaTime;
                pitchDelta = input.y * pitchDegreesPerSecond * safeDeltaTime;
            }
            else
            {
                yawDelta = input.x * deltaYawSensitivity;
                pitchDelta = input.y * deltaPitchSensitivity;
            }

            yaw = Mathf.Repeat(yaw + yawDelta, 360f);
            pitch = Mathf.Clamp(
                pitch + (invertVertical ? pitchDelta : -pitchDelta),
                minimumPitch,
                maximumPitch);

            ApplyRotation();
        }

        /// <summary>
        /// Immediately sets the look angles. X is yaw and Y is pitch in degrees.
        /// </summary>
        public void SetYawPitch(Vector2 yawPitch)
        {
            yaw = Mathf.Repeat(yawPitch.x, 360f);
            pitch = Mathf.Clamp(yawPitch.y, minimumPitch, maximumPitch);
            ApplyRotation();
        }

        /// <summary>
        /// Immediately points the camera target in a world-space direction.
        /// </summary>
        public void SnapToDirection(Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude <= DirectionEpsilon)
            {
                return;
            }

            SetAnglesFromDirection(worldDirection.normalized);
            ApplyRotation();
        }

        /// <summary>
        /// Recenters on the configured reference's forward direction and the configured
        /// recenter pitch. With no reference, restores the look captured during Awake.
        /// </summary>
        public void Recenter()
        {
            if (recenterReference == null)
            {
                yaw = initialYaw;
                pitch = initialPitch;
            }
            else
            {
                Vector3 planarForward = Vector3.ProjectOnPlane(recenterReference.forward, Vector3.up);
                if (planarForward.sqrMagnitude > DirectionEpsilon)
                {
                    yaw = DirectionToYaw(planarForward.normalized);
                }

                pitch = Mathf.Clamp(recenterPitch, minimumPitch, maximumPitch);
            }

            ApplyRotation();
        }

        /// <summary>
        /// Recenters horizontally on an explicit Transform and returns pitch to zero.
        /// </summary>
        public void RecenterTo(Transform reference)
        {
            if (reference == null)
            {
                Recenter();
                return;
            }

            Vector3 planarForward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            if (planarForward.sqrMagnitude > DirectionEpsilon)
            {
                yaw = DirectionToYaw(planarForward.normalized);
            }

            pitch = Mathf.Clamp(recenterPitch, minimumPitch, maximumPitch);
            ApplyRotation();
        }

        public void SetLookLocked(bool locked)
        {
            lookLocked = locked;
        }

        // Temporary guidance must not release a settings/dialogue lock acquired during it.
        public void AcquireLookLock(object owner) { if (owner != null) lookLockOwners.Add(owner); }
        public void ReleaseLookLock(object owner) { if (owner != null) lookLockOwners.Remove(owner); }

        public void LockLook()
        {
            SetLookLocked(true);
        }

        public void UnlockLook()
        {
            SetLookLocked(false);
        }

        private void ResolveReferences()
        {
            if (yawPivot == null)
            {
                yawPivot = transform;
            }

            if (cameraTarget == null)
            {
                cameraTarget = yawPivot;
            }
        }

        private void ReadAnglesFromTarget()
        {
            Vector3 forward = CameraTarget.forward.normalized;
            if (forward.sqrMagnitude <= DirectionEpsilon)
            {
                yaw = 0f;
                pitch = 0f;
                return;
            }

            SetAnglesFromDirection(forward);
        }

        private void SetAnglesFromDirection(Vector3 direction)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (planarDirection.sqrMagnitude > DirectionEpsilon)
            {
                yaw = DirectionToYaw(planarDirection.normalized);
            }

            // Unity's positive X rotation looks down, so upward directions use
            // negative pitch values.
            pitch = Mathf.Clamp(
                -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg,
                minimumPitch,
                maximumPitch);
        }

        private void ApplyRotation()
        {
            ResolveReferences();

            Quaternion yawRotation = Quaternion.AngleAxis(yaw, Vector3.up);
            Quaternion lookRotation = yawRotation * Quaternion.AngleAxis(pitch, Vector3.right);

            if (yawPivot != cameraTarget)
            {
                yawPivot.rotation = yawRotation;
            }

            cameraTarget.rotation = lookRotation;
        }

        private static float DirectionToYaw(Vector3 planarDirection)
        {
            return Mathf.Repeat(
                Mathf.Atan2(planarDirection.x, planarDirection.z) * Mathf.Rad2Deg,
                360f);
        }
    }
}

