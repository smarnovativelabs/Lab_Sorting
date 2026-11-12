using UnityEngine;
using UnityEngine.UI;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class SortingHUD : MonoBehaviour
    {
        public SortingSession session;
        public Text progress,active,status,target;
        public Button[] inventoryButtons;
        public Text[] inventoryLabels;
        public UnityEngine.UI.Image[] inventoryIcons;
        public UnityEngine.UI.Image activeIcon;
        public UnityEngine.UI.Image targetBackdrop;
        public bool compact;
        public UnityEngine.UI.Button previousPage,nextPage; public UnityEngine.UI.Text pageLabel;
        int revision=-1,page;bool wasBlocked;
        readonly int[] selectionIndices=new int[30];
        readonly ItemDefinition[] buttonTypes=new ItemDefinition[30];
        int pageCount=1;
        public void ChangePage(int delta) {if(session.player.blocked)return;page=Mathf.Clamp(page+delta,0,pageCount-1);revision=-1;}
        void Start() { if(previousPage)previousPage.onClick.AddListener(()=>ChangePage(-1));if(nextPage)nextPage.onClick.AddListener(()=>ChangePage(1)); for(int i=0;i<inventoryButtons.Length;i++){int index=i;inventoryButtons[i].onClick.AddListener(()=>SelectType(buttonTypes[index]));} }
        string TossText()
        {
            var rack=session.TossRack;float distance=Vector3.Distance(session.player.view.transform.position,rack.transform.position);
            return "TOSS into "+(rack.Type?rack.Type.displayName+" rack":"empty rack")+"   "+distance.ToString("0")+" m";
        }
        void SelectType(ItemDefinition type){if(!type)return;for(int i=session.carried.Count-1;i>=0;i--)if(session.carried[i].definition==type){session.Select(i);return;}}
        void Update()
        {
            var rack=session.TargetRack;
            target.text=session.player.blocked?"":rack&&session.Active&&rack.Type==session.Active.definition&&rack.AvailabilityHint.Length>0?rack.AvailabilityHint:session.TargetItem?session.TargetItem.definition.displayName:rack?(rack.Type?rack.Type.displayName:"Empty rack — assign selected item"):session.TossRack?TossText():"";
            if(targetBackdrop)targetBackdrop.enabled=target.text.Length>0;
            target.color=session.TargetItem?new Color(.35f,1,1):Color.white;
            if(!session.player.blocked&&rack&&session.Active)
            {
                bool accepts=rack.CanAccept(session.Active.definition);
                target.text=accepts?"PLACE — "+session.Active.definition.displayName:rack.Type&&rack.Type!=session.Active.definition?"Wrong item — "+rack.Type.displayName:rack.CapacityFor(session.Active.definition)==0?"Too big for this rack":"Rack full";
                target.color=accepts?new Color(.35f,1,.55f):new Color(1,.45f,.4f);
            }
            if(revision==session.Revision&&wasBlocked==session.player.blocked) return; revision=session.Revision;wasBlocked=session.player.blocked;
            progress.text="LAB SORTING\nSorted "+session.SortedCount+" / "+session.items.Length+"    Remaining "+(session.items.Length-session.SortedCount);
            if(compact)progress.text="SORTED  "+session.SortedCount+" / "+session.items.Length;
            if(activeIcon){activeIcon.sprite=session.Active?session.Active.definition.icon:null;activeIcon.enabled=activeIcon.sprite;}
            int selectedCount=0;foreach(var item in session.carried)if(session.Active&&item.definition==session.Active.definition)selectedCount++;
            active.text=session.Active?"SELECTED  "+session.Active.definition.displayName+"  x"+selectedCount+"\nHands  "+session.carried.Count+" / "+session.capacity:"SELECT AN ITEM\nHands  0 / "+session.capacity;
            status.text=session.Message;status.color=session.MessageIsError?new Color(1,.68f,.32f):Color.white;
            int groups=0;
            for(int i=0;i<session.carried.Count;i++)
            {
                var type=session.carried[i].definition;bool seen=false;
                for(int g=0;g<groups;g++)if(session.carried[selectionIndices[g]].definition==type){seen=true;break;}
                if(!seen&&groups<selectionIndices.Length)selectionIndices[groups++]=i;
            }
            int pageSize=Mathf.Min(10,inventoryButtons.Length);
            int pages=Mathf.Max(1,Mathf.CeilToInt(groups/(float)pageSize));pageCount=pages;page=Mathf.Clamp(page,0,pages-1);
            if(previousPage){previousPage.gameObject.SetActive(pages>1);previousPage.interactable=page>0&&!session.player.blocked;}
            if(nextPage){nextPage.gameObject.SetActive(pages>1);nextPage.interactable=page<pages-1&&!session.player.blocked;}
            if(pageLabel){pageLabel.gameObject.SetActive(pages>1);pageLabel.text=(page+1)+" / "+pages;}
            for(int slot=0;slot<inventoryButtons.Length;slot++)
            {
                int group=page*pageSize+slot;bool visible=slot<pageSize&&group<groups;inventoryButtons[slot].gameObject.SetActive(visible);if(!visible){buttonTypes[slot]=null;continue;}
                var type=session.carried[selectionIndices[group]].definition;int count=0;
                if(inventoryIcons!=null&&slot<inventoryIcons.Length&&inventoryIcons[slot]){inventoryIcons[slot].sprite=type.icon;inventoryIcons[slot].enabled=type.icon;}
                buttonTypes[slot]=type;
                foreach(var item in session.carried)if(item.definition==type)count++;
                bool selected=session.Active&&session.Active.definition==type;
                inventoryLabels[slot].text=(selected?"> ":"")+type.displayName+" x"+count;
                inventoryButtons[slot].interactable=!session.player.blocked;
                inventoryButtons[slot].image.color=selected?new Color(.08f,.52f,.43f):new Color(.10f,.16f,.22f);
            }
        }
    }
}

