using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    // Left/right arrows at the ends of the store's product row: they show only when more cards lie that way
    // and glide one card per tap, so players notice the row scrolls.
    public sealed class StoreScrollArrows:MonoBehaviour
    {
        public ScrollRect scroll;
        public GameObject left,right;
        [Tooltip("Card pitch in content units (card width + spacing).")]
        public float step=232;
        public float speed=4;
        float target=-1;
        public void Left()=>Move(-1);
        public void Right()=>Move(1);
        void Move(int direction)
        {
            if(!scroll||!scroll.content||!scroll.viewport)return;
            float overflow=scroll.content.rect.width-scroll.viewport.rect.width;if(overflow<=1)return;
            float from=target>=0?target:scroll.horizontalNormalizedPosition;
            target=Mathf.Clamp01(from+direction*step/overflow);scroll.StopMovement();
        }
        void OnEnable(){target=-1;}
        void Update()
        {
            if(!scroll)return;
            if(target>=0)
            {
                float position=Mathf.MoveTowards(scroll.horizontalNormalizedPosition,target,speed*Time.unscaledDeltaTime);
                scroll.horizontalNormalizedPosition=position;if(Mathf.Approximately(position,target))target=-1;
            }
            bool scrollable=scroll.content&&scroll.viewport&&scroll.content.rect.width>scroll.viewport.rect.width+1;
            float now=scroll.horizontalNormalizedPosition;
            if(left&&left.activeSelf!=(scrollable&&now>.01f))left.SetActive(scrollable&&now>.01f);
            if(right&&right.activeSelf!=(scrollable&&now<.99f))right.SetActive(scrollable&&now<.99f);
        }
    }
}
