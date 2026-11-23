using UnityEngine;
using UnityEngine.UI;

namespace LabSorting.Sorting
{
    // Resolution-independent silhouette: long tail, body, pointed snout and round ears.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RatSilhouetteGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Ellipse(vh, .50f, .43f, .27f, .23f);
            Ellipse(vh, .74f, .51f, .17f, .16f);
            Ellipse(vh, .66f, .69f, .09f, .11f);
            Triangle(vh, new Vector2(.78f, .63f), new Vector2(.98f, .43f), new Vector2(.73f, .37f));
            Ellipse(vh, .39f, .22f, .12f, .045f);
            Ellipse(vh, .71f, .26f, .09f, .04f);
            Vector2 last = new Vector2(.30f, .35f);
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f;
                var next = new Vector2(.30f - .25f * Mathf.Sin(t * Mathf.PI * .75f), .35f + .46f * t);
                var normal = new Vector2(-(next - last).y, (next - last).x).normalized * Mathf.Lerp(.024f, .006f, t);
                Triangle(vh, last - normal, last + normal, next + normal);
                Triangle(vh, last - normal, next + normal, next - normal);
                last = next;
            }
        }

        void Ellipse(VertexHelper vh, float x, float y, float rx, float ry)
        {
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2 / 28, b = (i + 1) * Mathf.PI * 2 / 28;
                Triangle(vh, new Vector2(x, y), new Vector2(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry), new Vector2(x + Mathf.Cos(b) * rx, y + Mathf.Sin(b) * ry));
            }
        }

        void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            int start = vh.currentVertCount;
            var rect = rectTransform.rect;
            foreach (var p in new[] { a, b, c }) vh.AddVert(new Vector3(rect.xMin + p.x * rect.width, rect.yMin + p.y * rect.height), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
