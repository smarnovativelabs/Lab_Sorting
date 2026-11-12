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
        public Image markerBackdrop;
        public bool compact;
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
            if(session.MagnetSeconds>0)magnetLabel.text="MAGNET  "+session.MagnetSeconds.ToString("0.0")+"s\n"+session.MagnetStatus;
            upgradeLabel.text=session.capacity>=session.maxCapacity?"CAPACITY MAX":"+1 SLOT  $"+session.UpgradeCost;
            bool enabled=!session.player.blocked;
            if(!session.Active) {if(!session.SearchType)searchLabel.text="SEARCH  $"+session.searchCost+"\nSelect an item";magnetLabel.text="MAGNET  $"+session.magnetCost+"\nSelect an item";}
            else if(session.Cash<session.searchCost&&!session.SearchType)searchLabel.text+=" (low cash)";
            if(session.Active&&session.Cash<session.magnetCost)magnetLabel.text+=" (low cash)";
            if(session.MagnetSeconds>0)magnetLabel.text="MAGNET  "+session.MagnetSeconds.ToString("0.0")+"s\n"+session.MagnetStatus;
            searchButton.interactable=enabled&&session.Active&&!session.SearchType&&session.Cash>=session.searchCost;
            magnetButton.interactable=enabled&&session.MagnetSeconds<=0&&(!session.Active||(session.carried.Count<session.capacity&&!session.Active.Moving&&session.Cash>=session.magnetCost));
            upgradeButton.interactable=enabled&&session.capacity<session.maxCapacity&&session.Cash>=session.UpgradeCost;
            if(compact)
            {
                wallet.text="$"+session.Cash+"  +";
                searchLabel.text=session.SearchType?session.SearchSeconds.ToString("0.0")+"s":"SEARCH"+(session.SearchCredits>0?"  "+session.SearchCredits:"  +");
                magnetLabel.text=session.MagnetSeconds>0?session.MagnetSeconds.ToString("0.0")+"s":"MAGNET"+(session.MagnetCredits>0?"  "+session.MagnetCredits:"  +");
                upgradeLabel.text=session.carried.Count+" / "+session.capacity+"  +";
                searchButton.interactable=magnetButton.interactable=upgradeButton.interactable=enabled;
            }
            var item=session.NearestSearchResult();marker.gameObject.SetActive(item&&!session.player.blocked);
            if(markerBackdrop)markerBackdrop.gameObject.SetActive(marker.gameObject.activeSelf);
            if(!item) return;
            var camera=session.player.view;var point=camera.WorldToViewportPoint(item.transform.position+Vector3.up*.3f);
            bool behind=point.z<0;
            if(behind) {point.x=1-point.x;point.y=.5f;}
            bool offscreen=behind||point.x<.08f||point.x>.92f||point.y<.12f||point.y>.82f;
            float x=Mathf.Clamp(point.x,.10f,.9f),y=Mathf.Clamp(point.y,.18f,.78f);
            marker.rectTransform.anchorMin=marker.rectTransform.anchorMax=new Vector2(x,y);marker.rectTransform.anchoredPosition=Vector2.zero;
            if(compact)
            {
                // One stable direction strip; world-space beacons identify the actual models.
                marker.rectTransform.anchorMin=marker.rectTransform.anchorMax=new Vector2(.5f,1);
                marker.rectTransform.anchoredPosition=new Vector2(0,-167);
                int remaining=0;foreach(var candidate in session.ItemsOfType(session.SearchType))if(candidate.location!=ItemLocation.Rack)remaining++;
                marker.text=SearchDirection.Describe(session,item)+"\n"+item.definition.displayName+"  |  "+remaining+" remaining";
            }
            else marker.text=(offscreen?(point.x<.5f?"< ":"> "):"v ")+item.definition.displayName+"  "+Vector3.Distance(camera.transform.position,item.transform.position).ToString("0")+"m";
            if(markerBackdrop){var r=markerBackdrop.rectTransform;r.anchorMin=marker.rectTransform.anchorMin;r.anchorMax=marker.rectTransform.anchorMax;r.anchoredPosition=marker.rectTransform.anchoredPosition;r.sizeDelta=marker.rectTransform.sizeDelta+new Vector2(16,8);}
        }
    }
}




