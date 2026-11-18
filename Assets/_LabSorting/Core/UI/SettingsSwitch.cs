using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    // The designer's settings toggle art has its silver (off) knob baked in on the left. "On" mirrors the
    // track so the knob sits right and covers it with the gold knob, so both states use the delivered sprites.
    public sealed class SettingsSwitch:MonoBehaviour
    {
        public RectTransform track;
        public GameObject onKnob;
        public bool On {get;private set;}
        public void Set(bool on)
        {
            On=on;
            if(track){var s=track.localScale;s.x=Mathf.Abs(s.x)*(on?-1:1);track.localScale=s;}
            if(onKnob&&onKnob.activeSelf!=on)onKnob.SetActive(on);
        }
    }
}
