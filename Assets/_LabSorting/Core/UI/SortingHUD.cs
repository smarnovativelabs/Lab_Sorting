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
        int revision=-1;
        readonly int[] selectionIndices=new int[30];
        void Start() { for(int i=0;i<inventoryButtons.Length;i++){int index=i;inventoryButtons[i].onClick.AddListener(()=>session.Select(selectionIndices[index]));} }
        void Update()
        {
            target.text=session.TargetItem?session.TargetItem.definition.displayName:session.TargetRack?(session.TargetRack.Type?session.TargetRack.Type.displayName:"Empty rack — assign selected item"):"";
            if(revision==session.Revision) return; revision=session.Revision;
            progress.text="LAB SORTING   /   PROTOTYPE\nSorted "+session.SortedCount+" / "+session.items.Length+"    Remaining "+(session.items.Length-session.SortedCount);
            int selectedCount=0;foreach(var item in session.carried)if(session.Active&&item.definition==session.Active.definition)selectedCount++;
            active.text=session.Active?"SELECTED  "+session.Active.definition.displayName+"  x"+selectedCount+"\nHands  "+session.carried.Count+" / "+session.capacity:"SELECT AN ITEM\nHands  0 / "+session.capacity;
            status.text=session.Message;
            int groups=0;
            for(int i=0;i<session.carried.Count;i++)
            {
                var type=session.carried[i].definition;bool seen=false;
                for(int g=0;g<groups;g++) if(session.carried[selectionIndices[g]].definition==type) {seen=true;break;}
                if(seen || groups>=inventoryButtons.Length) continue;
                int count=0;for(int j=i;j<session.carried.Count;j++) if(session.carried[j].definition==type) count++;
                selectionIndices[groups]=i;inventoryButtons[groups].gameObject.SetActive(true);
                inventoryLabels[groups].text=(session.Active&&session.Active.definition==type?"> ":"")+type.displayName+" x"+count;
                inventoryButtons[groups].image.color=session.Active&&session.Active.definition==type?new Color(.13f,.65f,.57f):new Color(.14f,.2f,.26f);groups++;
            }
            for(int i=groups;i<inventoryButtons.Length;i++) inventoryButtons[i].gameObject.SetActive(false);
        }
    }
}

