using UnityEngine;
namespace LabSorting.UI {
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FocusCorners : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();float arm=Mathf.Min(14,Mathf.Min(r.width,r.height)*.25f);
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            {
                float px=x==0?r.xMin:r.xMax,py=y==0?r.yMin:r.yMax,sx=x==0?1:-1,sy=y==0?1:-1;
                Quad(vh,new Rect(px+(sx<0?-arm:0),py+(sy<0?-2:0),arm,2));Quad(vh,new Rect(px+(sx<0?-2:0),py+(sy<0?-arm:0),2,arm));
            }
        }
        void Quad(UnityEngine.UI.VertexHelper vh,Rect r){int i=vh.currentVertCount;vh.AddVert(new Vector2(r.xMin,r.yMin),color,Vector2.zero);vh.AddVert(new Vector2(r.xMin,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMin),color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
    }
}

