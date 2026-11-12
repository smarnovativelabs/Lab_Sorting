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
        public UnityEngine.UI.Button continueButton;
        public RectTransform[] confetti;
        public bool autoShow=true;
        public bool IsOpen {get;private set;}
        bool pending,previousBlocked,previousVisible;
        CursorLockMode previousCursor;
        float showAfter;
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
            if(IsOpen){if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)ContinueExploring();return;}
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
            summary.text=session.SortedCount+" / "+session.items.Length+" ITEMS SORTED\n\n"+session.GetItemProgress().Count+" categories organized\n"+session.MilestoneCount+" milestones unlocked\nCash balance  $"+session.Cash+"\n\nCompletion milestone secured!";
            card.DOKill();card.localScale=Vector3.one*.94f;card.DOScale(1,.25f).SetEase(Ease.OutBack).SetUpdate(true);
            var random=new System.Random(73);
            foreach(var piece in confetti){piece.DOKill();piece.gameObject.SetActive(true);piece.anchoredPosition=new Vector2((float)random.NextDouble()*540-270,175);piece.localRotation=Quaternion.identity;piece.localScale=Vector3.one;var end=piece.anchoredPosition+new Vector2((float)random.NextDouble()*100-50,-340);DOTween.Sequence().AppendInterval((float)random.NextDouble()*.15f).Append(piece.DOAnchorPos(end,1.1f).SetEase(Ease.OutQuad)).Join(piece.DORotate(new Vector3(0,0,360),1.1f,RotateMode.FastBeyond360)).Join(piece.DOScale(.3f,1.1f)).SetUpdate(true).OnComplete(()=>piece.gameObject.SetActive(false)).SetTarget(piece);}
        }
        public void ContinueExploring(){if(!IsOpen)return;session.AcknowledgeCompletion();Close();}
        void Close(){if(!IsOpen)return;IsOpen=false;root.SetActive(false);session.player.SetBlocked(previousBlocked);Cursor.lockState=previousCursor;Cursor.visible=previousVisible;KillEffects();}
        void KillEffects(){if(card)card.DOKill();if(confetti!=null)foreach(var piece in confetti)if(piece){piece.DOKill();piece.gameObject.SetActive(false);}}
        void ResetPanel(){pending=false;Close();KillEffects();}
    }
}
