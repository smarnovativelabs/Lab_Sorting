using DG.Tweening;
using UnityEngine;
namespace LabSorting.Sorting
{
    public enum ItemLocation { Loose, Hand, Rack }
    public sealed class SortItem : MonoBehaviour
    {
        public string id;
        public ItemDefinition definition;
        public ItemLocation location;
        public SortRack rack;
        public int slot=-1;
        public Collider hitbox;
        public Renderer visual;
        public Outline outline;
        public Vector3 originalScale;
        Tween motion;
        Vector3 motionDestination;Quaternion motionRotation;
        public Vector3 SavedPosition=>Moving&&location==ItemLocation.Loose?motionDestination:transform.position;
        public Quaternion SavedRotation=>Moving&&location==ItemLocation.Loose?motionRotation:transform.rotation;
        bool hovered,searched;
        public event System.Action VisualChanged;
        public event System.Action<SortItem> PersistenceChanged;
        void PoseChanged(){VisualChanged?.Invoke();PersistenceChanged?.Invoke(this);}
        bool batchHighlight;
        public bool Hovered=>hovered;
        public bool Searched=>searched;
        public void SetBatchHighlight(bool value){if(batchHighlight==value)return;batchHighlight=value;ApplyHighlight();}
        [Range(.3f,1f)] public float heldScale=.65f;
        public bool Moving => motion!=null && motion.IsActive() && motion.IsPlaying();
        public void CompleteMotion() { motion?.Complete(true); }
        void Awake() { if(originalScale==Vector3.zero) originalScale=transform.localScale; }
        public void Highlight(bool value) { if(hovered==value)return;hovered=value;ApplyHighlight();VisualChanged?.Invoke(); }
        public void SetSearchHighlight(bool value) { if(searched==value)return;searched=value;ApplyHighlight();VisualChanged?.Invoke(); }
        void ApplyHighlight() { if(outline) outline.enabled=hovered||(searched&&!batchHighlight); }
        public void Snap(Vector3 position, Quaternion rotation, Transform parent=null)
        { motion?.Kill(); transform.SetParent(parent,true); transform.SetPositionAndRotation(position,rotation); transform.localScale=originalScale*(parent?heldScale:1f); hitbox.enabled=location!=ItemLocation.Hand;PoseChanged(); }
        public void Travel(Vector3 position, Quaternion rotation, System.Action done=null, bool drop=false)
        {
            motionDestination=position;motionRotation=rotation;
            motion?.Kill(); hitbox.enabled=false; Highlight(false);
            float duration=drop ? .28f : .24f;Vector3 from=transform.position;
            var sequence=DOTween.Sequence();
            if(drop) sequence.Join(DOTween.To(()=>0f,t=>transform.position=Vector3.Lerp(from,position,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.12f),1f,duration).SetEase(Ease.InOutSine));
            else sequence.Join(transform.DOMove(position,duration).SetEase(Ease.InOutSine));
            sequence.Join(transform.DORotateQuaternion(rotation,duration).SetEase(Ease.OutSine));
            sequence.Join(transform.DOScale(originalScale,duration).SetEase(Ease.OutCubic));
            motion=sequence.OnComplete(()=>{transform.SetPositionAndRotation(position,rotation);transform.localScale=originalScale;hitbox.enabled=location!=ItemLocation.Hand;done?.Invoke();PoseChanged();});PoseChanged();
        }
        public void DockAt(Transform socket)
        {
            motion?.Kill(); hitbox.enabled=false; Highlight(false);transform.SetParent(socket,true);
            motion=DOTween.Sequence().Join(transform.DOLocalMove(Vector3.zero,.18f).SetEase(Ease.OutCubic))
                .Join(transform.DOLocalRotateQuaternion(Quaternion.identity,.18f).SetEase(Ease.OutSine))
                .Join(transform.DOScale(originalScale*heldScale,.18f).SetEase(Ease.OutCubic));
            PoseChanged();
        }
        void OnEnable(){PoseChanged();}
        void OnDisable(){PoseChanged();}
        void OnDestroy() { motion?.Kill(); }
    }
}

