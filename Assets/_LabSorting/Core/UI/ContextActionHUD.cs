using UnityEngine;
using UnityEngine.UI;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class ContextActionHUD : MonoBehaviour
    {
        public SortingSession session;
        public CanvasGroup pick,place,drop;
        public SortingHoldButton pickHold,placeHold;
        public Color tossColor=new Color(.9f,.47f,.1f,.97f);
        public bool PickVisible=>session&&!session.player.blocked&&session.TargetItem&&!session.TargetItem.Moving&&session.TargetItem.location!=ItemLocation.Hand;
        public bool CarryVisible=>session&&!session.player.blocked&&session.Active;
        public bool PlaceVisible=>CarryVisible&&session.TargetRack;
        public bool TossShown {get;private set;}
        Text dropLabel;Image dropImage;string throwText;Color throwColor;
        void Awake()
        {
            if(!drop)return;dropLabel=drop.GetComponentInChildren<Text>(true);dropImage=drop.GetComponent<Image>();
            if(dropLabel){throwText="DROP";dropLabel.text=throwText;}if(dropImage)throwColor=dropImage.color;
        }
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            if(dropLabel)dropLabel.text=session.DropPending?"DROP ...":"DROP";
            Show(pick,PickVisible,pickHold&&pickHold.PointerHeld);
            Show(place,PlaceVisible,placeHold&&placeHold.PointerHeld);
            Show(drop,CarryVisible,false);
            // DROP remains predictable regardless of the rack under the crosshair.
            bool toss=false;
            if(toss!=TossShown)
            {
                TossShown=toss;
                if(dropLabel)dropLabel.text=toss?"TOSS!":throwText;
                if(dropImage)dropImage.color=toss?tossColor:throwColor;
            }
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
