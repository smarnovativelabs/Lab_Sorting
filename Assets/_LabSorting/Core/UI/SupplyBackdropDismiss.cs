using UnityEngine;
using UnityEngine.EventSystems;
namespace LabSorting.UI
{
    // A gesture must both start and end outside the card; dragging out is not dismissal.
    public sealed class SupplyBackdropDismiss : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public SupplyPanel panel;
        public RectTransform card;
        int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            pointer = e.button == PointerEventData.InputButton.Left && Outside(e) ? e.pointerId : int.MinValue;
        }
        public void OnPointerClick(PointerEventData e)
        {
            bool dismiss = pointer == e.pointerId && e.button == PointerEventData.InputButton.Left && Outside(e);
            pointer = int.MinValue;
            if(dismiss&&panel&&(!panel.commerce.Busy||panel.commerce.CanClosePurchaseUI))
            {var animation=GetComponent<DesignerPanelMotion>();if(animation)animation.CloseAnimated(panel.Close);else panel.Close();}
        }
        bool Outside(PointerEventData e) => card && !RectTransformUtility.RectangleContainsScreenPoint(card,e.position,e.pressEventCamera);
        void OnDisable() => pointer = int.MinValue;
    }
}
