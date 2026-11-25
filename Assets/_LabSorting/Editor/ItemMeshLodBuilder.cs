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

        [MenuItem("Lab Sorting/Build Item Mesh LODs")]
        public static void Build()
        {
            Directory.CreateDirectory(LodFolder);
            var entries = new List<ItemMeshLodMap.Entry>();
            long fullTotal = 0, farTotal = 0;
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

                    var far = Compact(probe, probe.GetIndices(0, pick), source.name + "-far");
                    var farPath = LodFolder + "/" + source.name + "-far.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(farPath);
                    if (existing) { EditorUtility.CopySerialized(far, existing); Object.DestroyImmediate(far); far = existing; }
                    else AssetDatabase.CreateAsset(far, farPath);
                    entries.Add(new ItemMeshLodMap.Entry { full = source, far = far });
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
            Debug.Log("Item mesh LODs: " + entries.Count + " meshes, " + fullTotal + " -> " + farTotal + " triangles per set of one each.");
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
