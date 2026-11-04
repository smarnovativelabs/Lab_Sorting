using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IAmJanitor.Rendering.Highlighting
{
    /// <summary>
    /// Rendering backend for the project's QuickOutline component. One unlit draw per prepared
    /// renderer (or per original submesh fallback), no scene-color/depth copy, and no imported
    /// mesh/material mutation. Task-only x-ray adds an unexpanded, colorless stencil mask draw;
    /// it never fills the object's interior. Resources are allocated on first use and reused.
    /// </summary>
    internal sealed class QuickOutlineRenderer
    {
        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidthPixels");
        private static readonly int PulseFrequencyId = Shader.PropertyToID("_PulseFrequency");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
        private static readonly int DepthTestId = Shader.PropertyToID("_OutlineZTest");
        private static readonly int ColorMaskId = Shader.PropertyToID("_OutlineColorMask");
        private static readonly int StencilRefId = Shader.PropertyToID("_OutlineStencilRef");
        private static readonly int StencilReadMaskId = Shader.PropertyToID("_OutlineStencilReadMask");
        private static readonly int StencilWriteMaskId = Shader.PropertyToID("_OutlineStencilWriteMask");
        private static readonly int StencilCompareId = Shader.PropertyToID("_OutlineStencilComp");
        private static readonly int StencilPassId = Shader.PropertyToID("_OutlineStencilPass");
        // URP reserves high stencil bits for deferred rendering. These four user bits are
        // reserved by this effect while drawing; independent bits keep overlapping rims intact.
        // The authored task budget is four. More requests fall back to a normal visible rim.
        private static readonly int[] StencilBits = { 8, 4, 2, 1 };
        private static readonly QuickOutlineRenderer[] StencilOwners = new QuickOutlineRenderer[4];
        private static Material defaultTemplate;
        private readonly Outline owner;
        private readonly List<Proxy> proxies = new List<Proxy>(2);
        private Renderer[] sources;
        private Material template;
        private Mesh[] prepared;
        private Material material;
        private Material maskMaterial;
        private bool initialized;
        private bool requireSourceVisible;
        private bool throughWalls;
        private bool warnedBudget;
        private int stencilBit;
        public bool IsVisible { get; private set; }
        public bool IsThroughWallVisible => IsVisible && throughWalls;
        public bool HasResources => material != null;

        private sealed class Proxy
        {
            public Renderer Source, Effect, Mask;
            public Mesh CachedSource;
            public SkinnedMeshRenderer SkinSource, SkinEffect, SkinMask;
            public int BlendShapes;
            public Material[] VisibleMaterials, XRayMaterials;
            public Mesh OutlineMesh;
        }

        public QuickOutlineRenderer(Outline owner) { this.owner = owner; }
        public void Configure(Renderer[] renderers, Material materialTemplate, Mesh[] preparedMeshes)
        { Dispose(); sources = renderers; template = materialTemplate; prepared = preparedMeshes; }

        private void Initialize()
        {
            if (initialized || owner == null) return;
            initialized = true;
            Material resolved = template;
            if (resolved == null)
            {
                if (defaultTemplate == null) defaultTemplate = Resources.Load<Material>("Janitor/HighlightOutline");
                resolved = defaultTemplate;
            }
            if (resolved == null)
            { Debug.LogWarning("QuickOutline needs the Janitor URP outline material.", owner); return; }
            material = new Material(resolved) { name = owner.name + " QuickOutline URP", hideFlags = HideFlags.DontSave };
            material.SetFloat("_SurfaceMode", 0f);
            material.SetFloat("_OutlineCull", (float)CullMode.Front);
            material.SetFloat("_OutlineDepthBias", 0f);
            bool discover = sources == null || sources.Length == 0;
            if (discover) sources = owner.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                Renderer source = sources[i];
                if (source == null || !(source is MeshRenderer) && !(source is SkinnedMeshRenderer)) continue;
                if (discover && source.GetComponentInParent<Outline>() != owner) continue;
                bool duplicate = false;
                foreach (Proxy existing in proxies) if (existing.Source == source) { duplicate = true; break; }
                if (duplicate) continue;
                SkinnedMeshRenderer skin = source as SkinnedMeshRenderer;
                MeshFilter filter = skin == null ? source.GetComponent<MeshFilter>() : null;
                Mesh sourceMesh = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
                Mesh preparedMesh = prepared != null && i < prepared.Length ? prepared[i] : null;
                Mesh mesh = preparedMesh != null ? preparedMesh : OutlineMeshCache.Acquire(sourceMesh);
                if (mesh == null) continue;
                var holder = new GameObject("__QuickOutlineURP")
                { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave, layer = source.gameObject.layer };
                holder.transform.SetParent(source.transform, false);
                Renderer effect;
                SkinnedMeshRenderer skinEffect = null;
                if (skin != null)
                {
                    skinEffect = holder.AddComponent<SkinnedMeshRenderer>();
                    skinEffect.sharedMesh = mesh; skinEffect.bones = skin.bones; skinEffect.rootBone = skin.rootBone;
                    skinEffect.quality = skin.quality; skinEffect.updateWhenOffscreen = false;
                    effect = skinEffect;
                }
                else
                {
                    holder.AddComponent<MeshFilter>().sharedMesh = mesh;
                    effect = holder.AddComponent<MeshRenderer>();
                }
                Bounds bounds = source.localBounds; bounds.Expand(.5f); effect.localBounds = bounds;
                // Fallback meshes can remain GPU-only. Cover ALL submeshes without combining
                // indices or writing UV channels; prepared meshes reduce this to one draw.
                var materials = new Material[mesh.subMeshCount];
                for (int m = 0; m < materials.Length; m++) materials[m] = material;
                effect.sharedMaterials = materials;
                effect.shadowCastingMode = ShadowCastingMode.Off; effect.receiveShadows = false;
                effect.lightProbeUsage = LightProbeUsage.Off; effect.reflectionProbeUsage = ReflectionProbeUsage.Off;
                effect.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                effect.renderingLayerMask = source.renderingLayerMask;
                effect.enabled = false;
                proxies.Add(new Proxy { Source = source, Effect = effect, VisibleMaterials = materials,
                    OutlineMesh = mesh,
                    CachedSource = preparedMesh == null ? sourceMesh : null,
                    SkinSource = skin, SkinEffect = skinEffect,
                    BlendShapes = skin != null && sourceMesh != null ? Mathf.Min(sourceMesh.blendShapeCount, mesh.blendShapeCount) : 0 });
            }
        }

        public void Show(HighlightStyle style, bool onlyVisibleSources)
        {
            Initialize();
            requireSourceVisible = onlyVisibleSources;
            IsVisible = material != null && style.widthPixels > 0f;
            bool wantsXRay = IsVisible && style.throughWalls && material.HasProperty(DepthTestId);
            if (wantsXRay && TryAcquireStencil()) EnsureMaskResources();
            else
            {
                if (wantsXRay && !warnedBudget)
                {
                    warnedBudget = true;
                    Debug.LogWarning("Through-wall outlines support four nearby closed-mesh targets. " +
                        "This extra target keeps a normal visible outline until a slot is free.", owner);
                }
                ReleaseStencil();
            }
            bool nextThroughWalls = wantsXRay && stencilBit != 0 && maskMaterial != null;
            if (throughWalls != nextThroughWalls)
            {
                throughWalls = nextThroughWalls;
                foreach (Proxy proxy in proxies)
                {
                    if (proxy.Effect == null) continue;
                    proxy.Effect.sharedMaterials = throughWalls ? proxy.XRayMaterials : proxy.VisibleMaterials;
                    // Frustum culling is retained. Only baked/dynamic occlusion is bypassed
                    // for the task draw, never for its authored source renderer.
                    proxy.Effect.allowOcclusionWhenDynamic = !throughWalls;
                }
            }
            if (material != null)
            {
                material.SetColor(ColorId, style.color);
                material.SetFloat(WidthId, Mathf.Clamp(style.widthPixels, 0f, 10f));
                material.SetFloat(PulseFrequencyId, Mathf.Max(.01f, style.pulseFrequency));
                material.SetFloat(PulseAmountId, style.pulse ? Mathf.Clamp(style.pulseAmount, 0f, .8f) : 0f);
                material.SetFloat(DepthTestId, (float)(throughWalls ? CompareFunction.Always : CompareFunction.LessEqual));
                material.SetFloat(ColorMaskId, (float)ColorWriteMask.All);
                material.SetFloat(StencilRefId, stencilBit);
                material.SetFloat(StencilReadMaskId, throughWalls ? stencilBit : 0);
                material.SetFloat(StencilWriteMaskId, 0);
                material.SetFloat(StencilCompareId, (float)(throughWalls ? CompareFunction.NotEqual : CompareFunction.Always));
                material.SetFloat(StencilPassId, (float)StencilOp.Keep);
            }
            if (throughWalls)
            {
                maskMaterial.SetFloat(StencilRefId, stencilBit);
                maskMaterial.SetFloat(StencilReadMaskId, stencilBit);
                maskMaterial.SetFloat(StencilWriteMaskId, stencilBit);
            }
            SyncVisibility();
        }

        private bool TryAcquireStencil()
        {
            for (int i = 0; i < StencilOwners.Length; i++)
                if (ReferenceEquals(StencilOwners[i], this)) { stencilBit = StencilBits[i]; return true; }
            for (int i = 0; i < StencilOwners.Length; i++)
            {
                if (StencilOwners[i] != null) continue;
                StencilOwners[i] = this;
                stencilBit = StencilBits[i];
                return true;
            }
            stencilBit = 0;
            return false;
        }

        private void ReleaseStencil()
        {
            for (int i = 0; i < StencilOwners.Length; i++)
                if (ReferenceEquals(StencilOwners[i], this)) StencilOwners[i] = null;
            stencilBit = 0;
        }

        private void EnsureMaskResources()
        {
            if (maskMaterial != null) return;
            // The existing referenced shader has a configurable color/stencil/depth state,
            // so a separate Shader.Find/Resources dependency cannot be stripped from a build.
            maskMaterial = new Material(material) { name = owner.name + " Task Outline Mask", hideFlags = HideFlags.DontSave };
            maskMaterial.renderQueue = material.renderQueue - 1;
            maskMaterial.SetFloat("_OutlineCull", (float)CullMode.Off);
            maskMaterial.SetFloat("_OutlineDepthBias", 0f);
            maskMaterial.SetFloat(WidthId, 0f);
            maskMaterial.SetFloat(DepthTestId, (float)CompareFunction.Always);
            maskMaterial.SetFloat(ColorMaskId, 0f);
            maskMaterial.SetFloat(StencilCompareId, (float)CompareFunction.Always);
            maskMaterial.SetFloat(StencilPassId, (float)StencilOp.Replace);
            foreach (Proxy proxy in proxies)
            {
                if (proxy.Effect == null) continue;
                if (proxy.OutlineMesh.subMeshCount == 1)
                {
                    // Unity draws an excess material on the last submesh. Prepared/cached
                    // single-submesh geometry therefore needs no extra GameObject/renderer.
                    proxy.XRayMaterials = new[] { maskMaterial, material };
                    continue;
                }
                // An unreadable multi-submesh fallback cannot be combined on CPU. Only this
                // uncommon path needs a separate, pooled mask renderer covering every submesh.
                var holder = new GameObject("__QuickOutlineMaskURP")
                { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave, layer = proxy.Source.gameObject.layer };
                holder.transform.SetParent(proxy.Source.transform, false);
                if (proxy.SkinSource != null)
                {
                    proxy.SkinMask = holder.AddComponent<SkinnedMeshRenderer>();
                    proxy.SkinMask.sharedMesh = proxy.OutlineMesh;
                    proxy.SkinMask.bones = proxy.SkinSource.bones;
                    proxy.SkinMask.rootBone = proxy.SkinSource.rootBone;
                    proxy.SkinMask.quality = proxy.SkinSource.quality;
                    proxy.SkinMask.updateWhenOffscreen = false;
                    proxy.Mask = proxy.SkinMask;
                }
                else
                {
                    holder.AddComponent<MeshFilter>().sharedMesh = proxy.OutlineMesh;
                    proxy.Mask = holder.AddComponent<MeshRenderer>();
                }
                var maskMaterials = new Material[proxy.OutlineMesh.subMeshCount];
                for (int i = 0; i < maskMaterials.Length; i++) maskMaterials[i] = maskMaterial;
                proxy.Mask.sharedMaterials = maskMaterials;
                proxy.Mask.localBounds = proxy.Effect.localBounds;
                proxy.Mask.shadowCastingMode = ShadowCastingMode.Off;
                proxy.Mask.receiveShadows = false;
                proxy.Mask.lightProbeUsage = LightProbeUsage.Off;
                proxy.Mask.reflectionProbeUsage = ReflectionProbeUsage.Off;
                proxy.Mask.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                proxy.Mask.renderingLayerMask = proxy.Source.renderingLayerMask;
                proxy.Mask.allowOcclusionWhenDynamic = false;
                proxy.Mask.enabled = false;
                proxy.XRayMaterials = proxy.VisibleMaterials;
            }
        }

        public void Hide() { IsVisible = false; ReleaseStencil(); SyncVisibility(); }
        public void SyncVisibility()
        {
            foreach (Proxy proxy in proxies)
            {
                if (proxy.Effect == null) continue;
                bool active = IsVisible && owner != null && owner.isActiveAndEnabled
                    && proxy.Source != null && proxy.Source.enabled && !proxy.Source.forceRenderingOff
                    && proxy.Source.gameObject.activeInHierarchy && (throughWalls || !requireSourceVisible || proxy.Source.isVisible);
                if (proxy.Effect.enabled != active) proxy.Effect.enabled = active;
                if (proxy.Mask != null && proxy.Mask.enabled != (active && throughWalls)) proxy.Mask.enabled = active && throughWalls;
                if (active && proxy.SkinSource != null && proxy.SkinEffect != null)
                    for (int i = 0; i < proxy.BlendShapes; i++)
                    {
                        proxy.SkinEffect.SetBlendShapeWeight(i, proxy.SkinSource.GetBlendShapeWeight(i));
                        if (proxy.SkinMask != null && throughWalls)
                            proxy.SkinMask.SetBlendShapeWeight(i, proxy.SkinSource.GetBlendShapeWeight(i));
                    }
            }
        }
        public void Dispose()
        {
            IsVisible = false;
            ReleaseStencil();
            foreach (Proxy proxy in proxies)
            {
                if (proxy.Effect != null)
                {
                    proxy.Effect.enabled = false;
                    proxy.Effect.gameObject.SetActive(false);
                    proxy.Effect.transform.SetParent(null, false);
                    Object.Destroy(proxy.Effect.gameObject);
                }
                if (proxy.Mask != null)
                {
                    proxy.Mask.enabled = false;
                    proxy.Mask.gameObject.SetActive(false);
                    proxy.Mask.transform.SetParent(null, false);
                    Object.Destroy(proxy.Mask.gameObject);
                }
                OutlineMeshCache.Release(proxy.CachedSource);
            }
            proxies.Clear();
            if (material != null) Object.Destroy(material);
            if (maskMaterial != null) Object.Destroy(maskMaterial);
            material = null; maskMaterial = null; initialized = false; throughWalls = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStencilSlots() => System.Array.Clear(StencilOwners, 0, StencilOwners.Length);
    }
}
