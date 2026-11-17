using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class RatLayoutInspection
    {
        public static void Capture()
        {
            var colony=Object.FindFirstObjectByType<RatColony>();var rack=colony.session.racks.Single(r=>r.ratsOnly);
            var b=rack.GetComponent<Renderer>().bounds;var text=new StringBuilder();text.AppendLine("Glass "+b+" localScale="+rack.transform.localScale+" rotation="+rack.transform.eulerAngles);
            foreach(var r in colony.session.racks.Where(r=>r!=rack))
            {
                var bounds=new Bounds(r.transform.position,Vector3.zero);
                foreach(var c in r.GetComponentsInChildren<Collider>())bounds.Encapsulate(c.bounds);
                text.AppendLine(r.name+" "+bounds);
            }
            foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if(!r.GetComponentInParent<SortItem>()&&!r.GetComponentInParent<SortRack>()&&r.bounds.size.magnitude<8&&(Vector3.Distance(r.bounds.center,b.center)<4||r.bounds.center.y>2))text.AppendLine("ENV "+r.name+" "+r.bounds);
            File.WriteAllText("Logs/RatLayout.txt",text.ToString());
            var go=new GameObject("Rat layout inspection camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.03f;camera.fieldOfView=60;
            go.transform.position=b.center+new Vector3(-2.5f,1.6f,-.7f);go.transform.LookAt(b.center);
            var rt=new RenderTexture(1200,800,24);var previous=RenderTexture.active;
            var texture=new Texture2D(1200,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1200,800),0,0);texture.Apply();File.WriteAllBytes("Logs/RatLayout.png",texture.EncodeToPNG());go.transform.position=new Vector3(1,7,-3);go.transform.LookAt(new Vector3(1,3.7f,5.7f));camera.Render();texture.ReadPixels(new Rect(0,0,1200,800),0,0);texture.Apply();File.WriteAllBytes("Logs/RatUpperLanding.png",texture.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}
