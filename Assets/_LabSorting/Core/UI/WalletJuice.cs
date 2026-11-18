using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    // When cash goes up during play, a few cash icons fly from the screen centre into the wallet, the wallet
    // gives a small bump (its number counts up in EconomyHUD) and the phone taps once. Pooled; no allocations
    // after the first burst. Spending and the startup load never fly; call Quiet() around a programmatic restore.
    public sealed class WalletJuice:MonoBehaviour
    {
        public SortingSession session;
        public SortingFeedback feedback;
        public RectTransform wallet,layer;
        public Sprite coin;
        [Min(1)] public int coins=6;
        public float size=30,duration=.55f,stagger=.05f;
        Image[] pool;int lastCash=-1;float quietUntil;
        public int Bursts {get;private set;}
        void Update()
        {
            if(!session||!session.Initialized){lastCash=-1;return;}
            int cash=session.Cash;
            if(lastCash<0){lastCash=cash;return;}
            if(cash>lastCash&&Time.unscaledTime>=quietUntil)Burst();
            lastCash=cash;
        }
        public void Quiet(float seconds=.2f){quietUntil=Time.unscaledTime+seconds;if(session)lastCash=session.Cash;}
        void Burst()
        {
            if(!wallet||!layer||!coin)return;
            Bursts++;if(feedback)feedback.Reward();
            if(pool==null)
            {
                pool=new Image[coins];
                for(int i=0;i<coins;i++){var go=new GameObject("WalletCoin",typeof(RectTransform),typeof(Image));go.layer=layer.gameObject.layer;go.transform.SetParent(layer,false);var image=go.GetComponent<Image>();image.sprite=coin;image.preserveAspect=true;image.raycastTarget=false;((RectTransform)go.transform).sizeDelta=Vector2.one*size;go.SetActive(false);pool[i]=image;}
            }
            layer.SetAsLastSibling();
            Vector2 target=layer.InverseTransformPoint(wallet.position);
            for(int i=0;i<pool.Length;i++)
            {
                var image=pool[i];var r=image.rectTransform;DOTween.Kill(r);
                image.gameObject.SetActive(true);r.localScale=Vector3.one*.6f;
                r.anchoredPosition=new Vector2(Random.Range(-60f,60f),Random.Range(-30f,30f));
                var go=image.gameObject;
                DOTween.Sequence().SetUpdate(true).SetTarget(r).AppendInterval(i*stagger)
                    .Append(r.DOScale(1,duration*.35f).SetEase(Ease.OutBack))
                    .Append(r.DOAnchorPos(target,duration).SetEase(Ease.InQuad))
                    .Join(r.DOScale(.5f,duration).SetEase(Ease.InQuad))
                    .OnComplete(()=>go.SetActive(false));
            }
            DOTween.Kill(wallet,true);wallet.DOPunchScale(Vector3.one*.12f,.3f,6,.6f).SetDelay(duration*.9f).SetUpdate(true).SetTarget(wallet);
        }
        void OnDisable(){if(pool!=null)foreach(var image in pool)if(image){DOTween.Kill(image.rectTransform);image.gameObject.SetActive(false);}if(wallet){DOTween.Kill(wallet,true);wallet.localScale=Vector3.one;}}
    }
}
