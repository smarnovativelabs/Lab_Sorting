using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public sealed class LabRackRoutes:MonoBehaviour
    {
        NavMeshData data;NavMeshDataInstance instance;NavMeshPath path;
        void Awake(){path=new NavMeshPath();}
        public bool Ready=>instance.valid;
        public static LabRackRoutes For(SortingSession session)
        {
            var routes=session.GetComponent<LabRackRoutes>();if(!routes){routes=session.gameObject.AddComponent<LabRackRoutes>();routes.Build(session);}return routes;
        }
        void Build(SortingSession session)
        {
            var sources=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources((Transform)null,~(1<<2),NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            sources.RemoveAll(s=>!s.component||s.component.GetComponentInParent<SortItem>()||s.component.transform.IsChildOf(session.player.transform)||(s.component is Collider c&&c.isTrigger));
            if(sources.Count==0)return;
            var bounds=new Bounds(session.player.transform.position,Vector3.one);
            foreach(var source in sources)if(source.component is Collider c)bounds.Encapsulate(c.bounds);bounds.Expand(4);
            var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.25f;settings.agentHeight=1.6f;settings.agentClimb=.35f;settings.agentSlope=50;
            data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(data)instance=NavMesh.AddNavMeshData(data);
        }
        public bool Route(Vector3 from,SortRack rack,out float distance,out Vector3 waypoint)
        {
            distance=float.PositiveInfinity;waypoint=rack.transform.position;
            if(!Ready||!NavMesh.SamplePosition(from,out var start,1.5f,NavMesh.AllAreas))return false;
            for(int side=-1;side<=1;side+=2)
            {
                var approach=rack.transform.TransformPoint(new Vector3(0,0,side*.9f));
                if(!NavMesh.SamplePosition(approach,out var target,1.2f,NavMesh.AllAreas)||Mathf.Abs(target.position.y-approach.y)>1.1f)continue;
                if(!NavMesh.CalculatePath(start.position,target.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                var corners=path.corners;float length=0;for(int i=1;i<corners.Length;i++)length+=Vector3.Distance(corners[i-1],corners[i]);
                if(length>=distance)continue;distance=length;waypoint=target.position;
                for(int i=1;i<corners.Length;i++)if(Vector3.Distance(from,corners[i])>1){waypoint=corners[i];break;}
            }
            return !float.IsInfinity(distance);
        }
        void OnDestroy(){if(instance.valid)instance.Remove();if(data)Destroy(data);}
    }
}
