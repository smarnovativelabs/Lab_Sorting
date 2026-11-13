using UnityEngine;
namespace LabSorting.Player
{
    // Presentation only: inventory, item colliders and saved world poses remain unchanged.
    [DefaultExecutionOrder(200)]
    public sealed class CarryPresentation : MonoBehaviour
    {
        public Camera view;
        Vector3 rest;
        int mask;
        public float ClearanceScale {get;private set;}=1;
        void Awake(){rest=transform.localPosition;mask=~((1<<2)|(1<<LayerMask.NameToLayer("SortingItem")));}
        void LateUpdate()
        {
            if(!view)return;
            Vector3 desired=transform.parent.TransformPoint(rest),origin=view.transform.position;
            var delta=desired-origin;float distance=delta.magnitude;
            bool blocked=Physics.SphereCast(origin,.20f,delta.normalized,out var hit,distance,mask,QueryTriggerInteraction.Ignore);
            float restDepth=Vector3.Dot(delta,view.transform.forward);
            // Every carried model fits the .18m envelope. Keep its rear edge ahead of
            // the near plane rather than enforcing a fixed minimum distance from walls.
            float minimum=Mathf.Clamp01((view.nearClipPlane+.015f)/Mathf.Max(.001f,restDepth-.18f));
            float target=blocked?Mathf.Clamp((hit.distance-.025f)/Mathf.Max(.001f,distance),minimum,1):1;
            // Smooth a camera-relative distance, never a world position left behind by a turn.
            // Size and camera depth change together, preserving the projected size near walls.
            ClearanceScale=target<ClearanceScale?target:Mathf.Lerp(ClearanceScale,target,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            transform.position=origin+delta*ClearanceScale;
            foreach(Transform child in transform)
            {
                if(!child.gameObject.activeSelf)continue;
                var item=child.GetComponent<LabSorting.Sorting.SortItem>();if(!item||item.location!=LabSorting.Sorting.ItemLocation.Hand)continue;
                if((!item.Moving&&!item.Presenting)||ClearanceScale<1)child.localScale=item.originalScale*item.CarryScale*ClearanceScale;
            }
        }
        public Vector3 MagnetEntry(float delay)
        {
            var delta=transform.TransformDirection(new Vector3((delay*17%1-.5f)*.65f,.2f,.7f));
            return ConstrainFlight(transform.position+delta);
        }
        public Vector3 ConstrainFlight(Vector3 position)
        {
            var delta=position-transform.position;
            float distance=delta.magnitude;
            if(distance<.001f)return transform.position;
            if(Physics.SphereCast(transform.position,.19f*ClearanceScale,delta.normalized,out var hit,distance,mask,QueryTriggerInteraction.Ignore))distance=Mathf.Max(0,hit.distance-.03f);
            return transform.position+delta.normalized*distance;
        }
    }
}
