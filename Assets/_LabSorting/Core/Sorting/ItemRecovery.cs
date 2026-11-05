using System.Collections.Generic;
using UnityEngine;
namespace LabSorting.Sorting
{
    [DisallowMultipleComponent]
    public sealed class ItemRecovery : MonoBehaviour
    {
        public SortingSession session;
        public BoxCollider playableBounds;
        public Transform[] safePoints;
        public bool automatic=true;
        public float minimumY=-.05f;
        [Min(.1f)] public float scanInterval=.5f,graceSeconds=2;
        [Min(1)] public int itemsPerScan=64;
        [Min(.01f)] public float penetrationTolerance=.06f;
        public int RecoveredCount {get;private set;}
        public string LastStatus {get;private set;}="Ready";
        readonly Dictionary<SortItem,float> invalidSince=new Dictionary<SortItem,float>();
        readonly Collider[] overlaps=new Collider[64];
        readonly RaycastHit[] floorHits=new RaycastHit[64];
        float nextScan;int cursor;
        public bool Configured=>session&&playableBounds&&playableBounds.enabled&&playableBounds.isTrigger&&safePoints!=null&&safePoints.Length>0;
        void OnEnable(){if(session)session.VisualsReset+=ResetScan;}
        void OnDisable(){if(session)session.VisualsReset-=ResetScan;ResetScan();}
        void ResetScan(){invalidSince.Clear();cursor=0;nextScan=0;}
        void Update()
        {
            if(!automatic||!Configured||Time.unscaledTime<nextScan||session.items==null||session.items.Length==0)return;
            nextScan=Time.unscaledTime+Mathf.Max(.1f,scanInterval);int recovered=0;
            for(int i=0;i<Mathf.Min(Mathf.Max(1,itemsPerScan),session.items.Length);i++){if(cursor>=session.items.Length)cursor=0;if(Recover(session.items[cursor++],true))recovered++;}
            if(recovered>0)session.ReportRecovery(recovered);
        }
        public bool Contains(Vector3 world)
        {
            if(!playableBounds)return false;var p=playableBounds.transform.InverseTransformPoint(world)-playableBounds.center;var half=playableBounds.size*.5f;
            return Mathf.Abs(p.x)<=half.x&&Mathf.Abs(p.y)<=half.y&&Mathf.Abs(p.z)<=half.z;
        }
        bool ContainsBox(Vector3 center,Vector3 extents)
        {
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)if(!Contains(center+Vector3.Scale(extents,new Vector3(x,y,z))))return false;return true;
        }
        public bool NeedsRecovery(SortItem item)
        {
            if(!item||item.location!=ItemLocation.Loose||item.Moving||!item.gameObject.activeInHierarchy||!item.hitbox||!item.hitbox.enabled)return false;
            if(item.transform.position.y<minimumY||!Contains(item.transform.position))return true;
            var bounds=item.hitbox.bounds;int count=Physics.OverlapBoxNonAlloc(bounds.center,bounds.extents,overlaps,Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var other=overlaps[i];if(other==item.hitbox||other.transform.IsChildOf(item.transform)||other.GetComponentInParent<SortItem>()||other.attachedRigidbody)continue;
                if(Physics.ComputePenetration(item.hitbox,item.hitbox.transform.position,item.hitbox.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float distance)&&distance>penetrationTolerance)return true;
            }
            return false;
        }
        public bool TrySafePosition(SortItem item,out Vector3 position)
        {
            position=Vector3.zero;if(!Configured||!item||!item.hitbox)return false;var bounds=item.hitbox.bounds;var extents=bounds.extents;var offset=bounds.center-item.transform.position;
            foreach(var point in safePoints)
            {
                if(!point)continue;int hits=Physics.RaycastNonAlloc(point.position+Vector3.up*2,Vector3.down,floorHits,4,~(1<<2),QueryTriggerInteraction.Ignore);
                if(hits>=floorHits.Length)continue;float nearest=float.PositiveInfinity;Vector3 floor=Vector3.zero;bool found=false;
                for(int h=0;h<hits;h++){var hit=floorHits[h];if(hit.normal.y<.7f||hit.collider.GetComponentInParent<SortItem>()||hit.collider.GetComponentInParent<SortRack>()||hit.collider.attachedRigidbody)continue;if(hit.distance<nearest){nearest=hit.distance;floor=hit.point;found=true;}}
                if(!found)continue;var center=floor+Vector3.up*(extents.y+.025f);var pivot=center-offset;
                if(pivot.y<minimumY||!ContainsBox(center,extents))continue;
                int count=Physics.OverlapBoxNonAlloc(center,extents*.99f,overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);bool clear=count<overlaps.Length;
                for(int i=0;i<count&&clear;i++)if(overlaps[i]!=item.hitbox&&!overlaps[i].transform.IsChildOf(item.transform))clear=false;
                if(clear){position=pivot;return true;}
            }
            return false;
        }
        bool Recover(SortItem item,bool waitForGrace)
        {
            if(!NeedsRecovery(item)){if(item)invalidSince.Remove(item);return false;}
            if(waitForGrace){if(!invalidSince.TryGetValue(item,out float since)){invalidSince[item]=Time.unscaledTime;return false;}if(Time.unscaledTime-since<Mathf.Max(.1f,graceSeconds))return false;}
            if(!TrySafePosition(item,out var position)){LastStatus="No clear recovery point; retrying later";return false;}
            item.Snap(position,item.transform.rotation);var body=item.GetComponent<Rigidbody>();if(body&&!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            Physics.SyncTransforms();invalidSince.Remove(item);RecoveredCount++;LastStatus="Recovered "+item.id;return true;
        }
        public int RecoverLostItems(bool waitForGrace=false)
        {
            if(!Application.isPlaying||!Configured){LastStatus="Recovery configuration missing, or not in Play mode";return 0;}Physics.SyncTransforms();int count=0;foreach(var item in session.items)if(Recover(item,waitForGrace))count++;if(count>0)session.ReportRecovery(count);return count;
        }
        void OnDrawGizmosSelected(){if(playableBounds){Gizmos.color=Color.cyan;Gizmos.matrix=playableBounds.transform.localToWorldMatrix;Gizmos.DrawWireCube(playableBounds.center,playableBounds.size);Gizmos.matrix=Matrix4x4.identity;}if(safePoints!=null)foreach(var point in safePoints)if(point){Gizmos.color=Color.green;Gizmos.DrawWireSphere(point.position,.15f);}}
    }
    public sealed partial class SortingSession
    {
        public void ReportRecovery(int count){if(count<=0)return;Message="Recovered "+count+" lost item"+(count==1?"":"s")+" to the safe area";saveAt=Time.unscaledTime+.4f;Notify();}
    }
}

