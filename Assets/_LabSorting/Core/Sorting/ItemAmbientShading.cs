using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace LabSorting.Sorting
{
    // Loose items get their light only from the ambient probe, and the baked LightingData stores a flat white one,
    // so every face is equally bright and items read like flat stickers. After the gameplay scene loads this swaps in
    // a top-lit gradient (same result as a Trilight sky 1.06 / equator .93 / ground .54). Items already sample this
    // probe, so it adds no light, shadow, pass or draw call. Lightmapped environment surfaces ignore it.
    static class ItemAmbientShading
    {
        const string GameplayScene="Lab_Sorting";
        const float Top=1f,Side=.81f,Bottom=.53f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}

        // Unity drops an assigned probe while the mode is Flat/Trilight/Skybox (it re-derives it), so switch to Custom first.
        static void Loaded(Scene scene,LoadSceneMode mode){if(scene.name!=GameplayScene)return;RenderSettings.ambientMode=AmbientMode.Custom;RenderSettings.ambientProbe=Gradient(Top,Side,Bottom);}

        // Solves the constant, vertical and two quadratic SH terms so the probe reads exactly top/side/bottom
        // for normals facing up, sideways and down (a gradient that is round about the vertical axis).
        internal static SphericalHarmonicsL2 Gradient(float top,float side,float bottom)
        {
            int[] terms={0,1,6,8};
            Vector3[] dirs={Vector3.up,Vector3.down,Vector3.forward,Vector3.right};
            float[] want={top,bottom,side,side};
            var m=new float[4,5];var lit=new Color[4];
            for(int j=0;j<4;j++){var basis=new SphericalHarmonicsL2();basis[0,terms[j]]=1f;basis.Evaluate(dirs,lit);for(int i=0;i<4;i++)m[i,j]=lit[i].r;m[j,4]=want[j];}
            for(int c=0;c<4;c++)
            {
                int p=c;for(int r=c+1;r<4;r++)if(Mathf.Abs(m[r,c])>Mathf.Abs(m[p,c]))p=r;
                for(int k=0;k<5;k++){var t=m[c,k];m[c,k]=m[p,k];m[p,k]=t;}
                for(int r=0;r<4;r++){if(r==c)continue;float f=m[r,c]/m[c,c];for(int k=c;k<5;k++)m[r,k]-=f*m[c,k];}
            }
            var sh=new SphericalHarmonicsL2();
            for(int j=0;j<4;j++)for(int ch=0;ch<3;ch++)sh[ch,terms[j]]=m[j,4]/m[j,j];
            return sh;
        }
    }
}
