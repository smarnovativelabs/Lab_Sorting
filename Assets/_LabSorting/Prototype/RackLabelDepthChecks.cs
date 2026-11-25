#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using LabSorting.Sorting;

namespace LabSorting.Prototype
{
    // Renders a rack label with a wall in front of it, once with Unity's default text shader (control) and once with
    // the depth-tested one. Run in the real Lab scene in Play Mode.
    public static class RackLabelDepthChecks
    {
        static void Check(bool ok,string text){Debug.Log("LABELDEPTH "+(ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception("LABELDEPTH: "+text);}
        // White pixels in the middle of the frame: the label text is white on a black background.
        static int WhitePixels(Camera cam,RenderTexture rt,Texture2D tex)
        {
            cam.targetTexture=rt;cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();RenderTexture.active=previous;cam.targetTexture=null;
            int count=0;
            for(int y=rt.height*3/8;y<rt.height*5/8;y++)for(int x=rt.width*3/8;x<rt.width*5/8;x++){var c=tex.GetPixel(x,y);if(c.r>.85f&&c.g>.85f&&c.b>.85f)count++;}
            return count;
        }
        public static IEnumerator Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Label depth checks require Play Mode");
            var s=UnityEngine.Object.FindFirstObjectByType<SortingSession>();while(!s.Initialized)yield return null;
            var racks=UnityEngine.Object.FindObjectsByType<SortRack>(FindObjectsSortMode.None).Where(r=>r.label&&r.label.gameObject.activeInHierarchy).ToArray();
            Check(racks.Length>0,"Scene has rack labels");
            foreach(var rack in racks)Check(RackLabelDepth.IsDepthTested(rack.label),"Depth-tested label material on "+rack.name);
            var go=new GameObject("Label depth camera");var cam=go.AddComponent<Camera>();cam.CopyFrom(s.player.view);
            cam.fieldOfView=35;cam.nearClipPlane=.05f;cam.cullingMask=~0;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.allowMSAA=false;
            var rt=new RenderTexture(256,256,24);var tex=new Texture2D(256,256,TextureFormat.RGB24,false);
            var blackShader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");
            var blackMaterial=new Material(blackShader);blackMaterial.color=Color.black;if(blackMaterial.HasProperty("_BaseColor"))blackMaterial.SetColor("_BaseColor",Color.black);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Label depth wall";wall.GetComponent<MeshRenderer>().sharedMaterial=blackMaterial;
            UnityEngine.Object.Destroy(wall.GetComponent<Collider>());wall.SetActive(false);
            var text=racks[0].label.GetComponent<MeshRenderer>().sharedMaterial;var defaultShader=Shader.Find("GUI/Text Shader");
            try
            {
                // Find a label that reads clearly from some side with nothing in front of it.
                TextMesh chosen=null;int baseline=0;
                foreach(var rack in racks)
                {
                    foreach(float side in new[]{-1f,1f})
                    {
                        var label=rack.label;cam.transform.position=label.transform.position+label.transform.forward*side*1.1f;cam.transform.LookAt(label.transform.position);
                        yield return null;int count=WhitePixels(cam,rt,tex);
                        if(count>baseline){baseline=count;chosen=label;if(count>=40)break;}
                    }
                    if(baseline>=40)break;
                }
                Check(chosen!=null&&baseline>=20,"A rack label is clearly visible without a wall ("+baseline+" white pixels)");
                // The camera sits where the best view was found; put a wall halfway between it and the label.
                foreach(float side in new[]{-1f,1f})
                {
                    cam.transform.position=chosen.transform.position+chosen.transform.forward*side*1.1f;cam.transform.LookAt(chosen.transform.position);
                    yield return null;if(WhitePixels(cam,rt,tex)>=baseline*.8f)break;
                }
                wall.transform.position=Vector3.Lerp(cam.transform.position,chosen.transform.position,.5f);wall.transform.rotation=cam.transform.rotation;wall.transform.localScale=new Vector3(6,6,.05f);wall.SetActive(true);
                text.shader=defaultShader;yield return null;
                int control=WhitePixels(cam,rt,tex);
                Check(control>=10,"Control: Unity's default text shader shows the label through a wall ("+control+" white pixels)");
                RackLabelDepth.Apply(chosen);yield return null;
                int hidden=WhitePixels(cam,rt,tex);
                Check(hidden==0,"Depth-tested label is hidden behind the wall ("+hidden+" white pixels)");
                wall.SetActive(false);yield return null;
                int visible=WhitePixels(cam,rt,tex);
                Check(visible>=baseline*.8f,"Label is visible again once the wall is gone ("+visible+" of "+baseline+")");
            }
            finally
            {
                foreach(var rack in racks)RackLabelDepth.Apply(rack.label);
                UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(wall);UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(tex);UnityEngine.Object.Destroy(blackMaterial);
            }
        }
    }
}
#endif
