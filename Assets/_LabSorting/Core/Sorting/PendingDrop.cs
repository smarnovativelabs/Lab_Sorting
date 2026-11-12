using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        SortItem pendingDrop;
        Vector3 dropProbePosition,dropProbeForward;
        float dropProbeAt;
        int dropProbeRevision;
        public bool DropPending=>pendingDrop&&pendingDrop==Active;
        void CancelPendingDrop(){pendingDrop=null;}
        void QueueDrop(SortItem item)
        {
            pendingDrop=item;dropProbePosition=player.view.transform.position;dropProbeForward=player.view.transform.forward;
            dropProbeRevision=Revision;dropProbeAt=Time.unscaledTime+.2f;
        }
        void TickPendingDrop()
        {
            if(!pendingDrop)return;
            if(pendingDrop!=Active||player.blocked||pendingDrop.location!=ItemLocation.Hand){CancelPendingDrop();return;}
            if(Time.unscaledTime<dropProbeAt||!Ready()||Active.Moving)return;
            if(Revision==dropProbeRevision&&(player.view.transform.position-dropProbePosition).sqrMagnitude<.0025f&&Vector3.Angle(player.view.transform.forward,dropProbeForward)<2)return;
            ThrowActive();
        }
    }
}
