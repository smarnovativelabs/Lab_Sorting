using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    // HUD-only placement feedback, so it can never disturb item poses, batching or the rack label glow
    // (SortingFeedback.Celebrate owns the label):
    //  - every placement: a small gold "+1" floats up from the slot;
    //  - a completed rack: a short confetti burst from the rack label.
    // Pooled; nothing is created after the first use.
    public sealed class SortingCelebration:MonoBehaviour
    {
        public SortingSession session;
        public RectTransform layer;
        public Font font;
        public Sprite confettiSprite;
        [Min(1)] public int plusPool=4,confettiPieces=18;
        Text[] plus;int nextPlus;
        Image[] confetti;
        public int PlusShown {get;private set;}
        public int Bursts {get;private set;}
        static readonly Color[] Palette={new Color32(0xFF,0xC2,0x1A,255),new Color32(0x34,0xC7,0x59,255),new Color32(0x3E,0xC7,0xF0,255),new Color32(0xFF,0x6F,0x91,255),Color.white};
        void OnEnable(){if(session){session.ItemPlaced+=Placed;session.RackCompleted+=Completed;}}
        void OnDisable(){if(session){session.ItemPlaced-=Placed;session.RackCompleted-=Completed;}Hide();}
        bool ToLayer(Vector3 world,out Vector2 local)
        {
            local=default;var camera=session.player.view;if(!camera||!layer)return false;
            var screen=camera.WorldToScreenPoint(world);if(screen.z<=0)return false;
            var canvas=layer.GetComponentInParent<Canvas>();var eye=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,screen,eye,out local);
        }
        void Placed(SortItem item)
        {
            if(!item||!item.rack||item.slot<0||!font)return;
            if(!ToLayer(item.rack.SlotPose(item.slot).position,out var at))return;
            if(plus==null)
            {
                plus=new Text[plusPool];
                for(int i=0;i<plusPool;i++)
                {
                    var go=new GameObject("PlusOne",typeof(RectTransform),typeof(Text),typeof(UnityEngine.UI.Outline));go.layer=layer.gameObject.layer;go.transform.SetParent(layer,false);
                    var t=go.GetComponent<Text>();t.font=font;t.fontSize=40;t.text="+1";t.alignment=TextAnchor.MiddleCenter;t.color=Palette[0];t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;
                    var o=go.GetComponent<UnityEngine.UI.Outline>();o.effectColor=new Color(.02f,.08f,.25f,.9f);o.effectDistance=new Vector2(1.5f,-1.5f);
                    ((RectTransform)go.transform).sizeDelta=new Vector2(110,56);go.SetActive(false);plus[i]=t;
                }
            }
            var text=plus[nextPlus];nextPlus=(nextPlus+1)%plus.Length;var r=text.rectTransform;DOTween.Kill(r);
            layer.SetAsLastSibling();text.gameObject.SetActive(true);r.anchoredPosition=at;r.localScale=Vector3.one*.6f;var color=Palette[0];color.a=1;text.color=color;PlusShown++;
            var go2=text.gameObject;
            DOTween.Sequence().SetUpdate(true).SetTarget(r)
                .Append(r.DOScale(1.1f,.12f).SetEase(Ease.OutBack)).Append(r.DOScale(1,.08f))
                .Insert(0,r.DOAnchorPos(at+Vector2.up*55,.7f).SetEase(Ease.OutCubic))
                .Insert(.42f,text.DOFade(0,.28f))
                .OnComplete(()=>go2.SetActive(false));
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
            if(plus!=null)foreach(var t in plus)if(t){DOTween.Kill(t.rectTransform);t.gameObject.SetActive(false);}
            if(confetti!=null)foreach(var c in confetti)if(c){DOTween.Kill(c.rectTransform);c.gameObject.SetActive(false);}
        }
    }
}
