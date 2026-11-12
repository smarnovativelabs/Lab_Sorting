using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.UI
{
    public static class SearchDirection
    {
        public static string Floor(float itemBase,float playerFeet)=>itemBase-playerFeet>1.8f?"UPPER FLOOR":itemBase-playerFeet< -1.8f?"LOWER FLOOR":"SAME FLOOR";
        public static string Bearing(Vector3 forward,Vector3 delta)
        {
            forward.y=0;delta.y=0;if(delta.sqrMagnitude<.04f)return "HERE";
            float angle=Vector3.SignedAngle(forward,delta,Vector3.up);
            return Mathf.Abs(angle)>135?"TURN AROUND":angle< -18?"< LEFT":angle>18?"RIGHT >":"AHEAD";
        }
        public static string Describe(SortingSession session,SortItem item)
        {
            var delta=item.transform.position-session.player.transform.position;
            float bottom=item.hitbox?item.hitbox.bounds.min.y:item.transform.position.y;
            return Bearing(session.player.view.transform.forward,delta)+"  |  "+Floor(bottom,session.player.transform.position.y)+"  |  "+delta.magnitude.ToString("0.0")+"m";
        }
    }
}
