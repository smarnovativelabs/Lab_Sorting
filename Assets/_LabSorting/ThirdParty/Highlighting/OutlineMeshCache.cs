using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IAmJanitor.Rendering.Highlighting
{
    /// <summary>Private outline copies: imported shared meshes and their UV channels are never modified.</summary>
    internal static class OutlineMeshCache
    {
        private sealed class Entry { public Mesh Mesh; public int Users; public bool Owned; }
        private static readonly Dictionary<Mesh, Entry> Entries = new Dictionary<Mesh, Entry>();

        public static Mesh Acquire(Mesh source)
        {
            if (source == null) return null;
            if (Entries.TryGetValue(source, out Entry existing))
            {
                existing.Users++;
                return existing.Mesh;
            }

            Mesh outline = Build(source);
            if (outline != source) outline.UploadMeshData(true);
            Entries.Add(source, new Entry { Mesh = outline, Users = 1, Owned = outline != source });
            return outline;
        }

        public static void Release(Mesh source)
        {
            if (source == null || !Entries.TryGetValue(source, out Entry entry)) return;
            if (--entry.Users > 0) return;
            Entries.Remove(source);
            if (entry.Owned && entry.Mesh != null) Object.Destroy(entry.Mesh);
        }

        // Also used by the authoring baker; its result belongs to the caller when different from source.
        public static Mesh Build(Mesh source)
        {
            if (!source.isReadable)
            {
                Debug.LogWarning($"Outline mesh '{source.name}' is not CPU readable. Using authored normals. " +
                    "Supply a prepared outline mesh for smooth hard-edge silhouettes without runtime Read/Write memory.", source);
                return source;
            }
            for (int i = 0; i < source.subMeshCount; i++)
            {
                if (source.GetTopology(i) != MeshTopology.Triangles) return source;
            }

            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            if (positions.Length == 0 || normals.Length != positions.Length) return source;
            var averaged = new Dictionary<Vector3, Vector3>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                averaged.TryGetValue(positions[i], out Vector3 sum);
                averaged[positions[i]] = sum + normals[i];
            }
            for (int i = 0; i < positions.Length; i++) normals[i] = averaged[positions[i]].normalized;

            Mesh copy = Object.Instantiate(source);
            copy.name = source.name + " (Janitor Outline)";
            copy.hideFlags = HideFlags.DontSave;
            int[] triangles = source.triangles;
            copy.subMeshCount = 1;
            copy.SetTriangles(triangles, 0);
            copy.normals = normals;
            Bounds bounds = copy.bounds;
            bounds.Expand(0.5f);
            copy.bounds = bounds;
            return copy;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (Entry entry in Entries.Values)
                if (entry.Owned && entry.Mesh != null) Object.Destroy(entry.Mesh);
            Entries.Clear();
        }
    }
}
