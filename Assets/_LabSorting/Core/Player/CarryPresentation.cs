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
            ClearanceScale=blocked&&hit.distance<.22f?.5f:1;
            if(ClearanceScale<1)blocked=Physics.SphereCast(origin,.1f,delta.normalized,out hit,distance,mask,QueryTriggerInteraction.Ignore);
            if(blocked)desired=origin+delta.normalized*Mathf.Max(ClearanceScale<1?.18f:.27f,hit.distance-.025f);
            // Retract immediately before rendering; ease back only when clearance increases.
            transform.position=(desired-origin).sqrMagnitude<(transform.position-origin).sqrMagnitude?desired:Vector3.Lerp(transform.position,desired,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
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
