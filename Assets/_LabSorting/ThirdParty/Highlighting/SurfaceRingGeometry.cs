using System;

namespace IAmJanitor.Rendering.Highlighting
{
    /// <summary>Engine-independent, bounded hollow perimeter topology. The shader expands only outer vertices.</summary>
    internal static class SurfaceRingGeometry
    {
        internal readonly struct Vertex
        {
            public readonly float X;
            public readonly float Y;
            public readonly bool Outer;
            public Vertex(float x, float y, bool outer) { X = x; Y = y; Outer = outer; }
        }

        public static void Build(int segments, out Vertex[] vertices, out int[] triangles)
        {
            if (segments < 8 || segments > 128) throw new ArgumentOutOfRangeException(nameof(segments));
            vertices = new Vertex[segments * 2];
            triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                double angle = i * Math.PI * 2 / segments;
                float x = (float)Math.Cos(angle), y = (float)Math.Sin(angle);
                int inner = i * 2, outer = inner + 1, next = (i + 1) % segments * 2;
                vertices[inner] = new Vertex(x, y, false);
                vertices[outer] = new Vertex(x, y, true);
                int offset = i * 6;
                triangles[offset] = inner;
                triangles[offset + 1] = outer;
                triangles[offset + 2] = next + 1;
                triangles[offset + 3] = inner;
                triangles[offset + 4] = next + 1;
                triangles[offset + 5] = next;
            }
        }
    }
}
