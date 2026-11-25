using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace LabSorting.Sorting
{
    // Optional visual-only optimization. IDs, colliders, inventory and persistence stay authoritative.
    [DefaultExecutionOrder(11000)]
    public sealed class ItemBatchRenderer : MonoBehaviour
    {
        public SortingSession session;
        public Camera targetCamera;
        [Min(1)] public int minimumItems=200;
        [Min(1)] public float cellSize=16;
        public int SubmittedBatches {get;private set;}
        public int BatchedItems {get;private set;}
        public int FallbackItems {get;private set;}
        [Tooltip("Instances whose projected radius is below this many pixels are not drawn. 0 disables size culling.")]
        [Min(0)] public float minScreenRadiusPixels=2.5f;
        public int VisibleInstances {get;private set;}
        // Instances actually submitted after per-instance frustum and size culling (VisibleInstances is the pre-cull group count).
        public int DrawnInstances {get;private set;}
        [Tooltip("Instances farther than this many meters use the cheaper mesh from Resources/ItemMeshLodMap. 0 disables it.")]
        [Min(0)] public float farLodDistance=8f;
        public int FarInstances {get;private set;}
        Dictionary<Mesh,Mesh> farMeshes;
        readonly Matrix4x4[] scratchFar=new Matrix4x4[Limit];
        public double PrepareMilliseconds {get;private set;}
        const int Limit=256;
        sealed class Source {public SortItem item;public Renderer renderer;public Mesh mesh;public Material normal,search;public bool forceOff;}
        struct Key:IEquatable<Key>
        {
            public Mesh mesh;public Material material;public int x,z,layer;public ShadowCastingMode shadows;public bool receive;public LightProbeUsage probes;public uint renderingLayer;
            public bool Equals(Key k)=>mesh==k.mesh&&material==k.material&&x==k.x&&z==k.z&&layer==k.layer&&shadows==k.shadows&&receive==k.receive&&probes==k.probes&&renderingLayer==k.renderingLayer;
            public override bool Equals(object o)=>o is Key k&&Equals(k);
            public override int GetHashCode()=>HashCode.Combine(mesh,material,x,z,layer,shadows,receive,HashCode.Combine(probes,renderingLayer));
        }
        sealed class Group {public readonly List<Matrix4x4[]> chunks=new List<Matrix4x4[]>();public readonly List<Vector4> spheres=new List<Vector4>();public int count;public Bounds bounds;public void Add(Matrix4x4 matrix,Bounds b){int chunk=count/Limit;if(chunk==chunks.Count)chunks.Add(new Matrix4x4[Limit]);chunks[chunk][count%Limit]=matrix;var sphere=new Vector4(b.center.x,b.center.y,b.center.z,b.extents.magnitude);if(count<spheres.Count)spheres[count]=sphere;else spheres.Add(sphere);if(count++==0)bounds=b;else bounds.Encapsulate(b);}}
        readonly Matrix4x4[] scratch=new Matrix4x4[Limit];
        bool SphereVisible(Vector3 center,float radius)
        {
            for(int i=0;i<6;i++){var p=planes[i];if(p.normal.x*center.x+p.normal.y*center.y+p.normal.z*center.z+p.distance<-radius)return false;}
            return true;
        }
        readonly List<Source> sources=new List<Source>();
        readonly Dictionary<Key,Group> groups=new Dictionary<Key,Group>();
        readonly Dictionary<Material,Material[]> materials=new Dictionary<Material,Material[]>();
        readonly Plane[] planes=new Plane[6];
        SortItem[] registered;
        bool dirty=true;
        public void MarkDirty(){dirty=true;}
        public void Rebuild()
        {
            Release();if(!session||session.items==null)return;registered=session.items;
            var lodMap=Resources.Load<ItemMeshLodMap>("ItemMeshLodMap");farMeshes=lodMap?lodMap.Build():null;
            if(!SystemInfo.supportsInstancing||registered.Length<minimumItems)return;
            foreach(var item in registered)
            {
                if(!item||!item.visual)continue;var renderer=item.visual as MeshRenderer;var filter=renderer?renderer.GetComponent<MeshFilter>():null;
                if(item.hitbox&&item.hitbox.attachedRigidbody&&!item.hitbox.attachedRigidbody.isKinematic)continue;
                if(!renderer||!filter||!filter.sharedMesh||filter.sharedMesh.subMeshCount!=1||renderer.lightmapIndex>=0||renderer.GetComponentInParent<LODGroup>()||renderer.HasPropertyBlock())continue;
                var shared=renderer.sharedMaterials;if(shared.Length!=1||!shared[0]||shared[0].renderQueue>2500||!shared[0].HasProperty("_BaseColor"))continue;
                if(shared[0].shader.name!="Universal Render Pipeline/Unlit"&&shared[0].shader.name!="Universal Render Pipeline/Lit")continue;
                if(renderer.probeAnchor||renderer.lightProbeUsage==LightProbeUsage.UseProxyVolume||renderer.lightProbeUsage==LightProbeUsage.CustomProvided)continue;
                if(shared[0].shader.name.EndsWith("/Lit")&&renderer.reflectionProbeUsage!=ReflectionProbeUsage.Off)continue;
                if(!materials.TryGetValue(shared[0],out var pair))
                {
                    pair=new[]{new Material(shared[0]),new Material(shared[0])};foreach(var m in pair){m.enableInstancing=true;m.hideFlags=HideFlags.DontSave;}
                    pair[1].SetColor("_BaseColor",Color.Lerp(shared[0].GetColor("_BaseColor"),new Color(.4f,1f,.7f),.55f));materials.Add(shared[0],pair);
                }
                sources.Add(new Source{item=item,renderer=renderer,mesh=filter.sharedMesh,normal=pair[0],search=pair[1],forceOff=renderer.forceRenderingOff});
                item.VisualChanged+=MarkDirty;
            }
        }
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeginCamera;Camera.onPreCull+=BeforeCamera;}
        void BeginCamera(ScriptableRenderContext context,Camera camera){if(camera==targetCamera)RenderNow();}
        void BeforeCamera(Camera camera){if(GraphicsSettings.currentRenderPipeline==null&&camera==targetCamera)RenderNow();}
        void LateUpdate(){if(session&&registered!=session.items)Rebuild();if(!targetCamera&&session)targetCamera=session.player.view;}
        public void RenderNow()
        {
            long started=System.Diagnostics.Stopwatch.GetTimestamp();SubmittedBatches=VisibleInstances=DrawnInstances=FarInstances=0;
            if(!targetCamera&&session)targetCamera=session.player.view;
            if(!targetCamera||!targetCamera.isActiveAndEnabled){RestoreSources();dirty=true;return;}
            if(session&&session.SearchType)
            {
                float strength=.80f+.18f*Mathf.Sin(Time.unscaledTime*4);
                foreach(var pair in materials)pair.Value[1].SetColor("_BaseColor",Color.Lerp(pair.Key.GetColor("_BaseColor"),new Color(1f,.72f,.12f),strength));
            }
            if(dirty)
            {
            BatchedItems=0;FallbackItems=session&&session.items!=null?session.items.Length:0;
            foreach(var group in groups.Values)group.count=0;
            foreach(var source in sources)
            {
                var item=source.item;var renderer=source.renderer;if(!item||!renderer)continue;
                bool active=item.gameObject.activeInHierarchy&&renderer.enabled&&!source.forceOff;
                bool batch=active&&item.location!=ItemLocation.Hand&&!item.Moving&&!item.Hovered&&!item.Searched&&!item.IsShining&&!item.CompletionFloating;
                item.SetBatchHighlight(active&&item.location!=ItemLocation.Hand&&!item.Moving);
                renderer.forceRenderingOff=source.forceOff||batch;if(!batch)continue;
                var center=renderer.bounds.center;
                var key=new Key{mesh=source.mesh,material=item.Searched?source.search:source.normal,x=Mathf.FloorToInt(center.x/Mathf.Max(1,cellSize)),z=Mathf.FloorToInt(center.z/Mathf.Max(1,cellSize)),layer=renderer.gameObject.layer,shadows=renderer.shadowCastingMode,receive=renderer.receiveShadows,probes=renderer.lightProbeUsage,renderingLayer=renderer.renderingLayerMask};
                if(!groups.TryGetValue(key,out var group)){group=new Group();groups.Add(key,group);}
                group.Add(renderer.localToWorldMatrix,renderer.bounds);BatchedItems++;FallbackItems--;
            }
            dirty=false;
            }
            GeometryUtility.CalculateFrustumPlanes(targetCamera,planes);
            var cameraPosition=targetCamera.transform.position;
            bool sizeCull=minScreenRadiusPixels>0&&!targetCamera.orthographic;
            float pixelsPerUnit=sizeCull?targetCamera.pixelHeight*.5f/Mathf.Max(.01f,Mathf.Tan(targetCamera.fieldOfView*.5f*Mathf.Deg2Rad)):0;
            foreach(var entry in groups)
            {
                var key=entry.Key;var group=entry.Value;if(group.count==0||(key.shadows==ShadowCastingMode.Off&&!GeometryUtility.TestPlanesAABB(planes,group.bounds)))continue;
                if((targetCamera.cullingMask&(1<<key.layer))==0)continue;
                var rp=new RenderParams(key.material){camera=targetCamera,worldBounds=group.bounds,layer=key.layer,shadowCastingMode=key.shadows,receiveShadows=key.receive,lightProbeUsage=key.probes,renderingLayerMask=key.renderingLayer};
                VisibleInstances+=group.count;
                if(key.shadows!=ShadowCastingMode.Off)
                {
                    // Off-screen shadow casters can still shadow visible geometry, so keep the whole group.
                    for(int start=0;start<group.count;start+=Limit){int count=Mathf.Min(Limit,group.count-start);Graphics.RenderMeshInstanced(rp,key.mesh,0,group.chunks[start/Limit],count);SubmittedBatches++;DrawnInstances+=count;}
                    continue;
                }
                Mesh far=null;bool useFar=farLodDistance>0&&farMeshes!=null&&farMeshes.TryGetValue(key.mesh,out far);
                int pending=0,pendingFar=0;
                for(int i=0;i<group.count;i++)
                {
                    var sphere=group.spheres[i];var center=new Vector3(sphere.x,sphere.y,sphere.z);
                    if(!SphereVisible(center,sphere.w))continue;
                    float distanceSqr=(center-cameraPosition).sqrMagnitude;
                    float projected=sphere.w*pixelsPerUnit;
                    if(sizeCull&&projected*projected<minScreenRadiusPixels*minScreenRadiusPixels*distanceSqr)continue;
                    if(useFar&&distanceSqr>farLodDistance*farLodDistance)
                    {
                        scratchFar[pendingFar++]=group.chunks[i/Limit][i%Limit];
                        if(pendingFar==Limit){Graphics.RenderMeshInstanced(rp,far,0,scratchFar,pendingFar);SubmittedBatches++;DrawnInstances+=pendingFar;FarInstances+=pendingFar;pendingFar=0;}
                        continue;
                    }
                    scratch[pending++]=group.chunks[i/Limit][i%Limit];
                    if(pending==Limit){Graphics.RenderMeshInstanced(rp,key.mesh,0,scratch,pending);SubmittedBatches++;DrawnInstances+=pending;pending=0;}
                }
                if(pending>0){Graphics.RenderMeshInstanced(rp,key.mesh,0,scratch,pending);SubmittedBatches++;DrawnInstances+=pending;}
                if(pendingFar>0){Graphics.RenderMeshInstanced(rp,far,0,scratchFar,pendingFar);SubmittedBatches++;DrawnInstances+=pendingFar;FarInstances+=pendingFar;}
            }
            PrepareMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        void RestoreSources(){foreach(var source in sources){if(source.renderer)source.renderer.forceRenderingOff=source.forceOff;if(source.item)source.item.SetBatchHighlight(false);}}
        void Release(){RestoreSources();foreach(var source in sources)if(source.item)source.item.VisualChanged-=MarkDirty;sources.Clear();groups.Clear();foreach(var pair in materials.Values)foreach(var m in pair)if(m)Destroy(m);materials.Clear();registered=null;dirty=true;}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeginCamera;Camera.onPreCull-=BeforeCamera;Release();}
    }
}


