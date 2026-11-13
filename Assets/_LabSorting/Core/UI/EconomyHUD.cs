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
        public bool compact,designerSkin;
        float refreshAt;
        int revision=-1;
        SortItem guidedSearchItem;
        public SortItem GuidedSearchItem=>guidedSearchItem;
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
                wallet.text="$"+session.Cash+(designerSkin?"":"  +");
                searchLabel.text=session.SearchType?session.SearchSeconds.ToString("0.0")+"s":"SEARCH"+(session.SearchCredits>0?"  "+session.SearchCredits:designerSkin?"":"  +");
                magnetLabel.text=session.MagnetSeconds>0?session.MagnetSeconds.ToString("0.0")+"s":"MAGNET"+(session.MagnetCredits>0?"  "+session.MagnetCredits:designerSkin?"":"  +");
                upgradeLabel.text=session.carried.Count+" / "+session.capacity+"  +";
                searchButton.interactable=magnetButton.interactable=upgradeButton.interactable=enabled;
            }
            var item=session.NearestSearchResult();
            // Keep the current beacon until another is materially closer, avoiding
            // direction flicker between tightly packed items of the same type.
            if(item&&guidedSearchItem&&guidedSearchItem.definition==session.SearchType&&guidedSearchItem.location==ItemLocation.Loose&&!guidedSearchItem.Moving)
            {
                var origin=session.player.transform.position;
                if((item.transform.position-origin).sqrMagnitude>=(guidedSearchItem.transform.position-origin).sqrMagnitude*.64f)item=guidedSearchItem;
            }
            guidedSearchItem=item;
            marker.gameObject.SetActive(item&&!session.player.blocked);
            if(markerBackdrop)markerBackdrop.gameObject.SetActive(marker.gameObject.activeSelf);
                        if(!item)
            {
                if(session.SearchType&&!session.player.blocked)
                {
                    marker.gameObject.SetActive(true);marker.rectTransform.anchorMin=marker.rectTransform.anchorMax=new Vector2(.5f,1);marker.rectTransform.anchoredPosition=new Vector2(0,-167);
                    marker.text="ALL FOUND: "+session.SearchType.displayName+"\nPlace carried items on their rack";marker.color=new Color(.5f,1,.7f);
                    if(markerBackdrop){markerBackdrop.gameObject.SetActive(true);var rect=markerBackdrop.rectTransform;rect.anchorMin=rect.anchorMax=marker.rectTransform.anchorMin;rect.anchoredPosition=marker.rectTransform.anchoredPosition;rect.sizeDelta=marker.rectTransform.sizeDelta+new Vector2(16,8);}
                }
                return;
            }
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
                int remaining=0,loose=0;foreach(var candidate in session.ItemsOfType(session.SearchType)){if(candidate.location!=ItemLocation.Rack)remaining++;if(candidate.location==ItemLocation.Loose)loose++;}
                float bottom=item.hitbox?item.hitbox.bounds.min.y:item.transform.position.y;
                float feet=session.player.transform.position.y;
                bool otherFloor=Mathf.Abs(bottom-feet)>1.8f;
                marker.color=otherFloor?new Color(1,.87f,.4f):new Color(.5f,1,1);
                marker.text=SearchDirection.Describe(session,item)+"\n"+(loose>0&&loose<=5?"LAST "+loose+" TO FIND  |  ":"")+SearchDirection.Route(bottom,feet)+"  |  "+remaining+" remaining";
            }
            else marker.text=(offscreen?(point.x<.5f?"< ":"> "):"v ")+item.definition.displayName+"  "+Vector3.Distance(camera.transform.position,item.transform.position).ToString("0")+"m";
            if(markerBackdrop){var r=markerBackdrop.rectTransform;r.anchorMin=marker.rectTransform.anchorMin;r.anchorMax=marker.rectTransform.anchorMax;r.anchoredPosition=marker.rectTransform.anchoredPosition;r.sizeDelta=marker.rectTransform.sizeDelta+new Vector2(16,8);}
        }
    }
}




