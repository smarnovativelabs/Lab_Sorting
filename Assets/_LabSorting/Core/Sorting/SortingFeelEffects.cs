using System.Collections.Generic;
using UnityEngine;

namespace LabSorting.Sorting
{
    public sealed partial class SortingFeedback
    {
        readonly List<SortRack> awaitingCelebration=new List<SortRack>();
        readonly List<SortItem> floatingItems=new List<SortItem>();
        public bool CompletionPending => awaitingCelebration.Count>0 || floatingItems.Exists(i=>i&&i.CompletionFloating);
        void FloatRack(SortRack rack)
        {
            floatingItems.RemoveAll(i=>!i||!i.CompletionFloating);
            for(int i=0;i<rack.contents.Length;i++)
            {
                var item=rack.contents[i];if(!item)continue;
                rack.CompletionWave(i,out float delay,out float height);
                item.PlayCompletionFloat(delay,height);floatingItems.Add(item);
            }
        }
        ParticleSystem sparkles;
        Material sparkleMaterial;
        Texture2D sparkleTexture;
        float nextSparkle;
        public int PickupBursts {get;private set;}
        public int PlacementArrivals {get;private set;}

        void PickupSparkle(SortItem item,Vector3 position)
        { if(!isActiveAndEnabled||!item)return;PickupBursts++;EmitSparkles(position,5); }
        void PlacementArrival(SortItem item)
        {
            if(!isActiveAndEnabled||!item)return;
            // Resolve the material at arrival, not from whichever item was most recently reserved.
            lastPlaced=item.definition;PlacementArrivals++;
            item.ShineAtPlacement();
            var bounds=item.hitbox.bounds;
            EmitSparkles(new Vector3(bounds.center.x,bounds.min.y+.025f,bounds.center.z),4);
        }
        void QueueCelebration(SortRack rack)
        {if(rack&&!awaitingCelebration.Contains(rack))awaitingCelebration.Add(rack);}
        void LateUpdate()
        {
            for(int i=awaitingCelebration.Count-1;i>=0;i--)
            {
                var rack=awaitingCelebration[i];
                if(!rack){awaitingCelebration.RemoveAt(i);continue;}
                bool moving=false;foreach(var item in rack.contents)if(item&&item.Moving){moving=true;break;}
                if(moving)continue;
                awaitingCelebration.RemoveAt(i);
                if(rack.Count==rack.Capacity&&(!session||!session.player.blocked))Celebrate(rack);
            }
        }
        void ResetPresentation()
        {awaitingCelebration.Clear();foreach(var item in floatingItems)if(item)item.StopCompletionFloat();floatingItems.Clear();StopCelebration();if(sparkles)sparkles.Clear(true);}
        void EmitSparkles(Vector3 position,int count)
        {
            if(Time.unscaledTime<nextSparkle)return;nextSparkle=Time.unscaledTime+.07f;
            if(!sparkles)
            {
                var sourceMaterial=dust?dust.GetComponent<ParticleSystemRenderer>().sharedMaterial:null;
                var shader=sourceMaterial?sourceMaterial.shader:Shader.Find("Universal Render Pipeline/Particles/Unlit");if(!shader)return;
                sparkleTexture=new Texture2D(16,16,TextureFormat.RGBA32,false){name="Soft interaction sparkle",hideFlags=HideFlags.DontSave};
                for(int y=0;y<16;y++)for(int x=0;x<16;x++){float r=Vector2.Distance(new Vector2(x,y),new Vector2(7.5f,7.5f))/7.5f;sparkleTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1-r*r)));}
                sparkleTexture.Apply();
                sparkleMaterial=new Material(shader){name="Interaction sparkles",hideFlags=HideFlags.DontSave};
                sparkleMaterial.SetTexture("_BaseMap",sparkleTexture);sparkleMaterial.SetColor("_BaseColor",Color.white);
                sparkleMaterial.SetFloat("_Surface",1);sparkleMaterial.SetFloat("_ZWrite",0);
                sparkleMaterial.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sparkleMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                sparkleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");sparkleMaterial.renderQueue=3000;
                var go=new GameObject("Interaction sparkles (shared pool)");go.transform.SetParent(transform,false);
                sparkles=go.AddComponent<ParticleSystem>();sparkles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=sparkles.main;main.loop=false;main.playOnAwake=false;main.startLifetime=.24f;main.startSpeed=.22f;main.startSize=.016f;main.maxParticles=64;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=sparkles.emission;emission.enabled=false;
                var shape=sparkles.shape;shape.enabled=false;
                var size=sparkles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
                sparkles.GetComponent<ParticleSystemRenderer>().sharedMaterial=sparkleMaterial;
            }
            for(int i=0;i<count;i++)
            {
                var p=new ParticleSystem.EmitParams{position=position+Random.insideUnitSphere*.035f,velocity=new Vector3(Random.Range(-.12f,.12f),Random.Range(.08f,.2f),Random.Range(-.12f,.12f)),startColor=new Color(.55f,1f,.85f,.7f)};
                sparkles.Emit(p,1);
            }
        }
        void OnDestroy(){if(sparkleMaterial)Destroy(sparkleMaterial);if(sparkleTexture)Destroy(sparkleTexture);}
    }
}
