using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using LabSorting.Sorting;

namespace LabSorting.UI
{
    // Presentation only. The session commits and saves placements immediately; this display catches up on merge.
    [DisallowMultipleComponent]
    public sealed class SortedProgressFeedback : MonoBehaviour
    {
        sealed class Pending { public SortItem item; public bool arrived; }
        readonly Dictionary<SortItem,Pending> pending=new Dictionary<SortItem,Pending>();
        readonly List<Pending> flying=new List<Pending>();
        SortingHUD hud;
        SortingSession session;
        UnityEngine.UI.Text increment;
        Tween flight,pulse,fill;
        Vector3 originalScale;
        Color originalColor;
        bool subscribed,needsSync,wasBlocked;
        int rendered=-1;
        public int DisplayedCount {get;private set;}
        public int PendingCount=>pending.Count;
        public int ActiveAmount {get;private set;}
        public int Merges {get;private set;}
        public int LargestIncrement {get;private set;}
        public bool IsAnimating=>flight!=null&&flight.IsActive();
        public RectTransform IncrementRect=>increment?increment.rectTransform:null;

        public void Bind(SortingHUD owner)
        {
            if(hud==owner&&subscribed)return;
            Unsubscribe();hud=owner;session=owner.session;
            originalScale=hud.progress.transform.localScale;originalColor=hud.progress.color;
            if(isActiveAndEnabled){Subscribe();Synchronize();}
        }
        void Subscribe()
        {
            if(!session||subscribed)return;subscribed=true;
            session.ItemPlaced+=Reserved;session.ItemPlacementArrived+=Arrived;
            session.Changed+=Changed;session.VisualsReset+=ResetBeforeRestore;session.Backgrounded+=Synchronize;
        }
        void Unsubscribe()
        {
            if(!session||!subscribed)return;subscribed=false;
            session.ItemPlaced-=Reserved;session.ItemPlacementArrived-=Arrived;
            session.Changed-=Changed;session.VisualsReset-=ResetBeforeRestore;session.Backgrounded-=Synchronize;
        }
        void OnEnable(){if(hud){Subscribe();Synchronize();}}
        void OnDisable(){Unsubscribe();if(hud)Synchronize();}
        void OnApplicationPause(bool paused){if(paused&&hud)Synchronize();}
        void OnApplicationFocus(bool focused){if(!focused&&hud)Synchronize();}
        void Reserved(SortItem item)
        {
            if(!item||session.player.blocked)return;
            pending[item]=new Pending{item=item};
        }
        void Arrived(SortItem item)
        {
            if(item&&pending.TryGetValue(item,out var value))value.arrived=true;
        }
        void Changed()
        {
            if(needsSync||session.player.blocked){Synchronize();return;}
            foreach(var pair in pending)
                if(!pair.Key||pair.Key.location!=ItemLocation.Rack){Synchronize();return;}
            // Picking up a previously sorted item decrements immediately. Reservations remain visually pending.
            SetCount(Mathf.Max(0,session.SortedCount-pending.Count),false);
        }
        void ResetBeforeRestore(){CancelVisuals();pending.Clear();needsSync=true;}
        public void Synchronize()
        {
            CancelVisuals();pending.Clear();needsSync=false;
            if(session)SetCount(session.SortedCount,true);
        }
        void CancelVisuals()
        {
            flight?.Kill();flight=null;pulse?.Kill();pulse=null;fill?.Kill();fill=null;
            flying.Clear();ActiveAmount=0;
            if(increment)increment.gameObject.SetActive(false);
            if(hud&&hud.progress){hud.progress.transform.localScale=originalScale;hud.progress.color=originalColor;}
        }
        void SetCount(int value,bool snap)
        {
            DisplayedCount=Mathf.Clamp(value,0,session.items.Length);
            if(rendered==DisplayedCount&&!snap)return;
            rendered=DisplayedCount;hud.RenderSortedProgress(DisplayedCount);
            if(!hud.progressFill)return;
            float fraction=session.items.Length>0?(float)DisplayedCount/session.items.Length:0;
            var bar=hud.progressFill;fill?.Kill();bar.gameObject.SetActive(fraction>0);
            if(snap)bar.anchorMax=new Vector2(fraction,bar.anchorMax.y);
            else fill=DOTween.To(()=>bar.anchorMax.x,x=>bar.anchorMax=new Vector2(x,bar.anchorMax.y),fraction,.2f).SetUpdate(true).SetEase(Ease.OutCubic);
        }
        void LateUpdate()
        {
            if(!hud||!session)return;
            bool blocked=session.player.blocked;
            if(needsSync||(blocked&&!wasBlocked))Synchronize();
            wasBlocked=blocked;
            if(blocked||IsAnimating)return;
            flying.Clear();
            foreach(var value in pending.Values)if(value.arrived)flying.Add(value);
            if(flying.Count>0)Launch();
        }
        Vector3 NumberPosition()
        {
            var text=hud.progress;var rect=text.rectTransform;
            int start=text.text.IndexOf(hud.compact?"SORTED  ":"Sorted ",System.StringComparison.Ordinal);
            if(start<0)return rect.position;
            start+=hud.compact?8:7;
            string count=hud.compact?UIFormat.Count(DisplayedCount):DisplayedCount.ToString();
            text.cachedTextGenerator.Populate(text.text,text.GetGenerationSettings(rect.rect.size));
            var characters=text.cachedTextGenerator.characters;
            int last=start+count.Length-1;
            if(start>=characters.Count||last>=characters.Count)return rect.position;
            var a=characters[start];var b=characters[last];
            float x=(a.cursorPos.x+b.cursorPos.x+b.charWidth)*.5f/text.pixelsPerUnit;
            // The compact title is vertically centred. Aim at its digits, not the centre of the whole badge.
            return rect.TransformPoint(new Vector3(x,rect.rect.center.y,0));
        }
        void Launch()
        {
            if(!increment)
            {
                var go=new GameObject("SortedCountIncrement",typeof(RectTransform),typeof(UnityEngine.UI.Text),typeof(UnityEngine.UI.Outline));
                go.layer=hud.progress.gameObject.layer;go.transform.SetParent(hud.progress.transform.parent,false);
                increment=go.GetComponent<UnityEngine.UI.Text>();increment.font=hud.progress.font;
                increment.fontSize=25;increment.alignment=TextAnchor.MiddleCenter;increment.raycastTarget=false;
                increment.horizontalOverflow=HorizontalWrapMode.Overflow;increment.verticalOverflow=VerticalWrapMode.Overflow;
                increment.rectTransform.sizeDelta=new Vector2(110,38);
                var border=go.GetComponent<UnityEngine.UI.Outline>();border.effectColor=new Color(.015f,.045f,.10f,.95f);border.effectDistance=new Vector2(1,-1);
            }
            ActiveAmount=flying.Count;LargestIncrement=Mathf.Max(LargestIncrement,ActiveAmount);
            increment.text="+"+ActiveAmount;increment.color=new Color(1,.83f,.25f,1);
            var r=increment.rectTransform;r.SetAsLastSibling();r.gameObject.SetActive(true);
            Vector3 target=r.parent.InverseTransformPoint(NumberPosition());
            r.localPosition=target+Vector3.down*46;r.localScale=Vector3.one*.85f;
            flight=DOTween.Sequence().SetUpdate(true)
                .Append(r.DOLocalMove(target,.42f).SetEase(Ease.InOutSine))
                .Insert(0,r.DOScale(1,.09f).SetEase(Ease.OutBack))
                .Insert(.2f,r.DOScale(.45f,.22f).SetEase(Ease.InQuad))
                .Insert(.28f,increment.DOFade(0,.14f))
                .OnComplete(Merge);
        }
        void Merge()
        {
            foreach(var value in flying)
                if(value.item&&pending.TryGetValue(value.item,out var current)&&current==value)pending.Remove(value.item);
            flying.Clear();flight=null;ActiveAmount=0;increment.gameObject.SetActive(false);
            SetCount(session.SortedCount-pending.Count,false);Merges++;
            pulse?.Kill();hud.progress.transform.localScale=originalScale;hud.progress.color=originalColor;
            pulse=DOTween.Sequence().SetUpdate(true)
                .Append(hud.progress.transform.DOScale(originalScale*1.055f,.08f).SetEase(Ease.OutQuad))
                .Join(hud.progress.DOColor(new Color(1,.87f,.4f,originalColor.a),.08f))
                .Append(hud.progress.transform.DOScale(originalScale,.14f).SetEase(Ease.OutSine))
                .Join(hud.progress.DOColor(originalColor,.14f));
        }
        void OnDestroy(){CancelVisuals();if(increment)Destroy(increment.gameObject);}
    }
}
