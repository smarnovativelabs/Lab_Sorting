using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace LabSorting.UI
{
    // Fires on finger DOWN instead of Button.onClick (finger up over the same button). A thumb that rolls or slides
    // a little before lifting no longer loses the action. Keep the action off Button.onClick, or a tap fires twice.
    [RequireComponent(typeof(Button))]
    public sealed class PressActionButton:MonoBehaviour,IPointerDownHandler
    {
        public UnityEvent onPress=new UnityEvent();
        Button button;
        void Awake(){button=GetComponent<Button>();}
        public void OnPointerDown(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left||!button||!button.IsInteractable())return;
            onPress.Invoke();
        }
    }
}
