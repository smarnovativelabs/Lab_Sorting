using DG.Tweening;
using UnityEngine;
namespace LabSorting.Sorting
{
    public sealed partial class SortItem
    {
        Tween completionFloat;
        GameObject completionVisual;
        bool completionVisualEnabled;
        public bool CompletionFloating => completionFloat!=null && completionFloat.IsActive();

        // A render-only copy leaves the authoritative mesh transform, collider and save pose untouched.
        public void PlayCompletionFloat(float delay,float height)
        {
            StopCompletionFloat();
            var mesh=visual?visual.GetComponent<MeshFilter>():null;
            if(!mesh||!mesh.sharedMesh||!visual.enabled)return;
            ClearShine();Highlight(false);
            completionVisual=new GameObject("Rack completion visual",typeof(MeshFilter),typeof(MeshRenderer));
            completionVisual.layer=visual.gameObject.layer;
            var t=completionVisual.transform;t.SetParent(visual.transform,false);
            completionVisual.GetComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
            var renderer=completionVisual.GetComponent<MeshRenderer>();renderer.sharedMaterials=visual.sharedMaterials;
            renderer.shadowCastingMode=visual.shadowCastingMode;renderer.receiveShadows=visual.receiveShadows;
            renderer.lightProbeUsage=visual.lightProbeUsage;renderer.reflectionProbeUsage=visual.reflectionProbeUsage;
            renderer.renderingLayerMask=visual.renderingLayerMask;
            completionVisualEnabled=visual.enabled;visual.enabled=false;
            var offset=t.parent.InverseTransformVector(Vector3.up*height);
            completionFloat=DOTween.Sequence().SetUpdate(true).AppendInterval(delay)
                .Append(t.DOLocalMove(offset,.32f).SetEase(Ease.InOutSine))
                .Append(t.DOLocalMove(Vector3.zero,.32f).SetEase(Ease.InOutSine))
                .OnComplete(StopCompletionFloat);
            VisualChanged?.Invoke();
        }
        public void StopCompletionFloat()
        {
            var tween=completionFloat;completionFloat=null;tween?.Kill();
            if(completionVisual){completionVisual.SetActive(false);Destroy(completionVisual);completionVisual=null;if(visual)visual.enabled=completionVisualEnabled;VisualChanged?.Invoke();}
        }
    }
}
