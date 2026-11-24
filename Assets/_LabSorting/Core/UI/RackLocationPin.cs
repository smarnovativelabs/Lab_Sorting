using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    // Code-native UI vector: a round head, transparent circular hole and pointed tip.
    // The bottom center is the rack anchor. No bitmap scaling or imported icon required.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RackLocationPin:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();const int count=64;
            for(int i=0;i<count;i++)
            {
                float angle=i*Mathf.PI*2/count,x=Mathf.Cos(angle),y=Mathf.Sin(angle);
                var outer=new Vector2(x*.48f,y>=0?.1f+y*.4f:.1f-.6f*(1-Mathf.Abs(x)));
                var inner=new Vector2(x*.18f,.12f+y*.14f);
                mesh.AddVert(new Vector3(rect.center.x+outer.x*rect.width,rect.center.y+outer.y*rect.height),color,Vector2.zero);
                mesh.AddVert(new Vector3(rect.center.x+inner.x*rect.width,rect.center.y+inner.y*rect.height),color,Vector2.zero);
            }
            for(int i=0;i<count;i++){int a=i*2,b=((i+1)%count)*2;mesh.AddTriangle(a,b,a+1);mesh.AddTriangle(a+1,b,b+1);}
        }
    }
}
