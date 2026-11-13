using UnityEngine;
using LabSorting.Services;
namespace LabSorting.UI
{
    [DefaultExecutionOrder(10010)]
    public sealed class DesignerToasts:MonoBehaviour
    {
        public SortingHUD hud;
        public CanvasGroup toast;
        public GameObject adPrompt;
        public UnityEngine.UI.Text adText;
        readonly ToastPriority messages=new ToastPriority();
        string lastSubmitted;
        public void Present(string text,bool error){if(text==lastSubmitted)return;lastSubmitted=text;int priority=ToastPriority.Classify(text,error);messages.Push(text,priority,Time.unscaledTime,priority>0?4:2.5f);}
        void LateUpdate()
        {
            messages.Tick(Time.unscaledTime);hud.status.text=messages.Text;hud.status.color=messages.Priority>=2?new Color(1,.85f,.45f):Color.white;
            toast.alpha=Mathf.MoveTowards(toast.alpha,!string.IsNullOrEmpty(messages.Text)?1:0,Time.unscaledDeltaTime*6);
            var commerce=LabServiceRoot.Instance?LabServiceRoot.Instance.commerce:null;float remaining=commerce?commerce.AdCountdownSeconds:0;
            adPrompt.SetActive(remaining>0);if(remaining>0){adPrompt.transform.SetAsLastSibling();adText.text=commerce.RewardCountdown?LabLocalization.Text("Preparing video..."):LabLocalization.Text("Displaying Ad In:")+" "+Mathf.CeilToInt(remaining)+" "+LabLocalization.Text("Seconds");adText.transform.localScale=Vector3.one*(1+.025f*Mathf.Sin(remaining*Mathf.PI*2));}else adText.transform.localScale=Vector3.one;
        }
    }
}
