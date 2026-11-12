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
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.15f;
            for(int i=0;i<nearest.Length;i++){nearest[i]=null;distances[i]=float.MaxValue;}
            if(session.SearchType&&!session.player.blocked)
            foreach(var item in session.ItemsOfType(session.SearchType))
            {
                if(item.location!=ItemLocation.Loose||item.definition!=session.SearchType)continue;
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
                if(!item)continue;beacons[i].text="▼  "+Mathf.Sqrt(distances[i]).ToString("0.0")+"m";
                var position=Local(camera.WorldToScreenPoint(item.visual.bounds.center+Vector3.up*(item.visual.bounds.extents.y+.08f)));
                bool overlaps=false;
                for(int j=0;j<shown;j++)if(Mathf.Abs(position.x-markerPositions[j].x)<104&&Mathf.Abs(position.y-markerPositions[j].y)<34)overlaps=true;
                // Suppress redundant labels instead of moving arrows onto the wrong item.
                if(overlaps){beacons[i].gameObject.SetActive(false);continue;}
                markerPositions[shown++]=position;beacons[i].rectTransform.anchoredPosition=position;
            }
        }
    }
}

