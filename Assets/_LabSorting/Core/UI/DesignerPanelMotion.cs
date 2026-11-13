using UnityEngine;
using DG.Tweening;
namespace LabSorting.UI
{
    public sealed class DesignerPanelMotion:MonoBehaviour
    {
        public RectTransform card;
        Tween motion,fade;
        CanvasGroup group;
        readonly CanvasGroup[] offers=new CanvasGroup[2];
        Tween offerMotion;
        Vector2 rest;
        public bool Closing {get;private set;}
        void OnEnable()
        {
            if(!card)return;Closing=false;rest=card.anchoredPosition;
            group=GetComponent<CanvasGroup>();if(!group)group=gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.interactable=true;
            card.localScale=Vector3.one*.94f;
            var offset=new Vector2(card.anchorMin.x==0?-18:card.anchorMin.x==1?18:0,card.anchorMin.x==.5f?-10:0);
            card.anchoredPosition=rest+offset;
            fade=group.DOFade(1,.2f).SetUpdate(true);
            motion=DOTween.Sequence().Join(card.DOScale(1,.24f)).Join(card.DOAnchorPos(rest,.24f)).SetEase(Ease.OutCubic).SetUpdate(true);
            var sequence=DOTween.Sequence().SetUpdate(true);
            for(int i=0;i<2;i++){var tile=card.Find(i==0?"CashOfferCard":"AdOfferCard");if(!tile)continue;offers[i]=tile.GetComponent<CanvasGroup>();if(!offers[i])offers[i]=tile.gameObject.AddComponent<CanvasGroup>();offers[i].alpha=0;sequence.Insert(.05f+i*.05f,offers[i].DOFade(1,.16f));}
            offerMotion=sequence;
        }
        public void CloseAnimated(System.Action completed)
        {
            if(Closing)return;if(!card||!isActiveAndEnabled){completed?.Invoke();return;}
            Closing=true;motion?.Kill();fade?.Kill();group.interactable=false;
            motion=DOTween.Sequence().Join(card.DOScale(.96f,.12f)).Join(group.DOFade(0,.12f)).SetUpdate(true).OnComplete(()=>{Closing=false;group.interactable=true;completed?.Invoke();if(gameObject.activeInHierarchy){group.alpha=1;card.localScale=Vector3.one;}});
        }
        void OnDisable(){motion?.Kill();fade?.Kill();offerMotion?.Kill();foreach(var offer in offers)if(offer)offer.alpha=1;Closing=false;if(group){group.alpha=1;group.interactable=true;}if(card){card.localScale=Vector3.one;card.anchoredPosition=rest;}}
    }
}
