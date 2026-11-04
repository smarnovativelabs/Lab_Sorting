// QuickOutline - Created by Chris Nolet. Copyright (c) 2018 Chris Nolet.
// Project-local Unity 6 / URP adaptation. Keep this script's GUID and public API intact.
// Visible outlines use the project's cached, unlit renderer instead of modifying imported
// mesh UVs/submeshes or appending materials to gameplay renderers. Legacy x-ray/stencil modes
// are retained for serialization, but fall back to OutlineVisible. Task-only x-ray is managed
// by HighlightStyle.throughWalls using a bounded colorless mask plus hollow URP rim.
using System;
using System.Collections.Generic;
using IAmJanitor.Rendering.Highlighting;
using UnityEngine;

[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    public enum Mode { OutlineAll, OutlineVisible, OutlineHidden, OutlineAndSilhouette, SilhouetteOnly }

    [SerializeField] private Mode outlineMode = Mode.OutlineVisible;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField, Range(0f, 10f)] private float outlineWidth = 2f;

    // Retain the original serialized layout for existing QuickOutline prefabs. Runtime CPU
    // mesh edits are intentionally not performed; ObjectHighlight may supply prepared meshes.
    [Serializable] private class ListVector3 { public List<Vector3> data; }
    [SerializeField, HideInInspector] private bool precomputeOutline;
    [SerializeField, HideInInspector] private List<Mesh> bakeKeys = new List<Mesh>();
    [SerializeField, HideInInspector] private List<ListVector3> bakeValues = new List<ListVector3>();

    private QuickOutlineRenderer rendererBackend;
    private bool managed;
    private bool requestedVisible;
    private bool warnedMode;
    private bool requireSourceVisible = true;
    private HighlightStyle managedStyle;

    public Mode OutlineMode
    {
        get => outlineMode;
        set { if (outlineMode == value) return; outlineMode = value; warnedMode = false; ApplyVisibility(); }
    }
    public Color OutlineColor
    {
        get => outlineColor;
        set { if (outlineColor == value) return; outlineColor = value; ApplyVisibility(); }
    }
    public float OutlineWidth
    {
        get => outlineWidth;
        set { value = Mathf.Clamp(value, 0f, 10f); if (Mathf.Approximately(outlineWidth, value)) return; outlineWidth = value; ApplyVisibility(); }
    }

    public bool IsHighlighted => rendererBackend != null && rendererBackend.IsVisible;
    public bool IsThroughWallHighlighted => rendererBackend != null && rendererBackend.IsThroughWallVisible;
    public bool HasRenderResources => rendererBackend != null && rendererBackend.HasResources;

    private void Awake()
    {
        // No material allocations or mesh reads here, including on disabled components.
        // A sibling facade owns visibility; its requests may already have arrived before Awake.
        managed |= GetComponent<ObjectHighlight>() != null;
        // Legacy standalone usage started hidden until its interaction script enabled it.
        // Never override a facade request here: component Awake ordering is unspecified.
        if (!managed) enabled = false;
    }
    private void OnEnable() => ApplyVisibility();
    private void OnDisable() => rendererBackend?.Hide();
    private void OnDestroy() { rendererBackend?.Dispose(); rendererBackend = null; }

    public void ConfigureForHighlight(Renderer[] sources, Material template, Mesh[] preparedMeshes,
        bool onlyVisibleSources = true)
    {
        managed = true;
        requestedVisible = false;
        outlineMode = Mode.OutlineVisible;
        requireSourceVisible = onlyVisibleSources;
        if (rendererBackend == null) rendererBackend = new QuickOutlineRenderer(this);
        rendererBackend.Configure(sources, template, preparedMeshes);
        if (Application.isPlaying) enabled = false;
    }

    /// <summary>The ownership facade remains enabled; only the winning request drives Outline.</summary>
    public void SetManagedHighlight(bool visible, HighlightStyle style)
    {
        managed = true;
        requestedVisible = visible;
        managedStyle = style;
        outlineColor = style.color;
        outlineWidth = style.widthPixels;
        if (!Application.isPlaying) return;
        if (enabled != visible) enabled = visible;
        ApplyVisibility();
    }

    public void ReleaseManagedRenderer()
    {
        requestedVisible = false;
        rendererBackend?.Dispose();
        rendererBackend = null;
        if (Application.isPlaying && managed) enabled = false;
    }

    private void ApplyVisibility()
    {
        if (!Application.isPlaying) return;
        bool show = isActiveAndEnabled && (!managed || requestedVisible);
        if (!show) { rendererBackend?.Hide(); return; }
        if (outlineMode != Mode.OutlineVisible && !warnedMode)
        {
            warnedMode = true;
            Debug.LogWarning("QuickOutline's mobile URP backend supports OutlineVisible only. " +
                outlineMode + " falls back to a visible outline; x-ray/stencil silhouette modes are not enabled.", this);
        }
        if (rendererBackend == null) rendererBackend = new QuickOutlineRenderer(this);
        HighlightStyle style = managed ? managedStyle : HighlightStyle.Hover;
        style.color = outlineColor;
        style.widthPixels = Mathf.Clamp(outlineWidth, 0f, 10f);
        rendererBackend.Show(style, requireSourceVisible);
    }

    private void LateUpdate()
    {
        // This component is disabled while a managed object has no request. Active outlines
        // only synchronize renderer visibility / skinned blend shapes, never mesh vertices.
        rendererBackend?.SyncVisibility();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        outlineWidth = Mathf.Clamp(outlineWidth, 0f, 10f);
        if (Application.isPlaying) ApplyVisibility();
    }
#endif
}
