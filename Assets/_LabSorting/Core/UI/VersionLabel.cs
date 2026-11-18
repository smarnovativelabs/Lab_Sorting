using UnityEngine;
using UnityEngine.EventSystems;
namespace LabSorting.UI
{
    // Shows the build's version (Player Settings > Version) so support questions can name the build.
    // Development builds only: five quick taps open the Store & Privacy panel, which no longer has a settings
    // button but still holds developer tools (the MAX ad debugger).
    [RequireComponent(typeof(UnityEngine.UI.Text))]
    public sealed class VersionLabel:MonoBehaviour,IPointerClickHandler
    {
        public ServicesPanel developerPanel;
        int taps;float lastTap;
        void Awake(){var text=GetComponent<UnityEngine.UI.Text>();text.text="v"+Application.version;text.raycastTarget=Debug.isDebugBuild&&developerPanel;}
        public void OnPointerClick(PointerEventData e)
        {
            if(!Debug.isDebugBuild||!developerPanel)return;
            taps=Time.unscaledTime-lastTap<.6f?taps+1:1;lastTap=Time.unscaledTime;
            if(taps>=5){taps=0;developerPanel.Open();}
        }
    }
}
