using UnityEngine;
using UnityEngine.Rendering;
namespace LabSorting.Sorting
{
    // One draw call for the next legal slot; no cloned item, collider or per-item Update.
    public sealed class RackPlacementPreview : MonoBehaviour
    {
        public Material previewMaterial;
        SortingSession session;
        public bool Visible {get;private set;}
        public Pose PreviewPose {get;private set;}
        void Awake(){session=GetComponent<SortingSession>();}
        void LateUpdate()
        {
            Visible=false;if(!session||!previewMaterial||session.player.blocked)return;
            var item=session.Active;var rack=session.TargetRack;
            if(!item||!rack||item.Moving)return;
            int slot=rack.NextSlot(item.definition);if(slot<0||!item.visual)return;
            var filter=item.visual.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)return;
            PreviewPose=rack.SlotPoseFor(item.definition,slot);Visible=true;
            var relative=item.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            var matrix=Matrix4x4.TRS(PreviewPose.position,PreviewPose.rotation,item.originalScale)*relative;
            for(int i=0;i<filter.sharedMesh.subMeshCount;i++)Graphics.DrawMesh(filter.sharedMesh,matrix,previewMaterial,gameObject.layer,null,i,null,ShadowCastingMode.Off,false);
        }
        void OnDisable(){Visible=false;}
    }
}
