using UnityEngine;
using UnityEngine.Events;
namespace LabSorting.UI
{
    public sealed class AnimatedPanelClose:MonoBehaviour
    {
        public DesignerPanelMotion panel;
        public UnityEvent completed=new UnityEvent();
        public void Close(){var button=GetComponent<UnityEngine.UI.Button>();if(button&&!button.IsInteractable())return;if(panel)panel.CloseAnimated(()=>completed.Invoke());else completed.Invoke();}
    }
}
