using UnityEngine;
namespace LabSorting.UI
{
    public sealed class TaskCardLayout:MonoBehaviour
    {
        public GuidedTutorial tutorial;
        public RectTransform card;
        void LateUpdate(){if(!card)return;card.gameObject.SetActive(!tutorial||!tutorial.IsRunning);card.anchoredPosition=new Vector2(-238,-185);}
    }
}
