using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class ItemsProgressPanel : MonoBehaviour
    {
        public SortingSession session;
        public SortingFeedback feedback;
        public GameObject root;
        public CanvasGroup group;
        public RectTransform card;
        public Text summary,pageLabel,soundLabel,hapticsLabel;
        public Text heading;
        public Text[] rows;
        public Text[] rowCounts;
        public UnityEngine.UI.Image[] rowIcons;
        public Button openButton,closeButton,previousButton,nextButton;
        public bool IsOpen {get;private set;}
        int page,revision=-1;
        bool previousBlocked;
        CursorLockMode previousCursor;bool previousCursorVisible;
        Tween fade,scale;
        void Start() {root.SetActive(false);}
        void Update()
        {
            var key=Keyboard.current;
            if(key!=null&&(key.iKey.wasPressedThisFrame||(IsOpen&&key.escapeKey.wasPressedThisFrame))) Toggle();
            if(IsOpen && revision!=session.Revision) Refresh();
        }
        public void Toggle() {if(IsOpen) Close();else Open();}
        public void Open()
        {
            var completion=GetComponent<LabCompletionPanel>();if(completion&&completion.IsOpen)return;
            var settings=GetComponent<SettingsTutorialPanel>();if(settings&&settings.IsOpen)return;
            if(session.player.TutorialBlocked||IsOpen) return;session.CancelHold();previousBlocked=session.player.blocked;session.player.SetBlocked(true);IsOpen=true;
            previousCursor=Cursor.lockState;previousCursorVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            root.SetActive(true);root.transform.SetAsLastSibling();page=0;Refresh();
            fade?.Kill();scale?.Kill();group.alpha=0;card.localScale=Vector3.one*.97f;
            fade=group.DOFade(1,.14f).SetUpdate(true);scale=card.DOScale(1,.16f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
        public void Close()
        {
            if(!IsOpen) return;fade?.Kill();scale?.Kill();IsOpen=false;root.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=previousCursor;Cursor.visible=previousCursorVisible;
        }
        public void Previous() {page--;Refresh();}
        public void Next() {page++;Refresh();}
        public void Refresh()
        {
            var data=session.GetItemProgress();
            int remaining=session.items.Length-session.SortedCount;
            bool finishing=remaining>0&&remaining<=SortingSession.CleanupThreshold;
            // Stable ordering within each group; completed types cannot bury the last items.
            if(finishing){var pending=data.FindAll(p=>p.Remaining>0);pending.AddRange(data.FindAll(p=>p.Remaining==0));data=pending;}
            int pages=Mathf.Max(1,Mathf.CeilToInt(data.Count/(float)rows.Length));page=Mathf.Clamp(page,0,pages-1);
            if(heading)heading.text=finishing?"ALMOST DONE!":"ALL ITEMS";
            summary.text=(heading?"":"ALL ITEMS\n")+"Left to sort: "+(session.items.Length-session.SortedCount)+" of "+session.items.Length;
            for(int i=0;i<rows.Length;i++)
            {
                int index=page*rows.Length+i;rows[i].transform.parent.gameObject.SetActive(index<data.Count);
                if(index>=data.Count) continue;var row=data[index];
                if(rowCounts!=null&&i<rowCounts.Length&&rowCounts[i])rowCounts[i].color=finishing&&row.Remaining>0?new Color(.45f,1,1):new Color(1,.88f,.2f);
                if(rowIcons!=null&&i<rowIcons.Length&&rowIcons[i]){rowIcons[i].sprite=row.type.icon;rowIcons[i].enabled=row.type.icon;}
                if(rowCounts!=null&&i<rowCounts.Length&&rowCounts[i]){rows[i].text=row.type.displayName;rowCounts[i].text=row.Remaining+" / "+row.total;}
                else rows[i].text=row.type.displayName+"     "+row.Remaining+" / "+row.total;
            }
            pageLabel.text=(page+1)+" / "+pages;previousButton.interactable=page>0;nextButton.interactable=page<pages-1;revision=session.Revision;
        }
        void OnDisable() {if(IsOpen) Close();fade?.Kill();scale?.Kill();}
    }
}


