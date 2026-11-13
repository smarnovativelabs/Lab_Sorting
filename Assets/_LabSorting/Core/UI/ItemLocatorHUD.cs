using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class ItemLocatorHUD : MonoBehaviour
    {
        public SortingSession session;
        public RectTransform safeArea,focusFrame;
        public UnityEngine.UI.Text[] beacons;
        readonly SortItem[] nearest=new SortItem[6];readonly float[] distances=new float[6];
        readonly Vector2[] markerPositions=new Vector2[6];
        float nextScan;
        Vector2[] beaconSizes;
        void Awake(){beaconSizes=new Vector2[beacons.Length];for(int i=0;i<beacons.Length;i++)beaconSizes[i]=beacons[i].rectTransform.sizeDelta;}
        public int LooseSearchCount {get;private set;}
        bool cleanupMode;
        int cleanupRevision=-1;
        public bool FinalItemsMode=>LooseSearchCount>0&&(LooseSearchCount<=5||cleanupMode);
        Vector2 Local(Vector3 screen){var canvas=safeArea.GetComponentInParent<Canvas>();RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea,screen,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var p);return p;}
        void LateUpdate()
        {
            var camera=session.player.view;var target=session.TargetItem;
            bool show=target&&!session.player.blocked&&!target.Moving;
            focusFrame.gameObject.SetActive(show);
            if(show)
            {
                var b=target.visual.bounds;Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=-min;bool front=true;
                for(int i=0;i<8;i++){var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var screen=camera.WorldToScreenPoint(p);front&=screen.z>0;var local=Local(screen);min=Vector2.Min(min,local);max=Vector2.Max(max,local);}
                focusFrame.gameObject.SetActive(front);focusFrame.anchoredPosition=(min+max)*.5f;focusFrame.sizeDelta=(max-min)+Vector2.one*(12+3*Mathf.Sin(Time.unscaledTime*5));
            }
            for(int i=0;i<beacons.Length;i++)if(i>=nearest.Length||!nearest[i]||nearest[i].location!=ItemLocation.Loose||!session.SearchType||session.player.blocked)beacons[i].gameObject.SetActive(false);
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.15f;
            for(int i=0;i<nearest.Length;i++){nearest[i]=null;distances[i]=float.MaxValue;}
            LooseSearchCount=0;
            if(cleanupRevision!=session.Revision){cleanupRevision=session.Revision;int remaining=session.items.Length-session.SortedCount;cleanupMode=remaining>0&&remaining<=SortingSession.CleanupThreshold;}
            if(session.SearchType&&!session.player.blocked)
            foreach(var item in session.ItemsOfType(session.SearchType))
            {
                if(item.location!=ItemLocation.Loose||item.definition!=session.SearchType)continue;
                LooseSearchCount++;
                var viewport=camera.WorldToViewportPoint(item.visual.bounds.center);
                if(viewport.z<=0||viewport.x<.1f||viewport.x>.85f||viewport.y<.22f||viewport.y>.83f)continue;
                var delta=item.visual.bounds.center-camera.transform.position;
                if(Physics.Raycast(camera.transform.position,delta.normalized,out var hit,delta.magnitude+.01f,~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<SortItem>()!=item)continue;
                float distance=delta.sqrMagnitude;
                for(int i=0;i<nearest.Length;i++)if(distance<distances[i]){for(int j=nearest.Length-1;j>i;j--){nearest[j]=nearest[j-1];distances[j]=distances[j-1];}nearest[i]=item;distances[i]=distance;break;}
            }
            int shown=0;
            for(int i=0;i<beacons.Length;i++)
            {
                var item=i<nearest.Length?nearest[i]:null;beacons[i].gameObject.SetActive(item);
                if(!item)continue;beacons[i].text=(FinalItemsMode?"LAST "+LooseSearchCount+"  ▼  ":"▼  ")+Mathf.Sqrt(distances[i]).ToString("0.0")+"m"+(FinalItemsMode?"\n"+SearchDirection.Floor(item.hitbox?item.hitbox.bounds.min.y:item.transform.position.y,session.player.transform.position.y):"");
                beacons[i].color=FinalItemsMode?new Color(.45f,1,1):new Color(1,.88f,.25f);
                beacons[i].rectTransform.sizeDelta=FinalItemsMode?new Vector2(190,Mathf.Max(52,beaconSizes[i].y)):beaconSizes[i];
                beacons[i].transform.localScale=Vector3.one*(FinalItemsMode?1.08f+.04f*Mathf.Sin(Time.unscaledTime*4):1);
                var position=Local(camera.WorldToScreenPoint(item.visual.bounds.center+Vector3.up*(item.visual.bounds.extents.y+.08f)));
                bool overlaps=false;
                for(int j=0;j<shown;j++)if(Mathf.Abs(position.x-markerPositions[j].x)<(FinalItemsMode?190:104)&&Mathf.Abs(position.y-markerPositions[j].y)<(FinalItemsMode?58:34))overlaps=true;
                // Suppress redundant labels instead of moving arrows onto the wrong item.
                if(overlaps){beacons[i].gameObject.SetActive(false);continue;}
                markerPositions[shown++]=position;beacons[i].rectTransform.anchoredPosition=position;
            }
        }
    }
}

