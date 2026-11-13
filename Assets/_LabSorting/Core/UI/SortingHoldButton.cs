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
        int pointer=int.MinValue; int generation;
        public bool PointerHeld=>pointer!=int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if(pointer!=int.MinValue||e.button!=PointerEventData.InputButton.Left||!GetComponent<UnityEngine.UI.Button>().IsInteractable())return;
            pointer=e.pointerId;session.BeginHold(place);generation=session.HoldGeneration;
        }
        public void OnPointerUp(PointerEventData e){if(e.pointerId==pointer)Release();}
        public void OnPointerExit(PointerEventData e){if(e.pointerId==pointer)Cancel();}
        void Release(){pointer=int.MinValue;if(session&&generation==session.HoldGeneration)session.EndHold(place);}
        void Cancel(){bool owned=PointerHeld;pointer=int.MinValue;if(owned&&session&&generation==session.HoldGeneration)session.CancelHold();}
        void Update(){if(PointerHeld&&session&&(session.player.blocked||generation!=session.HoldGeneration))Cancel();}
        void OnDisable(){Cancel();}
        void OnApplicationFocus(bool focus){if(!focus)Cancel();}
        void OnApplicationPause(bool pause){if(pause)Cancel();}
    }
}
