using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    // HUD-only confetti, accompanying the separate item wave and rack label glow
    // (SortingFeedback.Celebrate owns the label):
    //  - placement increments are handled by SortedProgressFeedback at the top progress badge;
    //  - a completed rack: a short confetti burst from the rack label.
    // Pooled; nothing is created after the first use.
    public sealed class SortingCelebration:MonoBehaviour
    {
        public SortingSession session;
        public RectTransform layer;
        public Font font;
        public Sprite confettiSprite;
        [Min(1)] public int confettiPieces=18;
        Image[] confetti;
        public int Bursts {get;private set;}
        static readonly Color[] Palette={new Color32(0xFF,0xC2,0x1A,255),new Color32(0x34,0xC7,0x59,255),new Color32(0x3E,0xC7,0xF0,255),new Color32(0xFF,0x6F,0x91,255),Color.white};
        SortingFeedback feedback;
        void OnEnable(){if(session){session.VisualsReset+=Hide;session.Backgrounded+=Hide;feedback=session.GetComponent<SortingFeedback>();if(feedback)feedback.RackCelebrated+=Completed;}}
        void OnDisable(){if(session){session.VisualsReset-=Hide;session.Backgrounded-=Hide;}if(feedback)feedback.RackCelebrated-=Completed;Hide();}
        bool ToLayer(Vector3 world,out Vector2 local)
        {
            local=default;var camera=session.player.view;if(!camera||!layer)return false;
            var screen=camera.WorldToScreenPoint(world);if(screen.z<=0)return false;
            var canvas=layer.GetComponentInParent<Canvas>();var eye=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,screen,eye,out local);
        }
        void Completed(SortRack rack)
        {
            if(!rack||!confettiSprite)return;
            var source=rack.label?rack.label.transform.position:rack.transform.position+Vector3.up*1.4f;
            if(!ToLayer(source,out var at))return;
            if(confetti==null)
            {
                confetti=new Image[confettiPieces];
                for(int i=0;i<confettiPieces;i++){var go=new GameObject("Confetti",typeof(RectTransform),typeof(Image));go.layer=layer.gameObject.layer;go.transform.SetParent(layer,false);var image=go.GetComponent<Image>();image.sprite=confettiSprite;image.raycastTarget=false;go.SetActive(false);confetti[i]=image;}
            }
            Bursts++;layer.SetAsLastSibling();
            for(int i=0;i<confetti.Length;i++)
            {
                var image=confetti[i];var r=image.rectTransform;DOTween.Kill(r);image.gameObject.SetActive(true);
                image.color=Palette[i%Palette.Length];r.sizeDelta=i%3==0?new Vector2(20,10):new Vector2(13,13);r.anchoredPosition=at;r.localRotation=Quaternion.Euler(0,0,Random.Range(0,360));r.localScale=Vector3.one;
                float angle=Random.Range(20f,160f)*Mathf.Deg2Rad,speed=Random.Range(120f,220f);var peak=at+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*speed;var land=peak+new Vector2(Random.Range(-25f,25f),-Random.Range(110f,170f));
                var go=image.gameObject;
                DOTween.Sequence().SetUpdate(true).SetTarget(r)
                    .Append(r.DOAnchorPos(peak,.35f).SetEase(Ease.OutQuad)).Append(r.DOAnchorPos(land,.55f).SetEase(Ease.InQuad))
                    .Insert(0,r.DOLocalRotate(new Vector3(0,0,Random.Range(-540f,540f)),.9f,RotateMode.FastBeyond360))
                    .Insert(.6f,image.DOFade(0,.3f))
                    .OnComplete(()=>go.SetActive(false));
            }
        }
        void Hide()
        {
            if(confetti!=null)foreach(var c in confetti)if(c){DOTween.Kill(c.rectTransform);c.gameObject.SetActive(false);}
        }
    }
}
