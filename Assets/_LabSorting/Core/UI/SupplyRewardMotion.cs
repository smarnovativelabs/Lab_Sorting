using UnityEngine;
using DG.Tweening;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class SupplyRewardMotion:MonoBehaviour
    {
        public SortingHUD hud;
        public SupplyPanel supply;
        public EconomyHUD economy;
        UnityEngine.UI.Image flying;
        Tween motion;
        public int Played {get;private set;}
        void OnEnable(){hud.session.SupplyRewardPresented+=Play;}
        void Play(SupplyKind kind,int amount)
        {
            if(!flying){var go=new GameObject("Supply Reward Flight",typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(hud.transform,false);flying=go.GetComponent<UnityEngine.UI.Image>();flying.raycastTarget=false;flying.preserveAspect=true;flying.rectTransform.sizeDelta=Vector2.one*44;}
            motion?.Kill();Played++;
            flying.sprite=kind==SupplyKind.Search?supply.searchSprite:kind==SupplyKind.Magnet?supply.magnetSprite:kind==SupplyKind.Slot?supply.slotSprite:economy.wallet.GetComponentInParent<UnityEngine.UI.Image>().sprite;
            flying.gameObject.SetActive(true);flying.transform.SetAsLastSibling();flying.color=Color.white;flying.transform.localScale=Vector3.one;
            var target=kind==SupplyKind.Search?economy.searchButton.transform:kind==SupplyKind.Magnet?economy.magnetButton.transform:kind==SupplyKind.Slot?hud.inventorySummary.transform:economy.wallet.transform;
            flying.transform.position=supply.IsOpen?supply.cashRewardIcon.transform.position:hud.transform.TransformPoint(Vector3.zero);
            motion=DOTween.Sequence().Append(flying.transform.DOMove(target.position,.48f).SetEase(Ease.InOutSine)).Join(flying.transform.DOScale(.65f,.48f)).Append(flying.DOFade(0,.12f)).SetUpdate(true).OnComplete(()=>flying.gameObject.SetActive(false));
        }
        void OnDisable(){if(hud&&hud.session)hud.session.SupplyRewardPresented-=Play;motion?.Kill();if(flying)flying.gameObject.SetActive(false);}
    }
}
