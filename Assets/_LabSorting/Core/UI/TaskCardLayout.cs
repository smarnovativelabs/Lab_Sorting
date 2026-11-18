using UnityEngine;
namespace LabSorting.UI
{
    public sealed class TaskCardLayout:MonoBehaviour
    {
        public GuidedTutorial tutorial;
        public RectTransform card;
        [Tooltip("Anchored position (top-right anchor) the task card is held at.")]
        public Vector2 position=new Vector2(-238,-185);
        void LateUpdate(){if(!card)return;card.gameObject.SetActive(!tutorial||!tutorial.IsRunning);card.anchoredPosition=position;}
    }
}
