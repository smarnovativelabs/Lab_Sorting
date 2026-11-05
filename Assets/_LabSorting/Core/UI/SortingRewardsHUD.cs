using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class SortingRewardsHUD : MonoBehaviour
    {
        public SortingSession session;
        public RectTransform safeArea;
        public UnityEngine.UI.Text milestone,toast,ability;
        public UnityEngine.UI.Image selectedSwatch;
        readonly Queue<string> announcements=new Queue<string>();
        readonly List<RectTransform> chips=new List<RectTransform>();
        readonly List<Tween> flights=new List<Tween>();
        Tween toastTween,abilityTween,rackTween;
        int revision=-1;
        public int ActiveFlights { get {int count=0;foreach(var chip in chips)if(chip&&chip.gameObject.activeSelf)count++;return count;} }
        void OnEnable(){if(!session)return;session.MilestoneEarned+=Earned;session.SmallGoalEarned+=Earned;session.RackCompleted+=CelebrateRack;session.SearchStarted+=Search;session.MagnetCollected+=Magnet;session.VisualsReset+=ResetVisuals;}
        void OnDisable(){if(session){session.MilestoneEarned-=Earned;session.SmallGoalEarned-=Earned;session.RackCompleted-=CelebrateRack;session.SearchStarted-=Search;session.MagnetCollected-=Magnet;session.VisualsReset-=ResetVisuals;}ResetVisuals();}
        void Update()
        {
            if(revision!=session.Revision){revision=session.Revision;milestone.text=session.ShortGoalText+"\n"+session.NextMilestoneText;selectedSwatch.color=session.Active?session.Active.definition.color:new Color(.3f,.35f,.4f);}
            if(announcements.Count>0&&(toastTween==null||!toastTween.IsActive()))
            {
                toast.text=announcements.Dequeue();toast.color=new Color(.5f,1,.75f,1);toast.transform.localScale=Vector3.one*.94f;
                toastTween=DOTween.Sequence().Append(toast.transform.DOScale(1,.18f).SetEase(Ease.OutCubic)).AppendInterval(2.5f).Append(toast.DOFade(0,.3f)).SetUpdate(true);
            }
        }
        void CelebrateRack(SortRack rack){rackTween?.Kill(true);if(!rack.label)return;var label=rack.label.transform;var scale=label.localScale;rackTween=DOTween.Sequence().AppendInterval(.26f).AppendCallback(()=>{var feedback=session.GetComponent<SortingFeedback>();if(feedback)feedback.Play(SortingCue.RackComplete);}).Append(label.DOScale(scale*1.08f,.16f)).Append(label.DOScale(scale,.24f)).SetUpdate(true).OnKill(()=>{if(label)label.localScale=scale;});}
        void Earned(string goal,int reward){announcements.Enqueue(goal+"   +$"+reward);}
        void Ability(string text,Color color)
        {
            abilityTween?.Kill();ability.text=text;ability.color=color;ability.transform.localScale=Vector3.one*.9f;
            abilityTween=DOTween.Sequence().Append(ability.transform.DOScale(1,.2f).SetEase(Ease.OutCubic)).AppendInterval(2).Append(ability.DOFade(0,.4f)).SetUpdate(true);
        }
        void Search(ItemDefinition type,int count){Ability("SEARCH  "+type.displayName+"  |  "+count+" found",new Color(1,.9f,.35f));}
        void Magnet(ItemDefinition type,Vector3[] origins)
        {
            ClearFlights();Ability("MAGNET  +"+origins.Length+" "+type.displayName,new Color(.4f,1,.9f));
            var canvas=safeArea.GetComponentInParent<Canvas>();var uiCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            int count=Mathf.Min(origins.Length,30);
            for(int i=0;i<count;i++)
            {
                if(chips.Count<=i)
                {
                    var go=new GameObject("MagnetVisual",typeof(RectTransform),typeof(UnityEngine.UI.Image));var r=(RectTransform)go.transform;r.SetParent(safeArea,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(22,22);var img=go.GetComponent<UnityEngine.UI.Image>();img.raycastTarget=false;chips.Add(r);
                }
                var chip=chips[i];chip.gameObject.SetActive(true);chip.GetComponent<UnityEngine.UI.Image>().color=type.color;chip.localScale=Vector3.one;
                var viewport=session.player.view.WorldToViewportPoint(origins[i]);if(viewport.z<0){viewport.x=1-viewport.x;viewport.y=.5f;}
                var screen=new Vector2(Mathf.Clamp(viewport.x,.08f,.92f)*Screen.width,Mathf.Clamp(viewport.y,.15f,.85f)*Screen.height);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea,screen,uiCamera,out var start);chip.anchoredPosition=start;
                Vector2 end=(Vector2)safeArea.InverseTransformPoint(selectedSwatch.transform.position);
                var motion=DOTween.Sequence().AppendInterval(i*.045f).Append(chip.DOAnchorPos(end,.42f).SetEase(Ease.InOutSine)).Join(chip.DOScale(.35f,.42f)).SetUpdate(true).OnComplete(()=>chip.gameObject.SetActive(false));flights.Add(motion);
            }
        }
        void ClearFlights(){foreach(var flight in flights)flight?.Kill();flights.Clear();foreach(var chip in chips)if(chip)chip.gameObject.SetActive(false);}
        void ResetVisuals(){rackTween?.Kill();ClearFlights();announcements.Clear();toastTween?.Kill();abilityTween?.Kill();if(toast)toast.text="";if(ability)ability.text="";revision=-1;}
    }
}

