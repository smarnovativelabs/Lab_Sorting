using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    // Collected Items rests as a compact header bar; tapping it unrolls the list downward.
    // The list stays active while collapsed (clipped by the panel's RectMask2D and hidden), so
    // HUD refresh, auto-scroll and selection keep working exactly as before.
    public sealed class CollectedDropdown:MonoBehaviour
    {
        public SortingHUD hud;
        public RectTransform panel,chevron;
        public Button toggle;
        public Image collapsedArt;
        public float collapsedHeight=60,duration=.22f;
        public bool startExpanded;
        [Tooltip("Fold the list away shortly after the player picks an item from it.")]
        public bool collapseOnSelect=true;
        Image panelImage;Color panelColor,artColor;ScrollRect scroll;CanvasGroup[] body;
        float expandedHeight,open,target,collapseAt=-1;bool ready;
        public bool Expanded=>target>0;
        public bool Animating=>open!=target;
        void Awake()
        {
            if(!panel)panel=(RectTransform)transform;
            panelImage=panel.GetComponent<Image>();if(panelImage)panelColor=panelImage.color;if(collapsedArt)artColor=collapsedArt.color;
            expandedHeight=panel.sizeDelta.y;
            PinTop(panel,null);
            scroll=panel.GetComponent<ScrollRect>();
            if(scroll)
            {
                var parts=new System.Collections.Generic.List<CanvasGroup>();
                foreach(var part in new[]{scroll.viewport,scroll.verticalScrollbar?(RectTransform)scroll.verticalScrollbar.transform:null})
                {if(!part)continue;PinTop(part,panel);var group=part.GetComponent<CanvasGroup>();if(!group)group=part.gameObject.AddComponent<CanvasGroup>();parts.Add(group);}
                body=parts.ToArray();
            }
            else body=new CanvasGroup[0];
            if(toggle)toggle.onClick.AddListener(Toggle);
            if(hud)foreach(var b in hud.inventoryButtons)if(b)b.onClick.AddListener(ItemChosen);
            open=target=startExpanded?1:0;ready=true;Apply();
        }
        // Keep the top edge fixed so height changes grow/shrink from the bottom.
        static void PinTop(RectTransform r,RectTransform parent)
        {
            if(parent==null)
            {
                if(Mathf.Approximately(r.pivot.y,1))return;
                r.anchoredPosition+=new Vector2(0,(1-r.pivot.y)*r.sizeDelta.y);r.pivot=new Vector2(r.pivot.x,1);return;
            }
            if(!Mathf.Approximately(r.anchorMin.y,r.anchorMax.y)||Mathf.Approximately(r.anchorMin.y,1))return;
            r.anchoredPosition+=new Vector2(0,(r.anchorMin.y-1)*parent.sizeDelta.y);
            r.anchorMin=new Vector2(r.anchorMin.x,1);r.anchorMax=new Vector2(r.anchorMax.x,1);
        }
        bool Blocked=>hud&&hud.session&&hud.session.player&&hud.session.player.blocked;
        public void Toggle(){if(!Blocked)SetExpanded(target<=0);}
        public void SetExpanded(bool value){target=value?1:0;collapseAt=-1;}
        void ItemChosen(){if(collapseOnSelect&&target>0&&!Blocked)collapseAt=Time.unscaledTime+.35f;}
        void Update()
        {
            if(toggle)toggle.interactable=!Blocked;
            if(collapseAt>0&&Time.unscaledTime>=collapseAt){collapseAt=-1;if(!Blocked)target=0;}
            if(open==target)return;
            open=Mathf.MoveTowards(open,target,Time.unscaledDeltaTime/Mathf.Max(.01f,duration));Apply();
        }
        void Apply()
        {
            if(!ready)return;
            float eased=open*open*(3-2*open);
            panel.sizeDelta=new Vector2(panel.sizeDelta.x,Mathf.Lerp(collapsedHeight,expandedHeight,eased));
            // Panel art (with its header divider) takes over early, before the bar gets tall enough to squash its borders.
            float art=Mathf.Clamp01(open*3);
            if(panelImage)panelImage.color=new Color(panelColor.r,panelColor.g,panelColor.b,panelColor.a*art);
            if(collapsedArt)collapsedArt.color=new Color(artColor.r,artColor.g,artColor.b,artColor.a*(1-art));
            float content=Mathf.Clamp01((open-.25f)/.6f);bool usable=open>=1;
            foreach(var group in body){group.alpha=content;group.interactable=usable;group.blocksRaycasts=usable;}
            if(scroll)scroll.vertical=usable;
            if(chevron)chevron.localEulerAngles=new Vector3(0,0,Mathf.Lerp(-90,90,eased));
        }
    }
}
