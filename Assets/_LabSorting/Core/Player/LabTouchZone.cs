using UnityEngine;
using UnityEngine.EventSystems;

namespace LabSorting.Player
{
    /// Invisible catch area for the left thumb. It sits behind the joystick and forwards one gesture to it,
    /// so a thumb that lands near (not on) the stick still walks: the stick jumps under the thumb.
    [RequireComponent(typeof(RectTransform))]
    public sealed class LabTouchZone : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        public LabTouchArea joystick;
        public void OnPointerDown(PointerEventData e){if(joystick)joystick.OnZonePointerDown(e);}
        public void OnDrag(PointerEventData e){if(joystick)joystick.OnDrag(e);}
        public void OnPointerUp(PointerEventData e){if(joystick)joystick.OnPointerUp(e);}
        public void OnEndDrag(PointerEventData e){if(joystick)joystick.OnEndDrag(e);}
    }
}
