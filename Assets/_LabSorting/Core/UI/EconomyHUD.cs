using UnityEngine;
using UnityEngine.UI;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class EconomyHUD : MonoBehaviour
    {
        public SortingSession session;
        public Text wallet,searchLabel,magnetLabel,upgradeLabel,marker;
        public Button searchButton,magnetButton,upgradeButton;
        public RectTransform safeArea;
        float refreshAt;
        int revision=-1;
        void Update()
        {
            if(revision==session.Revision && Time.unscaledTime<refreshAt) return;
            revision=session.Revision;refreshAt=Time.unscaledTime+.1f;
            var type=session.Active?session.Active.definition.displayName:"Select item";
            wallet.text="CASH  "+session.Cash+"   |   Slots "+session.capacity+"/"+session.maxCapacity;
            searchLabel.text=session.SearchType?"SEARCH  "+session.SearchSeconds.ToString("0.0")+"s\n"+session.SearchType.displayName:"SEARCH  $"+session.searchCost+"\n"+type;
            magnetLabel.text="MAGNET  $"+session.magnetCost+"\n"+(session.carried.Count>=session.capacity?type+" — FULL":type);
            upgradeLabel.text=session.capacity>=session.maxCapacity?"CAPACITY MAX":"+1 SLOT  $"+session.UpgradeCost;
            bool enabled=!session.player.blocked;
            if(!session.Active) {if(!session.SearchType)searchLabel.text="SEARCH  $"+session.searchCost+"\nSelect an item";magnetLabel.text="MAGNET  $"+session.magnetCost+"\nSelect an item";}
            else if(session.Cash<session.searchCost&&!session.SearchType)searchLabel.text+=" (low cash)";
            if(session.Active&&session.Cash<session.magnetCost)magnetLabel.text+=" (low cash)";
            searchButton.interactable=enabled&&session.Active&&!session.SearchType&&session.Cash>=session.searchCost;
            magnetButton.interactable=enabled&&session.Active&&session.carried.Count<session.capacity&&!session.Active.Moving&&session.Cash>=session.magnetCost;
            upgradeButton.interactable=enabled&&session.capacity<session.maxCapacity&&session.Cash>=session.UpgradeCost;
            var item=session.NearestSearchResult();marker.gameObject.SetActive(item);
            if(!item) return;
            var camera=session.player.view;var point=camera.WorldToViewportPoint(item.transform.position+Vector3.up*.3f);
            bool behind=point.z<0;
            if(behind) {point.x=1-point.x;point.y=.5f;}
            bool offscreen=behind||point.x<.08f||point.x>.92f||point.y<.12f||point.y>.82f;
            float x=Mathf.Clamp(point.x,.10f,.9f),y=Mathf.Clamp(point.y,.18f,.78f);
            marker.rectTransform.anchorMin=marker.rectTransform.anchorMax=new Vector2(x,y);marker.rectTransform.anchoredPosition=Vector2.zero;
            marker.text=(offscreen?(point.x<.5f?"< ":"> "):"v ")+item.definition.displayName+"  "+Vector3.Distance(camera.transform.position,item.transform.position).ToString("0")+"m";
        }
    }
}




