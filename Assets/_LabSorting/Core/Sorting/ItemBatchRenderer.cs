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
        [Min(1)] public float cellSize=8;
        public int SubmittedBatches {get;private set;}
        public int BatchedItems {get;private set;}
        public int FallbackItems {get;private set;}
        public int VisibleInstances {get;private set;}
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
        sealed class Group {public readonly List<Matrix4x4[]> chunks=new List<Matrix4x4[]>();public int count;public Bounds bounds;public void Add(Matrix4x4 matrix,Bounds b){int chunk=count/Limit;if(chunk==chunks.Count)chunks.Add(new Matrix4x4[Limit]);chunks[chunk][count%Limit]=matrix;if(count++==0)bounds=b;else bounds.Encapsulate(b);}}
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
            long started=System.Diagnostics.Stopwatch.GetTimestamp();SubmittedBatches=VisibleInstances=0;
            if(!targetCamera&&session)targetCamera=session.player.view;
            if(!targetCamera||!targetCamera.isActiveAndEnabled){RestoreSources();dirty=true;return;}
            if(dirty)
            {
            BatchedItems=0;FallbackItems=session&&session.items!=null?session.items.Length:0;
            foreach(var group in groups.Values)group.count=0;
            foreach(var source in sources)
            {
                var item=source.item;var renderer=source.renderer;if(!item||!renderer)continue;
                bool active=item.gameObject.activeInHierarchy&&renderer.enabled&&!source.forceOff;
                bool batch=active&&item.location!=ItemLocation.Hand&&!item.Moving&&!item.Hovered;
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
            foreach(var entry in groups)
            {
                var key=entry.Key;var group=entry.Value;if(group.count==0||(key.shadows==ShadowCastingMode.Off&&!GeometryUtility.TestPlanesAABB(planes,group.bounds)))continue;
                if((targetCamera.cullingMask&(1<<key.layer))==0)continue;
                var rp=new RenderParams(key.material){camera=targetCamera,worldBounds=group.bounds,layer=key.layer,shadowCastingMode=key.shadows,receiveShadows=key.receive,lightProbeUsage=key.probes,renderingLayerMask=key.renderingLayer};
                for(int start=0;start<group.count;start+=Limit){int count=Mathf.Min(Limit,group.count-start);Graphics.RenderMeshInstanced(rp,key.mesh,0,group.chunks[start/Limit],count);SubmittedBatches++;VisibleInstances+=count;}
            }
            PrepareMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        void RestoreSources(){foreach(var source in sources){if(source.renderer)source.renderer.forceRenderingOff=source.forceOff;if(source.item)source.item.SetBatchHighlight(false);}}
        void Release(){RestoreSources();foreach(var source in sources)if(source.item)source.item.VisualChanged-=MarkDirty;sources.Clear();groups.Clear();foreach(var pair in materials.Values)foreach(var m in pair)if(m)Destroy(m);materials.Clear();registered=null;dirty=true;}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeginCamera;Camera.onPreCull-=BeforeCamera;Release();}
    }
}


