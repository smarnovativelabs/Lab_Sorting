using UnityEngine;
namespace LabSorting.UI
{
    public sealed class ControlSizes:MonoBehaviour
    {
        public RectTransform joystick;
        public RectTransform[] actions;
        Vector3 joystickBase;Vector2 joystickPosition;
        Vector3[] actionBase;
        bool captured;
        public float JoystickScale {get;private set;}=1;
        public float ActionScale {get;private set;}=1;
        public static float Clamp(float value)=>float.IsNaN(value)||float.IsInfinity(value)?1:Mathf.Round(Mathf.Clamp(value,.85f,1.15f)*20)/20;
        void Capture(){if(captured)return;captured=true;joystickBase=joystick.localScale;joystickPosition=joystick.anchoredPosition;actionBase=new Vector3[actions.Length];for(int i=0;i<actions.Length;i++)actionBase[i]=actions[i].localScale;}
        public void Apply(float movement,float action)
        {
            Capture();JoystickScale=Clamp(movement);ActionScale=Clamp(action);
            joystick.localScale=joystickBase*JoystickScale;
            // Keep the original left/bottom clearance when the joystick grows.
            joystick.anchoredPosition=joystickPosition+Vector2.Scale(joystick.rect.size,new Vector2(joystickBase.x,joystickBase.y))*(JoystickScale-1)*.5f;
            for(int i=0;i<actions.Length;i++){var size=actionBase[i]*ActionScale;var motion=actions[i].GetComponent<UIButtonMotion>();if(motion)motion.SetRestScale(size);else actions[i].localScale=size;}
        }
    }
}
