using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LabSorting.UI
{
    // Scale the button uniformly without moving its anchors or changing layout sizes.
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Button button;
        Vector3 rest=Vector3.one;
        bool pressed;
        public void SetRestScale(Vector3 value){rest=value;transform.localScale=rest*(pressed?.96f:1f);}
        void Awake(){button=GetComponent<Button>();rest=transform.localScale;}
        void OnEnable(){pressed=false;}
        public void OnPointerDown(PointerEventData e){pressed=button&&button.IsInteractable();}
        public void OnPointerUp(PointerEventData e){pressed=false;}
        public void OnPointerExit(PointerEventData e){pressed=false;}
        void Update()
        {
            if(!button||!button.IsInteractable())pressed=false;
            var target=rest*(pressed?.96f:1f);
            if((transform.localScale-target).sqrMagnitude<.000001f){transform.localScale=target;return;}
            transform.localScale=Vector3.Lerp(transform.localScale,target,1f-Mathf.Exp(-24f*Time.unscaledDeltaTime));
        }
        void OnDisable(){pressed=false;transform.localScale=rest;}
    }
}
