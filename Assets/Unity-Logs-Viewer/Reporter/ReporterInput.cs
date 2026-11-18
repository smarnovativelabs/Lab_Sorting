using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

// Reporter (Unity-Logs-Viewer) was written for the legacy Input Manager. This project uses the Input System only
// (Player Settings > Active Input Handling), where every UnityEngine.Input call throws InvalidOperationException,
// so Reporter.Update threw every frame. This shim offers the same few members Reporter reads (touches, mouse button
// 0 and mouse position) from the Input System, and falls back to UnityEngine.Input when the legacy manager is on.
static class ReporterInput
{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
	static readonly System.Collections.Generic.List<UnityEngine.Touch> active = new System.Collections.Generic.List<UnityEngine.Touch>();
	static int cachedFrame = -1;
	static UnityEngine.Touch[] cached = new UnityEngine.Touch[0];

	// Pressed touches plus touches released this frame, with legacy-style phases.
	public static UnityEngine.Touch[] touches
	{
		get
		{
			if (cachedFrame == Time.frameCount)
				return cached;
			cachedFrame = Time.frameCount;
			active.Clear();
			var screen = Touchscreen.current;
			if (screen != null) {
				foreach (TouchControl t in screen.touches) {
					bool began = t.press.wasPressedThisFrame, ended = t.press.wasReleasedThisFrame;
					if (!t.press.isPressed && !ended)
						continue;
					var phase = began ? UnityEngine.TouchPhase.Began
						: ended ? UnityEngine.TouchPhase.Ended
						: t.delta.ReadValue().sqrMagnitude > 0f ? UnityEngine.TouchPhase.Moved
						: UnityEngine.TouchPhase.Stationary;
					active.Add(new UnityEngine.Touch { fingerId = t.touchId.ReadValue(), position = t.position.ReadValue(), phase = phase });
				}
			}
			cached = active.ToArray();
			return cached;
		}
	}

	static ButtonControl Button(int button)
	{
		var mouse = Mouse.current;
		if (mouse == null)
			return null;
		return button == 1 ? mouse.rightButton : button == 2 ? mouse.middleButton : mouse.leftButton;
	}

	public static Vector3 mousePosition
	{
		get { var mouse = Mouse.current; return mouse != null ? (Vector3)mouse.position.ReadValue() : Vector3.zero; }
	}
	public static bool GetMouseButton(int button) { var c = Button(button); return c != null && c.isPressed; }
	public static bool GetMouseButtonDown(int button) { var c = Button(button); return c != null && c.wasPressedThisFrame; }
	public static bool GetMouseButtonUp(int button) { var c = Button(button); return c != null && c.wasReleasedThisFrame; }
#else
	public static UnityEngine.Touch[] touches => Input.touches;
	public static Vector3 mousePosition => Input.mousePosition;
	public static bool GetMouseButton(int button) => Input.GetMouseButton(button);
	public static bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
	public static bool GetMouseButtonUp(int button) => Input.GetMouseButtonUp(button);
#endif
}
