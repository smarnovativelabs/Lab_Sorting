using UnityEngine;
using UnityEngine.EventSystems;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class SortingHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public SortingSession session;
        public bool place;
        int pointer=int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if(pointer!=int.MinValue||e.button!=PointerEventData.InputButton.Left||!GetComponent<UnityEngine.UI.Button>().IsInteractable())return;
            pointer=e.pointerId;session.BeginHold(place);
        }
        public void OnPointerUp(PointerEventData e){if(e.pointerId==pointer)Release();}
        public void OnPointerExit(PointerEventData e){if(e.pointerId==pointer)Release();}
        void Release(){pointer=int.MinValue;if(session)session.EndHold(place);}
        void OnDisable(){Release();}
        void OnApplicationFocus(bool focus){if(!focus)Release();}
        void OnApplicationPause(bool pause){if(pause)Release();}
    }
}
