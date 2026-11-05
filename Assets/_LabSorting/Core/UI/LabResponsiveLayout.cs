using UnityEngine;
namespace LabSorting.UI
{
    [RequireComponent(typeof(UnityEngine.UI.CanvasScaler))]
    public sealed class LabResponsiveLayout:MonoBehaviour
    {
        public static float FitScale(Rect safe)=>Mathf.Max(.01f,Mathf.Min(safe.width/1280f,safe.height/720f));
        UnityEngine.UI.CanvasScaler scaler;Rect previous;Vector2 previousScreen;
        void Awake(){scaler=GetComponent<UnityEngine.UI.CanvasScaler>();}
        void Update()
        {
            var safe=Screen.safeArea;var size=new Vector2(Screen.width,Screen.height);if(safe==previous&&size==previousScreen)return;
            previous=safe;previousScreen=size;if(safe.width<=0||safe.height<=0)return;
            scaler.matchWidthOrHeight=0;scaler.referenceResolution=new Vector2(Screen.width/FitScale(safe),720);
        }
    }
}
