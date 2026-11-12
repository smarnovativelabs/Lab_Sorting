using System;
using System.IO;
using System.Linq;
using LabSorting.Sorting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LabSorting.Editor
{
    // Adds the landing dust puff used by thrown items to the open lab scene. Safe to run again.
    public static class ThrowEffectsSetup
    {
        const string Folder="Assets/_LabSorting/Art/FX";
        const string TexturePath=Folder+"/SoftDot.png",MaterialPath=Folder+"/LandingDust.mat",ObjectName="LandingDust";

        [MenuItem("Tools/Lab Sorting/Designer/Add Throw Effects")]
        public static void Menu()
        {
            try{Apply();EditorUtility.DisplayDialog("Lab Sorting","Landing dust added and scene saved.","OK");}
            catch(Exception e){Debug.LogException(e);EditorUtility.DisplayDialog("Lab Sorting",e.Message,"OK");}
        }

        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetActiveScene();
            var feedback=Object.FindObjectsByType<SortingFeedback>(FindObjectsInactive.Include,FindObjectsSortMode.None).SingleOrDefault(f=>f.gameObject.scene==scene);
            if(!feedback)throw new InvalidOperationException("No SortingFeedback in the open scene; open the lab scene first.");
            var material=DustMaterial(SoftDot());

            var root=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="LabGameplay");
            var parent=root?root.transform:feedback.transform.root;
            var old=parent.Find(ObjectName);if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var go=new GameObject(ObjectName);Undo.RegisterCreatedObjectUndo(go,"Landing dust");go.transform.SetParent(parent,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);

            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=1;main.maxParticles=300;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.6f);main.startSpeed=new ParticleSystem.MinMaxCurve(.35f,.9f);
            main.startSize=new ParticleSystem.MinMaxCurve(.05f,.12f);main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.92f,.88f,.8f,.75f),new Color(.8f,.76f,.7f,.55f));
            main.gravityModifier=-.03f;
            var emission=ps.emission;emission.enabled=true;emission.rateOverTime=0;
            // A flat ring that kicks dust outward and slightly up from where the item touches down.
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=80;shape.radius=.06f;
            shape.rotation=new Vector3(-90,0,0);
            var drag=ps.limitVelocityOverLifetime;drag.enabled=true;drag.limit=.15f;drag.dampen=.25f;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.8f,1,2.2f));
            var color=ps.colorOverLifetime;color.enabled=true;
            var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.6f,.4f),new GradientAlphaKey(0,1)});
            color.color=fade;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortingFudge=-10;

            Undo.RecordObject(feedback,"Landing dust");feedback.dust=ps;EditorUtility.SetDirty(feedback);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save "+scene.path);
            Debug.Log("THROW_EFFECTS_ADDED scene="+scene.path);
        }

        // 64x64 soft round dot with alpha, generated once.
        static Texture2D SoftDot()
        {
            if(!File.Exists(TexturePath))
            {
                Directory.CreateDirectory(Folder);
                var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))/32f;float a=Mathf.Clamp01(1-d);a=a*a*(3-2*a);
                    texture.SetPixel(x,y,new Color(1,1,1,a));
                }
                File.WriteAllBytes(TexturePath,texture.EncodeToPNG());Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(TexturePath);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=64;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
        // URP unlit particle material, alpha blended.
        static Material DustMaterial(Texture2D texture)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(!material)
            {
                var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if(!shader)throw new InvalidOperationException("URP particle shader not found.");
                material=new Material(shader);AssetDatabase.CreateAsset(material,MaterialPath);
            }
            material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
            return material;
        }
    }
}
