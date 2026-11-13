using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class SortingRewardsHUD : MonoBehaviour
    {
        public SortingSession session;
        public bool showMagnetChips=true;
        public RectTransform safeArea;
        public UnityEngine.UI.Text milestone,toast,ability;
        public UnityEngine.UI.Image selectedSwatch;
        public RectTransform shortGoalFill;
        float goalCompleteUntil,shownGoalProgress;
        int goalOrdinal=-1;
        bool showingGoalCompletion;
        readonly Queue<string> announcements=new Queue<string>();
        readonly List<RectTransform> chips=new List<RectTransform>();
        readonly List<Tween> flights=new List<Tween>();
        Tween toastTween,abilityTween;
        int revision=-1;
        readonly HashSet<ItemDefinition> celebratedCategories=new HashSet<ItemDefinition>();
        public int PendingAnnouncements=>announcements.Count;
        void Announce(string message){if(announcements.Count>=3)announcements.Dequeue();announcements.Enqueue(message);}
        public int ActiveFlights { get {int count=0;foreach(var chip in chips)if(chip&&chip.gameObject.activeSelf)count++;return count;} }
        void OnEnable(){if(!session)return;session.Backgrounded+=ResetVisuals;session.ItemPlaced+=CategoryPlaced;session.MilestoneEarned+=Earned;session.SmallGoalEarned+=Earned;session.SearchStarted+=Search;session.MagnetCollected+=Magnet;session.TossScored+=Tossed;session.VisualsReset+=ResetVisuals;}
        void OnDisable(){if(session){session.Backgrounded-=ResetVisuals;session.ItemPlaced-=CategoryPlaced;session.MilestoneEarned-=Earned;session.SmallGoalEarned-=Earned;session.SearchStarted-=Search;session.MagnetCollected-=Magnet;session.TossScored-=Tossed;session.VisualsReset-=ResetVisuals;}ResetVisuals();}
        // Trick shot popup: streak count and bonus (no bonus when the item was already rewarded before).
        void Tossed(int streak,int bonus)
        {
            string text=streak>=3?"STREAK x"+streak+"!":"NICE SHOT!";if(bonus>0)text+="   +$"+bonus;
            Ability(text,streak>=3?new Color(1,.55f,.15f):new Color(1,.8f,.3f));
        }
        void Update()
        {
            // Modal screens and the tour own attention. Do not replay stale rewards
            // over gameplay when the player returns.
            if(session.player.blocked){ResetVisuals();return;}
            bool completing=Time.unscaledTime<goalCompleteUntil;
            if(revision!=session.Revision||completing!=showingGoalCompletion)
            {
                bool first=revision<0;revision=session.Revision;showingGoalCompletion=completing;
                milestone.text=completing?"✓ GOAL COMPLETE   +$"+SortingSession.ShortGoalBonus:session.ShortGoalsComplete?"SHORT GOALS COMPLETE":
                    "SHORT GOAL   "+session.ShortGoalProgress+" / "+session.ShortGoalTarget+"   |   +$"+SortingSession.ShortGoalBonus+"\n"+session.ShortGoalFocus();
                selectedSwatch.color=session.Active?session.Active.definition.color:new Color(.3f,.35f,.4f);
                if(first)shownGoalProgress=session.ShortGoalFraction;else if(!completing&&goalOrdinal!=session.ShortGoalOrdinal)shownGoalProgress=0;
                if(!completing)goalOrdinal=session.ShortGoalOrdinal;
            }
            if(shortGoalFill){shownGoalProgress=Mathf.MoveTowards(shownGoalProgress,completing?1:session.ShortGoalFraction,Time.unscaledDeltaTime*3);if(!Mathf.Approximately(shortGoalFill.anchorMax.x,shownGoalProgress))shortGoalFill.anchorMax=new Vector2(shownGoalProgress,1);}
            if(announcements.Count>0&&(toastTween==null||!toastTween.IsActive()))
            {
                toast.text=announcements.Dequeue();toast.color=new Color(.5f,1,.75f,1);toast.transform.localScale=Vector3.one*.94f;
                toastTween=DOTween.Sequence().Append(toast.transform.DOScale(1,.16f).SetEase(Ease.OutCubic)).AppendInterval(1.35f).Append(toast.DOFade(0,.25f)).SetUpdate(true);
            }
        }
        void Earned(string goal,int reward){if(goal=="Rack complete!"){Ability("✓ "+goal+(reward>0?"   +$"+reward:""),new Color(.5f,1,.75f));return;}if(goal=="Sorting goal complete!"){goalCompleteUntil=Time.unscaledTime+1.4f;return;}Announce(goal+(reward>0?"   +$"+reward:""));}
        void CategoryPlaced(SortItem item)
        {
            if(celebratedCategories.Contains(item.definition))return;
            foreach(var candidate in session.ItemsOfType(item.definition))if(candidate.location!=ItemLocation.Rack)return;
            celebratedCategories.Add(item.definition);Announce("✓ Category complete!  "+item.definition.displayName);
        }
        void Ability(string text,Color color)
        {
            abilityTween?.Kill();ability.text=text;ability.color=color;ability.transform.localScale=Vector3.one*.9f;
            abilityTween=DOTween.Sequence().Append(ability.transform.DOScale(1,.16f).SetEase(Ease.OutCubic)).AppendInterval(1.35f).Append(ability.DOFade(0,.25f)).SetUpdate(true);
        }
        void Search(ItemDefinition type,int count){Ability("SEARCH  "+type.displayName+"  |  "+count+" found",new Color(1,.9f,.35f));}
        void Magnet(ItemDefinition type,Vector3[] origins)
        {
            ClearFlights();Ability("MAGNET  +"+origins.Length+" "+type.displayName,new Color(.4f,1,.9f));
            if(!showMagnetChips)return;
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
        void ResetVisuals(){ClearFlights();announcements.Clear();toastTween?.Kill();abilityTween?.Kill();if(toast)toast.text="";if(ability)ability.text="";goalCompleteUntil=0;showingGoalCompletion=false;goalOrdinal=-1;revision=-1;}
    }
}

