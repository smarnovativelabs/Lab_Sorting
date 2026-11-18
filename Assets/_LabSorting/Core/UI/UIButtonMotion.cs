using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LabSorting.UI
{
    // Scale the button uniformly without moving its anchors or changing layout sizes. This is the only writer of
    // the button's scale: press depth, release pop and the idle pulse all multiply the rest scale, so the
    // player's control-size setting (SetRestScale) is always preserved.
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const float PressScale=.92f,PopAmount=.045f,PopDuration=.16f,PulseAmount=.06f,PulsePeriod=2f;
        Button button;
        Vector3 rest=Vector3.one;
        bool pressed;
        float releasedAt=-1;
        [Tooltip("Gentle breathing to invite a tap (e.g. the + offers button when no uses are left).")]
        public bool idlePulse;
        public void SetRestScale(Vector3 value){rest=value;transform.localScale=rest*(pressed?PressScale:1f);}
        void Awake(){button=GetComponent<Button>();rest=transform.localScale;}
        void OnEnable(){pressed=false;releasedAt=-1;}
        public void OnPointerDown(PointerEventData e){pressed=button&&button.IsInteractable();releasedAt=-1;}
        // A completed tap pops slightly past rest and settles back within PopDuration; sliding off just releases.
        public void OnPointerUp(PointerEventData e){if(pressed)releasedAt=Time.unscaledTime;pressed=false;}
        public void OnPointerExit(PointerEventData e){pressed=false;releasedAt=-1;}
        void Update()
        {
            bool interactable=button&&button.IsInteractable();
            if(!interactable)pressed=false;
            if(releasedAt>=0)
            {
                float t=(Time.unscaledTime-releasedAt)/PopDuration;
                if(t<1){transform.localScale=rest*(1+PopAmount*Mathf.Sin(t*Mathf.PI));return;}
                releasedAt=-1;
            }
            if(idlePulse&&interactable&&!pressed){transform.localScale=rest*(1+PulseAmount*(.5f-.5f*Mathf.Cos(Time.unscaledTime*2*Mathf.PI/PulsePeriod)));return;}
            var target=rest*(pressed?PressScale:1f);
            if((transform.localScale-target).sqrMagnitude<.000001f){transform.localScale=target;return;}
            transform.localScale=Vector3.Lerp(transform.localScale,target,1f-Mathf.Exp(-24f*Time.unscaledDeltaTime));
        }
        void OnDisable(){pressed=false;releasedAt=-1;transform.localScale=rest;}
    }
}
