using System;
using System.IO;
using LabSorting.Sorting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace LabSorting.Editor
{
    public static class MaterialFeedbackUpgrade
    {
        const string Folder="Assets/_LabSorting/Audio/Materials";
        // Original short procedural Foley: mono PCM, generated in the Editor, no runtime synthesis.
        static void Clip(string name,int kind)
        {
            string path=Folder+"/"+name+".wav";if(File.Exists(path))return;
            const int rate=22050;int samples=(int)(rate*(kind==3?.65:.18));
            using(var w=new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(samples*2);
                var random=new System.Random(81);
                for(int i=0;i<samples;i++)
                {
                    double t=(double)i/rate,attack=Math.Min(1,t/.003),v;
                    if(kind==3){v=0;foreach(double f in new[]{659.25,830.61,987.77})v+=Math.Sin(2*Math.PI*f*t)*Math.Exp(-t*7)/3;}
                    else {double f=kind==0?1700:kind==1?330:940;double decay=kind==1?55:28;
                        v=(Math.Sin(2*Math.PI*f*t)*.6+Math.Sin(2*Math.PI*f*(kind==0?2.71:1.47)*t)*.25)*Math.Exp(-t*decay)+(random.NextDouble()*2-1)*.2*Math.Exp(-t*120);}
                    double fade=Math.Min(1,(samples-1-i)/(rate*.012));w.Write((short)(v*attack*fade*11000));
                }
            }
        }
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop play mode");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.isDirty)throw new Exception("Save scene before upgrade");
            Directory.CreateDirectory(Folder);Clip("GlassTap",0);Clip("PlasticTap",1);Clip("MetalTap",2);Clip("RackChime",3);AssetDatabase.Refresh();
            foreach(string guid in AssetDatabase.FindAssets("t:ItemDefinition",new[]{"Assets/_LabSorting/Designer/Definitions"}))
            {
                var d=AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));string id=d.id.ToLowerInvariant();
                d.placementMaterial=id.Contains("test-tube")||id.Contains("blood-sample")?PlacementMaterial.Glass:
                    id.Contains("oxygen-cylinder")||id.Contains("battr")||id.Contains("micro-scope")||id.Contains("flashlight")?PlacementMaterial.Metal:
                    id.Contains("bottle")||id.Contains("spray")||id.Contains("tool-kit")||id.Contains("radio")||id.Contains("wakie")||id.Contains("gas-mask")?PlacementMaterial.Plastic:PlacementMaterial.Default;
                EditorUtility.SetDirty(d);
            }
            var feedback=UnityEngine.Object.FindFirstObjectByType<SortingFeedback>();
            feedback.glassPlace=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/GlassTap.wav");feedback.plasticPlace=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/PlasticTap.wav");feedback.metalPlace=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/MetalTap.wav");feedback.rackChime=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/RackChime.wav");
            EditorUtility.SetDirty(feedback);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("MATERIAL_FEEDBACK_WIRED "+scene.path);
        }
    }
}
