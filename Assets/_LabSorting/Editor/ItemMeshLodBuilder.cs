#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LabSorting.Sorting;

namespace LabSorting.EditorTools
{
    // Builds a "far" mesh per designer item mesh using Unity's mesh LOD generator, then writes
    // Resources/ItemMeshLodMap.asset. The full meshes are never modified.
    public static class ItemMeshLodBuilder
    {
        const string SourceFolder = "Assets/_LabSorting/Designer/Meshes";
        const string LodFolder = SourceFolder + "/Lod";
        const string MapPath = "Assets/Resources/ItemMeshLodMap.asset";
        const int MinimumTriangles = 150;
        const float TargetFraction = .4f;
        // "Mid" mesh (ItemBatchRenderer.midLodDistance to farLodDistance): about half the triangles, only for heavy meshes.
        const int MidMinimumTriangles = 400;
        const float MidFraction = .55f;
        // Side-by-side renders at 2-3 m (2026-11-27) showed these simplified meshes with broken UVs or lost parts.
        // Gas mask and oxygen cylinder have no level between full and far, and their far mesh looked wrong up close.
        static readonly HashSet<string> MidExcluded = new HashSet<string> { "micro-scope", "micro-scope-big", "bandage-rolls-1", "thermal-blanket-2" };

        [MenuItem("Lab Sorting/Build Item Mesh LODs")]
        public static void Build()
        {
            Directory.CreateDirectory(LodFolder);
            var entries = new List<ItemMeshLodMap.Entry>();
            long fullTotal = 0, farTotal = 0, midTotal = 0;int midSources = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { SourceFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(LodFolder)) continue;
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!source || source.subMeshCount != 1) continue;
                int baseTriangles = (int)(source.GetIndexCount(0) / 3);
                if (baseTriangles < MinimumTriangles) continue;

                var probe = Object.Instantiate(source);
                try
                {
                    MeshLodUtility.GenerateMeshLods(probe, 8);
                    int pick = -1, pickTriangles = 0;
                    for (int lod = 1; lod < probe.lodCount; lod++)
                    {
                        int triangles = probe.GetIndices(0, lod).Length / 3;
                        if (triangles <= baseTriangles * TargetFraction) { pick = lod; pickTriangles = triangles; break; }
                    }
                    if (pick < 0) continue;

                    var far = Save(Compact(probe, probe.GetIndices(0, pick), source.name + "-far"));
                    Mesh mid = null;
                    if (baseTriangles >= MidMinimumTriangles && !MidExcluded.Contains(source.name))
                        for (int lod = 1; lod < pick; lod++)
                        {
                            int triangles = probe.GetIndices(0, lod).Length / 3;
                            if (triangles <= baseTriangles * MidFraction) { mid = Save(Compact(probe, probe.GetIndices(0, lod), source.name + "-mid")); midTotal += triangles; midSources++; break; }
                        }
                    entries.Add(new ItemMeshLodMap.Entry { full = source, far = far, mid = mid });
                    fullTotal += baseTriangles; farTotal += pickTriangles;
                }
                finally { Object.DestroyImmediate(probe); }
            }

            var map = AssetDatabase.LoadAssetAtPath<ItemMeshLodMap>(MapPath);
            if (!map)
            {
                map = ScriptableObject.CreateInstance<ItemMeshLodMap>();
                AssetDatabase.CreateAsset(map, MapPath);
            }
            map.entries = entries.OrderBy(e => e.full.name).ToArray();
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            Debug.Log("Item mesh LODs: " + entries.Count + " meshes, " + fullTotal + " -> " + farTotal + " far triangles per set of one each; " + midSources + " mid meshes, " + midTotal + " mid triangles.");
        }

        // Writes into the existing asset when there is one, so GUIDs and references stay stable.
        static Mesh Save(Mesh mesh)
        {
            var path = LodFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); return existing; }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // Copies only the vertices the chosen LOD index buffer references.
        static Mesh Compact(Mesh source, int[] indices, string name)
        {
            var remap = new Dictionary<int, int>();
            var order = new List<int>();
            var remapped = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                if (!remap.TryGetValue(indices[i], out int target)) { target = order.Count; remap.Add(indices[i], target); order.Add(indices[i]); }
                remapped[i] = target;
            }

            T[] Pick<T>(T[] all) => all == null || all.Length == 0 ? null : order.Select(v => all[v]).ToArray();
            var mesh = new Mesh { name = name };
            mesh.vertices = Pick(source.vertices);
            mesh.normals = Pick(source.normals);
            mesh.tangents = Pick(source.tangents);
            mesh.colors32 = Pick(source.colors32);
            for (int channel = 0; channel < 4; channel++)
            {
                var uvs = new List<Vector2>();
                source.GetUVs(channel, uvs);
                if (uvs.Count == source.vertexCount) mesh.SetUVs(channel, order.Select(v => uvs[v]).ToList());
            }
            mesh.SetIndices(remapped, MeshTopology.Triangles, 0);
            mesh.bounds = source.bounds;
            return mesh;
        }
    }
}
#endif
