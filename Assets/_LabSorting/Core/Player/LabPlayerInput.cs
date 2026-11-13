using UnityEngine;
using UnityEngine.InputSystem;

namespace LabSorting.Player
{
    // Adapted from JanitorPlayerInput: separate touch deltas, UI gates and focus resets.
    [DefaultExecutionOrder(10000)]
    public sealed class LabPlayerInput : MonoBehaviour
    {
        public LabCharacterMotor character;
        public LabLookRig look;
        public Camera view;
        public bool blocked;
        public float touchSensitivity = 1f;
        public Vector2 Move { get; private set; }
        public float LastLookInputTime {get;private set;}=-100;
        Vector2 drag;
        bool focused = true;
        bool requestedBlocked;
        public bool NetworkBlocked {get;private set;} public bool TutorialBlocked {get;private set;} public void SetTutorialBlocked(bool value){TutorialBlocked=value;blocked=requestedBlocked||NetworkBlocked||value;Clear();}
        public void SetMove(Vector2 value) { Move = blocked ? Vector2.zero : Vector2.ClampMagnitude(value, 1); }
        public void AddLook(Vector2 value) { if (!blocked && focused){drag += value * touchSensitivity;if(value.sqrMagnitude>.01f)LastLookInputTime=Time.unscaledTime;} }
        public void SetBlocked(bool value) { requestedBlocked=value;blocked = value||NetworkBlocked||TutorialBlocked; Clear(); }
        public void SetNetworkBlocked(bool value){if(value&&!NetworkBlocked)requestedBlocked=blocked&&!TutorialBlocked;NetworkBlocked=value;blocked=requestedBlocked||value||TutorialBlocked;Clear();}
        public void Clear() { Move = drag = Vector2.zero; if(character) character.CancelPendingJump(); }
        void OnDisable() { Clear(); }
        void OnApplicationFocus(bool value) { focused = value; Clear(); }
        void OnApplicationPause(bool value) { focused = !value; Clear(); }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Clear(); }
            Vector2 movement = Move;
            if (focused && !blocked)
            {
                if (keyboard != null)
                    movement += new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0), (keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
                if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
                    drag += Mouse.current.delta.ReadValue();
                if (Gamepad.current != null) movement += Gamepad.current.leftStick.ReadValue();
                if(drag.sqrMagnitude>.01f||(Gamepad.current!=null&&Gamepad.current.rightStick.ReadValue().sqrMagnitude>.01f))LastLookInputTime=Time.unscaledTime;
                look.ApplyLookInput(drag, LabLookInputMode.Delta, Time.deltaTime);
                if (Gamepad.current != null) look.ApplyLookInput(Gamepad.current.rightStick.ReadValue(), LabLookInputMode.Rate, Time.deltaTime);
            }
            else movement = Vector2.zero;
            drag = Vector2.zero;
            movement = Vector2.ClampMagnitude(movement, 1);
            var input = new PlayerCharacterInputs { MoveAxisRight=movement.x, MoveAxisForward=movement.y, CameraRotation=look.CameraRotation };
            character.SetInputs(ref input);
        }
        void LateUpdate() { if(character && look) character.PostInputUpdate(Time.deltaTime,look.CameraForward); }
    }
}
