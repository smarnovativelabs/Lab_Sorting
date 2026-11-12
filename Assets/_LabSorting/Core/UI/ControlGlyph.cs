using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    // Original lightweight vector controls: one uGUI mesh, no textures or runtime materials.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ControlGlyph : MaskableGraphic
    {
        public enum Symbol { Pick, Place, Throw, Search, Magnet, Slots, List, Settings, Cash, Joystick, Thumb }
        public Symbol symbol;
        public Color accent=new Color(.3f,.95f,.9f);
        VertexHelper mesh;
        Vector2 center;float radius;Color stroke;
        void Tri(Vector2 a,Vector2 b,Vector2 c,Color tint){int i=mesh.currentVertCount;mesh.AddVert(a,tint,Vector2.zero);mesh.AddVert(b,tint,Vector2.zero);mesh.AddVert(c,tint,Vector2.zero);mesh.AddTriangle(i,i+1,i+2);}
        Vector2 P(float x,float y)=>center+new Vector2(x,y)*radius;
        void Line(float x,float y,float u,float v,float width=.07f){var a=P(x,y);var b=P(u,v);var n=new Vector2(-(b-a).y,(b-a).x).normalized*radius*width*.5f;Tri(a-n,a+n,b+n,stroke);Tri(a-n,b+n,b-n,stroke);}
        void Ring(float x,float y,float r,float start=0,float end=360,float width=.07f){int count=Mathf.CeilToInt((end-start)/12);for(int i=0;i<count;i++){float a=Mathf.Lerp(start,end,i/(float)count)*Mathf.Deg2Rad,b=Mathf.Lerp(start,end,(i+1f)/count)*Mathf.Deg2Rad;Line(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r,x+Mathf.Cos(b)*r,y+Mathf.Sin(b)*r,width);}}
        void Box(float x,float y,float w,float h){Line(x,y,x+w,y);Line(x+w,y,x+w,y+h);Line(x+w,y+h,x,y+h);Line(x,y+h,x,y);}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();mesh=vh;var r=GetPixelAdjustedRect();center=r.center;radius=Mathf.Min(r.width,r.height)*.5f;
            for(int i=0;i<40;i++){float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;Tri(center,P(Mathf.Cos(a)*.97f,Mathf.Sin(a)*.97f),P(Mathf.Cos(b)*.97f,Mathf.Sin(b)*.97f),new Color(.025f,.085f,.12f,color.a*.94f));}
            stroke=accent*color;Ring(0,0,.91f,0,360,.035f);stroke=color;
            switch(symbol)
            {
                case Symbol.Joystick:Ring(0,0,.62f,0,360,.025f);for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Line(Mathf.Cos(a)*.72f,Mathf.Sin(a)*.72f,Mathf.Cos(a)*.81f,Mathf.Sin(a)*.81f,.04f);}break;
                case Symbol.Thumb:Ring(0,0,.7f,0,360,.1f);break;
                case Symbol.Search:Ring(-.1f,.1f,.28f);Line(.1f,-.1f,.42f,-.42f,.11f);break;
                case Symbol.Magnet:Ring(0,0,.32f,180,360,.17f);Line(-.32f,0,-.32f,.38f,.17f);Line(.32f,0,.32f,.38f,.17f);break;
                case Symbol.Place:Box(-.48f,-.4f,.96f,.75f);Line(-.48f,-.06f,.48f,-.06f);Line(0,.5f,0,.08f);Line(0,.08f,-.13f,.22f);Line(0,.08f,.13f,.22f);break;
                case Symbol.Slots:Box(-.4f,-.4f,.8f,.65f);Line(-.25f,.25f,-.25f,.4f);Line(-.25f,.4f,.25f,.4f);Line(.25f,.4f,.25f,.25f);Line(-.18f,-.07f,.18f,-.07f);Line(0,-.25f,0,.1f);break;
                case Symbol.Throw:Box(-.43f,-.38f,.24f,.24f);Ring(-.05f,-.2f,.42f,15,150);Line(.35f,-.09f,.46f,.12f);Line(.35f,-.09f,.16f,.02f);break;
                case Symbol.Pick:Line(-.25f,-.38f,.18f,-.38f);Line(.18f,-.38f,.4f,-.05f);Line(.4f,-.05f,.3f,.02f);Line(.3f,.02f,.15f,-.08f);Line(.15f,-.08f,.15f,.42f);Line(.15f,.42f,0,.42f);Line(0,.42f,0,.08f);Line(0,.08f,-.3f,.13f);Line(-.3f,.13f,-.38f,-.12f);Line(-.38f,-.12f,-.25f,-.38f);break;
                case Symbol.List:for(int i=0;i<3;i++){float y=.3f-i*.3f;Line(-.18f,y,.4f,y);Line(-.42f,y,-.32f,y,.1f);}break;
                case Symbol.Settings:Ring(0,0,.25f);for(int i=0;i<8;i++){float a=i*Mathf.PI*.25f;Line(Mathf.Cos(a)*.31f,Mathf.Sin(a)*.31f,Mathf.Cos(a)*.46f,Mathf.Sin(a)*.46f,.13f);}break;
                case Symbol.Cash:Box(-.5f,-.27f,1,.54f);Ring(0,0,.15f);Line(-.36f,0,-.25f,0);Line(.25f,0,.36f,0);break;
            }
        }
    }
}

