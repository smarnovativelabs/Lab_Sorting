using UnityEngine;
using LabSorting.Services;
namespace LabSorting.UI
{
    // Center toast for messages that matter (errors, rewards, purchases, saves). It rests just
    // above the crosshair, slides down into place and lifts away while fading. Routine
    // pick/place/select feedback is left to the rest of the HUD so the aim point stays clear.
    [DefaultExecutionOrder(10010)]
    public sealed class DesignerToasts:MonoBehaviour
    {
        public SortingHUD hud;
        public CanvasGroup toast;
        public GameObject adPrompt;
        public UnityEngine.UI.Text adText;
        [Tooltip("HUD pieces sharing the toast's column that it must stay below (task card, rack direction, ad prompt).")]
        public RectTransform[] keepBelow;
        [Tooltip("The toast never drops onto this (the crosshair).")]
        public RectTransform floor;
        public float gap=6;
        [Tooltip("How far above its resting spot the toast starts sliding from (canvas units).")]
        public float slideDistance=26;
        [Tooltip("Seconds the slide-in takes; leaving is slightly quicker.")]
        public float showDuration=.24f;
        [Header("Content-sized card")]
        [Tooltip("Card width follows the message: text width + padding, clamped to this range. 0 = keep the authored width.")]
        public float minWidth=200,maxWidth=440;
        public float padding=48;
        [Tooltip("Shown at the card's left edge for warnings and errors.")]
        public GameObject alertBadge;
        [Tooltip("Shown at the card's left edge when the message reports something gained (ToastTone.Success).")]
        public GameObject successBadge;
        readonly ToastPriority messages=new ToastPriority();
        readonly Vector3[] corners=new Vector3[4];
        string lastSubmitted,lastError,shownText="";
        int lastFullNotice=-1;
        RectTransform card;Vector2 rest;float restY,show,pop;
        public string ShownText=>shownText;
        void Awake()
        {
            if(!toast)return;card=(RectTransform)toast.transform;rest=card.anchoredPosition;restY=rest.y;
            if((keepBelow==null||keepBelow.Length==0)&&card.parent)
            {
                var found=new System.Collections.Generic.List<RectTransform>();
                foreach(var name in new[]{"MockupTaskCard","MatchingRackDirection","DesignerAdPrompt"}){var t=card.parent.Find(name);if(t)found.Add((RectTransform)t);}
                keepBelow=found.ToArray();
            }
            if(!floor&&card.parent){var t=card.parent.Find("Crosshair");if(t)floor=(RectTransform)t;}
            toast.alpha=0;card.anchoredPosition=rest+Vector2.up*slideDistance;
        }
        public void Present(string text,bool error,int fullNoticeVersion=-1)
        {
            if(text==lastSubmitted&&fullNoticeVersion==lastFullNotice)return;
            lastSubmitted=text;lastFullNotice=fullNoticeVersion;
            if(error&&text==shownText)pop=1;
            if(!error&&ToastPriority.IsRoutine(text))
            {
                // The player just acted successfully, so a complaint like "Aim at a rack" is stale.
                if(lastError!=null&&messages.Text==lastError)messages.Supersede(lastError,Time.unscaledTime);
                return;
            }
            int priority=ToastPriority.Classify(text,error);if(error)lastError=text;
            messages.Push(text,priority,Time.unscaledTime,error&&priority<3?2.5f:priority>0?4:2.5f,error);
        }
        Rect LocalRect(RectTransform space,RectTransform r)
        {
            r.GetWorldCorners(corners);Vector2 a=space.InverseTransformPoint(corners[0]),b=space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        }
        // Authored spot, lowered only as far as needed to clear whatever is visible above it,
        // but never onto the crosshair.
        public Vector2 SettledPosition()
        {
            var parent=card?card.parent as RectTransform:null;if(!parent)return rest;
            Vector2 offset=(Vector2)card.localPosition-card.anchoredPosition;
            float half=card.rect.height*.5f,halfWidth=card.rect.width*.5f,x=rest.x+offset.x,y=rest.y+offset.y;
            float floorTop=floor&&floor.gameObject.activeInHierarchy?LocalRect(parent,floor).yMax:float.NegativeInfinity;
            float top=float.PositiveInfinity;
            if(keepBelow!=null)foreach(var blocker in keepBelow)
            {
                if(!blocker||!blocker.gameObject.activeInHierarchy)continue;var r=LocalRect(parent,blocker);
                if(r.xMax<=x-halfWidth||r.xMin>=x+halfWidth||r.yMin<=floorTop)continue;top=Mathf.Min(top,r.yMin);
            }
            float target=Mathf.Max(Mathf.Min(y,top-gap-half),floorTop+gap+half);
            return new Vector2(rest.x,target-offset.y);
        }
        // A fixed 520-wide bar made "Rack full" a long empty strip; size the card to its message instead.
        void FitCard(bool alert)
        {
            bool success=!alert&&ToastTone.Of(shownText)==ToastTone.Tone.Success;
            if(alertBadge&&alertBadge.activeSelf!=alert)alertBadge.SetActive(alert);
            if(successBadge&&successBadge.activeSelf!=success)successBadge.SetActive(success);
            if(!card||maxWidth<=0)return;
            float badge=(alert&&alertBadge)||(success&&successBadge)?30:0;
            float width=Mathf.Clamp(TextFit.Width(hud.status,shownText,TextFit.MaxSize(hud.status))+padding+badge,minWidth,maxWidth);
            card.sizeDelta=new Vector2(width,card.sizeDelta.y);
            var text=hud.status.rectTransform;
            if(Mathf.Approximately(text.anchorMin.x,text.anchorMax.x)){text.sizeDelta=new Vector2(width-28-badge,text.sizeDelta.y);text.anchoredPosition=new Vector2(badge*.5f,text.anchoredPosition.y);}
        }
        static float OutBack(float t){const float c=1.4f;t-=1;return 1+(c+1)*t*t*t+c*t*t;}
        void LateUpdate()
        {
            messages.Tick(Time.unscaledTime);
            bool has=!string.IsNullOrEmpty(messages.Text);
            if(has&&messages.Text!=shownText)
            {
                // A replacement while already on screen gets a small bump instead of a full re-entry.
                if(show>.5f)pop=1;shownText=messages.Text;hud.status.text=shownText;
                bool alert=messages.Priority>=2;
                hud.status.color=alert?new Color(1,.85f,.45f):Color.white;
                // Badges follow what the message says (priority also flags good news like rewards).
                var tone=ToastTone.Of(shownText);FitCard(shownText==lastError||tone==ToastTone.Tone.Alert);
            }
            float step=Time.unscaledDeltaTime/Mathf.Max(.01f,showDuration);
            show=Mathf.MoveTowards(show,has?1:0,has?step:step*1.3f);
            if(!has&&show<=0&&shownText.Length>0){shownText="";hud.status.text="";}
            pop=Mathf.MoveTowards(pop,0,Time.unscaledDeltaTime/.2f);
            if(toast)
            {
                // Glide (not jump) if something above appears or hides while the toast is up.
                float settled=SettledPosition().y;restY=show<=0?settled:Mathf.MoveTowards(restY,settled,Time.unscaledDeltaTime*240);
                toast.alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(show*1.5f));
                card.anchoredPosition=new Vector2(rest.x,restY+slideDistance*(1-OutBack(show)));
                card.localScale=Vector3.one*(Mathf.Lerp(.94f,1,show)*(1+.05f*Mathf.Sin(pop*Mathf.PI)));
            }
            var commerce=LabServiceRoot.Instance?LabServiceRoot.Instance.commerce:null;float remaining=commerce?commerce.AdCountdownSeconds:0;
            adPrompt.SetActive(remaining>0);if(remaining>0){adPrompt.transform.SetAsLastSibling();adText.text=commerce.RewardCountdown?LabLocalization.Text("Preparing video..."):LabLocalization.Text("Displaying Ad In:")+" "+Mathf.CeilToInt(remaining)+" "+LabLocalization.Text("Seconds");adText.transform.localScale=Vector3.one*(1+.025f*Mathf.Sin(remaining*Mathf.PI*2));}else adText.transform.localScale=Vector3.one;
        }
    }
}
