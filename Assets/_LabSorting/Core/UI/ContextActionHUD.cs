using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class ContextActionHUD : MonoBehaviour
    {
        public SortingSession session;
        public CanvasGroup pick,place,drop;
        public SortingHoldButton pickHold,placeHold;
        public bool PickVisible=>session&&!session.player.blocked&&session.TargetItem&&!session.TargetItem.Moving&&session.TargetItem.location!=ItemLocation.Hand;
        public bool CarryVisible=>session&&!session.player.blocked&&session.Active;
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            Show(pick,PickVisible,pickHold&&pickHold.PointerHeld);
            Show(place,CarryVisible,placeHold&&placeHold.PointerHeld);
            Show(drop,CarryVisible,false);
        }
        void Show(CanvasGroup group,bool visible,bool captured)
        {
            if(!group)return;
            group.alpha=visible?1:0;
            // Keep an existing pointer until release/exit, even while the button is hidden
            // between pickup targets. Disabling its GameObject would cancel hold-to-pick.
            group.blocksRaycasts=visible||captured;
            group.interactable=!session.player.blocked&&(visible||captured);
        }
    }
}
