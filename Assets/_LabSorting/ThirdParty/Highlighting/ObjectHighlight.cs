using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IAmJanitor.Rendering.Highlighting
{
    public enum HighlightGeometry { ClosedMesh = 0, SurfaceEllipse = 1 }
    public enum HighlightSurfacePlane { XY = 0, XZ = 1, YZ = 2 }

    /// <summary>
    /// Object-local URP outline. Keep enabled and add/remove requests; no source material or mesh is altered.
    /// Closed geometry delegates to the URP-safe QuickOutline component; flat patches keep a
    /// small hollow surface perimeter. Hover/task/scripted ownership is independent of drawing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectHighlight : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidthPixels");
        private static readonly int PulseFrequencyId = Shader.PropertyToID("_PulseFrequency");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
        private static readonly int SurfaceModeId = Shader.PropertyToID("_SurfaceMode");
        private static readonly int CullId = Shader.PropertyToID("_OutlineCull");
        private static readonly int DepthBiasId = Shader.PropertyToID("_OutlineDepthBias");
        private static readonly int DepthTestId = Shader.PropertyToID("_OutlineZTest");
        private static Material defaultTemplate;

        [Tooltip("Empty caches this object's mesh renderers once. Excludes particle, trail and line renderers.")]
        [SerializeField] private Renderer[] targetRenderers;
        [Tooltip("Optional prepared smooth-normal mesh per target renderer, in matching array order.")]
        [SerializeField] private Mesh[] preparedOutlineMeshes;
        [SerializeField] private Material materialTemplate;
        [SerializeField] private HighlightStyle hoverStyle = HighlightStyle.Hover;
        [Tooltip("Avoid outlining inactive LOD renderers. Visibility becomes current after the source's first rendered frame.")]
        [SerializeField] private bool requireSourceVisible = true;
        [Header("Geometry")]
        [SerializeField] private HighlightGeometry geometry = HighlightGeometry.ClosedMesh;
        [Tooltip("SurfaceEllipse uses the source renderer's local bounds in this plane. Unity Quad uses XY.")]
        [SerializeField] private HighlightSurfacePlane surfacePlane = HighlightSurfacePlane.XY;

        private readonly HighlightRequestSet<HighlightStyle> requests = new HighlightRequestSet<HighlightStyle>();
        private readonly List<Proxy> proxies = new List<Proxy>(2);
        private Material instanceMaterial;
        private bool initialized;
        private bool visible;
        private bool surfaceThroughWalls;
        private Outline quickOutline;
        public bool IsHighlighted => visible;
        public bool UsesQuickOutline => geometry == HighlightGeometry.ClosedMesh;
        public bool IsThroughWallHighlighted => visible && (geometry == HighlightGeometry.ClosedMesh
            ? quickOutline != null && quickOutline.IsThroughWallHighlighted : surfaceThroughWalls);

        private sealed class Proxy
        {
            public Renderer Source;
            public Renderer Outline;
            public Mesh CachedSource;
            public Mesh OwnedSurfaceMesh;
            public SkinnedMeshRenderer SkinnedSource;
            public SkinnedMeshRenderer SkinnedOutline;
            public int BlendShapeCount;
        }

        public void Configure(Renderer[] renderers, Material template = null, Mesh[] preparedMeshes = null)
        {
            ReleaseProxies();
            targetRenderers = renderers;
            materialTemplate = template;
            preparedOutlineMeshes = preparedMeshes;
            geometry = HighlightGeometry.ClosedMesh;
            initialized = false;
            if (Application.isPlaying && isActiveAndEnabled) { Initialize(); ApplyWinner(); }
        }

        /// <summary>Outlines a flat elliptical patch using its enabled visual renderer, never a hidden legacy solid.</summary>
        public void ConfigureSurface(Renderer surface, HighlightSurfacePlane plane = HighlightSurfacePlane.XY, Material template = null)
        {
            ReleaseProxies();
            targetRenderers = surface != null ? new[] { surface } : System.Array.Empty<Renderer>();
            materialTemplate = template;
            preparedOutlineMeshes = null;
            geometry = HighlightGeometry.SurfaceEllipse;
            surfacePlane = plane;
            initialized = false;
            if (Application.isPlaying && isActiveAndEnabled) { Initialize(); ApplyWinner(); }
        }

        private void Awake()
        {
            if (Application.isPlaying && isActiveAndEnabled) Initialize();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Initialize();
            ApplyWinner();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (geometry == HighlightGeometry.ClosedMesh)
            {
                quickOutline = GetComponent<Outline>();
                if (quickOutline == null) quickOutline = gameObject.AddComponent<Outline>();
                quickOutline.ConfigureForHighlight(targetRenderers, materialTemplate, preparedOutlineMeshes, requireSourceVisible);
                return;
            }
            Material template = materialTemplate;
            if (template == null)
            {
                if (defaultTemplate == null) defaultTemplate = Resources.Load<Material>("Janitor/HighlightOutline");
                template = defaultTemplate;
            }
            if (template == null)
            {
                Debug.LogWarning("ObjectHighlight needs the Janitor URP outline material.", this);
                return;
            }
            instanceMaterial = new Material(template) { name = name + " Outline", hideFlags = HideFlags.DontSave };
            bool surfaceMode = geometry == HighlightGeometry.SurfaceEllipse;
            instanceMaterial.SetFloat(SurfaceModeId, surfaceMode ? 1 : 0);
            instanceMaterial.SetFloat(CullId, surfaceMode ? (float)CullMode.Off : (float)CullMode.Front);
            instanceMaterial.SetFloat(DepthBiasId, surfaceMode ? -1 : 0);
            bool discover = targetRenderers == null || targetRenderers.Length == 0;
            if (discover) targetRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer source = targetRenderers[i];
                if (source == null || !(source is MeshRenderer) && !(source is SkinnedMeshRenderer)) continue;
                if (discover && source.GetComponentInParent<ObjectHighlight>() != this) continue;
                bool duplicate = false;
                for (int j = 0; j < proxies.Count; j++) if (proxies[j].Source == source) { duplicate = true; break; }
                if (duplicate) continue;

                SkinnedMeshRenderer skinned = source as SkinnedMeshRenderer;
                if (surfaceMode && skinned != null) continue;
                MeshFilter filter = skinned == null ? source.GetComponent<MeshFilter>() : null;
                Mesh sourceMesh = skinned != null ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
                Mesh prepared = preparedOutlineMeshes != null && i < preparedOutlineMeshes.Length ? preparedOutlineMeshes[i] : null;
                Mesh outlineMesh = surfaceMode ? BuildSurfaceMesh(source.localBounds) : prepared != null ? prepared : OutlineMeshCache.Acquire(sourceMesh);
                if (outlineMesh == null) continue;

                var go = new GameObject("__JanitorOutline") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave, layer = source.gameObject.layer };
                go.transform.SetParent(source.transform, false);
                Renderer outline;
                if (skinned != null)
                {
                    var skinOutline = go.AddComponent<SkinnedMeshRenderer>();
                    skinOutline.sharedMesh = outlineMesh;
                    skinOutline.bones = skinned.bones;
                    skinOutline.rootBone = skinned.rootBone;
                    skinOutline.quality = skinned.quality;
                    skinOutline.updateWhenOffscreen = false;
                    Bounds localBounds = skinned.localBounds;
                    localBounds.Expand(0.5f);
                    skinOutline.localBounds = localBounds;
                    outline = skinOutline;
                }
                else
                {
                    go.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
                    outline = go.AddComponent<MeshRenderer>();
                }
                // Readable/prepared meshes normally have one combined submesh. An unreadable
                // fallback retains the authored submeshes, with one outline draw per submesh.
                var materials = new Material[outlineMesh.subMeshCount];
                for (int m = 0; m < materials.Length; m++) materials[m] = instanceMaterial;
                outline.sharedMaterials = materials;
                outline.shadowCastingMode = ShadowCastingMode.Off;
                outline.receiveShadows = false;
                outline.lightProbeUsage = LightProbeUsage.Off;
                outline.reflectionProbeUsage = ReflectionProbeUsage.Off;
                outline.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                outline.renderingLayerMask = source.renderingLayerMask;
                outline.enabled = false;
                proxies.Add(new Proxy
                {
                    Source = source,
                    Outline = outline,
                    CachedSource = !surfaceMode && prepared == null ? sourceMesh : null,
                    OwnedSurfaceMesh = surfaceMode ? outlineMesh : null,
                    SkinnedSource = skinned,
                    SkinnedOutline = outline as SkinnedMeshRenderer,
                    BlendShapeCount = skinned != null && sourceMesh != null ? Mathf.Min(sourceMesh.blendShapeCount, outlineMesh.blendShapeCount) : 0
                });
            }
        }

        private Mesh BuildSurfaceMesh(Bounds bounds)
        {
            Vector3 u = surfacePlane == HighlightSurfacePlane.YZ ? Vector3.up : Vector3.right;
            Vector3 v = surfacePlane == HighlightSurfacePlane.XY ? Vector3.up : Vector3.forward;
            float radiusU = Vector3.Dot(bounds.extents, u), radiusV = Vector3.Dot(bounds.extents, v);
            if (radiusU <= 0.00001f || radiusV <= 0.00001f) return null;
            SurfaceRingGeometry.Build(48, out SurfaceRingGeometry.Vertex[] ring, out int[] indices);
            var positions = new Vector3[ring.Length];
            var normals = new Vector3[ring.Length];
            var flags = new Vector2[ring.Length];
            for (int i = 0; i < ring.Length; i++)
            {
                SurfaceRingGeometry.Vertex vertex = ring[i];
                positions[i] = bounds.center + u * (vertex.X * radiusU) + v * (vertex.Y * radiusV);
                normals[i] = (u * (vertex.X / radiusU) + v * (vertex.Y / radiusV)).normalized;
                flags[i] = new Vector2(vertex.Outer ? 1 : 0, 0);
            }
            Mesh mesh = new Mesh { name = name + " Surface Outline", hideFlags = HideFlags.DontSave };
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = flags;
            mesh.triangles = indices;
            bounds.Expand(0.5f);
            mesh.bounds = bounds;
            mesh.UploadMeshData(true);
            return mesh;
        }

        public void SetHover(Object owner, bool active)
        {
            if (active) SetRequest(owner, HighlightChannel.Hover, hoverStyle);
            else Release(owner, HighlightChannel.Hover);
        }

        public void SetRequest(Object owner, HighlightChannel channel, HighlightStyle style, int priority = -1)
        {
            if (owner == null || !isActiveAndEnabled) return;
            // Hover never sees through walls, even if an old inspector/prefab accidentally
            // authored that flag. Task guidance is the only supported x-ray owner.
            if (channel != HighlightChannel.Task) style.throughWalls = false;
            Initialize();
            int resolvedPriority = priority >= 0 ? priority : channel == HighlightChannel.Hover ? 100 : channel == HighlightChannel.Scripted ? 200 : 10;
            if (requests.Set(owner.GetInstanceID(), channel, resolvedPriority, style)) ApplyWinner();
        }

        public void Release(Object owner, HighlightChannel channel)
        {
            if (ReferenceEquals(owner, null)) return;
            if (requests.Release(owner.GetInstanceID(), channel)) ApplyWinner();
        }

        public void ReleaseAll(Object owner)
        {
            if (ReferenceEquals(owner, null)) return;
            if (requests.ReleaseAll(owner.GetInstanceID())) ApplyWinner();
        }

        private void ApplyWinner()
        {
            bool hasWinner = requests.TryGetWinner(out HighlightStyle style);
            if (geometry == HighlightGeometry.ClosedMesh)
            {
                if (quickOutline != null) quickOutline.SetManagedHighlight(isActiveAndEnabled && hasWinner, style);
                visible = quickOutline != null && quickOutline.IsHighlighted;
                return;
            }
            visible = isActiveAndEnabled && instanceMaterial != null && hasWinner;
            surfaceThroughWalls = visible && style.throughWalls;
            if (visible)
            {
                instanceMaterial.SetColor(ColorId, style.color);
                instanceMaterial.SetFloat(WidthId, Mathf.Clamp(style.widthPixels, 0.5f, 6f));
                instanceMaterial.SetFloat(PulseFrequencyId, Mathf.Max(0.01f, style.pulseFrequency));
                instanceMaterial.SetFloat(PulseAmountId, style.pulse ? Mathf.Clamp(style.pulseAmount, 0f, 0.8f) : 0f);
                // The surface geometry is already a hollow ring; it needs no mask draw.
                instanceMaterial.SetFloat(DepthTestId, (float)(surfaceThroughWalls ? CompareFunction.Always : CompareFunction.LessEqual));
                for (int i = 0; i < proxies.Count; i++)
                    if (proxies[i].Outline != null) proxies[i].Outline.allowOcclusionWhenDynamic = !surfaceThroughWalls;
            }
            SyncVisibility();
        }

        private void LateUpdate()
        {
            if (visible && geometry == HighlightGeometry.SurfaceEllipse) SyncVisibility();
        }

        private void SyncVisibility()
        {
            for (int i = 0; i < proxies.Count; i++)
            {
                Proxy proxy = proxies[i];
                if (proxy.Outline == null) continue;
                bool active = visible && proxy.Source != null && proxy.Source.enabled
                    && !proxy.Source.forceRenderingOff && proxy.Source.gameObject.activeInHierarchy
                    && (surfaceThroughWalls || !requireSourceVisible || proxy.Source.isVisible);
                if (proxy.Outline.enabled != active) proxy.Outline.enabled = active;
                if (active && proxy.SkinnedSource != null && proxy.SkinnedOutline != null)
                    for (int shape = 0; shape < proxy.BlendShapeCount; shape++)
                        proxy.SkinnedOutline.SetBlendShapeWeight(shape, proxy.SkinnedSource.GetBlendShapeWeight(shape));
            }
        }

        private void OnDisable()
        {
            requests.Clear();
            visible = false;
            surfaceThroughWalls = false;
            if (quickOutline != null) quickOutline.SetManagedHighlight(false, default);
            SyncVisibility();
        }

        private void OnDestroy() => ReleaseProxies();

        private void ReleaseProxies()
        {
            if (quickOutline != null) quickOutline.ReleaseManagedRenderer();
            for (int i = 0; i < proxies.Count; i++)
            {
                Proxy proxy = proxies[i];
                if (proxy.Outline != null)
                {
                    proxy.Outline.enabled = false;
                    // Runtime Destroy is deferred. Detach first so a same-frame Configure(null)
                    // cannot rediscover and recursively outline an old proxy.
                    proxy.Outline.gameObject.SetActive(false);
                    proxy.Outline.transform.SetParent(null, false);
                    Destroy(proxy.Outline.gameObject);
                }
                OutlineMeshCache.Release(proxy.CachedSource);
                if (proxy.OwnedSurfaceMesh != null) Destroy(proxy.OwnedSurfaceMesh);
            }
            proxies.Clear();
            if (instanceMaterial != null) Destroy(instanceMaterial);
            instanceMaterial = null;
            visible = false;
            surfaceThroughWalls = false;
        }
    }
}
