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
        void Capture()
        {
            if(captured)return;captured=true;joystickBase=joystick.localScale;joystickPosition=joystick.anchoredPosition;
            actionBase=new Vector3[actions.Length];var economy=GetComponent<EconomyHUD>();var context=GetComponent<ContextActionHUD>();
            for(int i=0;i<actions.Length;i++)
            {
                bool tool=economy&&(actions[i]==economy.searchButton.transform||actions[i]==economy.magnetButton.transform);
                actionBase[i]=actions[i].localScale*(tool?1.25f:1f);
                // Keep enlarged icons, captions and badges inside the safe area at maximum control size.
                if(tool)actions[i].anchoredPosition+=new Vector2(-12,actions[i]==economy.searchButton.transform?32:0);
                // Make room for the larger magnet's touch area without shrinking DROP.
                if(context&&actions[i]==context.drop.transform)actions[i].anchoredPosition+=new Vector2(-24,0);
            }
        }
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
