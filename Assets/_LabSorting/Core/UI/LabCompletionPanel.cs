using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class LabCompletionPanel : MonoBehaviour
    {
        public SortingSession session;
        public GameObject root;
        public RectTransform card;
        public UnityEngine.UI.Text summary;
        public UnityEngine.UI.Text heading,replayHint,replayLabel,exploreLabel;
        public bool ConfirmingReplay {get;private set;}
        public UnityEngine.UI.Button continueButton;
        public RectTransform[] confetti;
        public bool autoShow=true;
        public bool IsOpen {get;private set;}
        bool pending,previousBlocked,previousVisible;
        bool paused;
        void OnApplicationPause(bool value){paused=value;}
        CursorLockMode previousCursor;
        float showAfter,confettiTime; Vector2[] confettiStarts;
        bool Complete=>session.items!=null&&session.items.Length>0&&session.HasMilestone("complete")&&session.SortedCount==session.items.Length;
        void OnEnable(){if(session){session.Changed+=Queue;session.VisualsReset+=ResetPanel;Queue();}}
        void Start(){root.SetActive(false);Queue();}
        void OnDisable(){if(session){session.Changed-=Queue;session.VisualsReset-=ResetPanel;}ResetPanel();}
        void Queue()
        {
            bool ready=Complete&&!session.CompletionAcknowledged;
            if(!ready){pending=false;if(IsOpen&&!Complete)Close();return;}
            if(!pending){pending=true;showAfter=Time.unscaledTime+.35f;}
        }
        void Update()
        {
            if(IsOpen){TickConfetti();if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)ContinueExploring();return;}
            if(!autoShow||!pending||session.player.blocked||Time.unscaledTime<showAfter)return;
            foreach(var item in session.items)if(item.Moving)return;
            Show();
        }
        void Show()
        {
            LabSorting.Services.LabTelemetry.Event("lab_completion_viewed");
            if(!Complete||session.CompletionAcknowledged)return;
            pending=false;previousBlocked=session.player.blocked;previousCursor=Cursor.lockState;previousVisible=Cursor.visible;
            session.CancelHold();session.player.SetBlocked(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;IsOpen=true;root.SetActive(true);root.transform.SetAsLastSibling();
            ConfirmingReplay=false;RefreshSummary();
            card.DOKill();card.localScale=Vector3.one*.94f;card.DOScale(1,.25f).SetEase(Ease.OutBack).SetUpdate(true);
            confettiTime=0;
            var random=new System.Random(73);confettiStarts=new Vector2[confetti.Length];
            for(int i=0;i<confetti.Length;i++){var piece=confetti[i];piece.SetAsLastSibling();piece.DOKill();piece.gameObject.SetActive(true);confettiStarts[i]=new Vector2((float)random.NextDouble()*540-270,175);piece.anchoredPosition=confettiStarts[i];piece.localRotation=Quaternion.identity;piece.localScale=Vector3.one;}
        }
        void TickConfetti()
        {
            if(confettiStarts==null)return;confettiTime+=Mathf.Min(Time.unscaledDeltaTime,.05f);
            for(int i=0;i<confetti.Length;i++){var piece=confetti[i];float t=Mathf.Clamp01((confettiTime-i*.008f)/1.6f);piece.anchoredPosition=confettiStarts[i]+new Vector2(Mathf.Sin(i)*55*t,-330*t*t);piece.localRotation=Quaternion.Euler(0,0,(i%2==0?1:-1)*360*t);piece.localScale=Vector3.one*Mathf.Lerp(1,.4f,t);if(t>=1)piece.gameObject.SetActive(false);}
        }
        void RefreshSummary()
        {
            if(heading)heading.text=ConfirmingReplay?"PLAY AGAIN?":"LAB COMPLETE!";
            summary.text=ConfirmingReplay?"Start a fresh sorting run?\n\nItems return to the floor.\nRacks and run progress reset.":session.SortedCount+" / "+session.items.Length+" ITEMS SORTED\n\n"+session.GetItemProgress().Count+" categories organized\n"+session.CompletedRackCount+" racks completed\nYou've sorted every item in this lab!";
            if(replayHint)replayHint.text="Cash, tools, capacity upgrades and\ncompletion history stay saved.";
            if(replayLabel)replayLabel.text=ConfirmingReplay?"START NEW RUN":"PLAY AGAIN";
            if(exploreLabel)exploreLabel.text=ConfirmingReplay?"BACK":"KEEP EXPLORING";
        }
        public void ContinueExploring(){if(!IsOpen||paused||session.player.NetworkBlocked)return;if(ConfirmingReplay){ConfirmingReplay=false;RefreshSummary();return;}session.AcknowledgeCompletion();Close();}
        public void PlayAgain(){if(!IsOpen||paused||session.player.NetworkBlocked)return;if(!ConfirmingReplay){ConfirmingReplay=true;KillEffects();RefreshSummary();return;}if(session.ReplayCompletedArea())Close();else{if(!IsOpen)Show();ConfirmingReplay=true;KillEffects();RefreshSummary();summary.text="Could not save the new run.\nYour completed lab is unchanged.\nPlease try again.";}}
        void Close(){if(!IsOpen)return;IsOpen=false;root.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=previousCursor;Cursor.visible=previousVisible;KillEffects();}
        void KillEffects(){if(card)card.DOKill();if(confetti!=null)foreach(var piece in confetti)if(piece){piece.DOKill();piece.gameObject.SetActive(false);}}
        void ResetPanel(){pending=false;Close();KillEffects();}
    }
}
