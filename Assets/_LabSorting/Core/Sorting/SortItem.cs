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
        Tween selectionPresentation;
        public bool Presenting=>selectionPresentation!=null&&selectionPresentation.IsActive()&&selectionPresentation.IsPlaying();
        public void PresentHeld()
        {
            selectionPresentation?.Kill();if(location!=ItemLocation.Hand)return;
            var scale=originalScale*CarryScale;
            transform.localScale=scale*.88f;transform.localRotation=Quaternion.Euler(0,6,-4);
            selectionPresentation=DOTween.Sequence().Join(transform.DOScale(scale,.13f).SetEase(Ease.OutCubic)).Join(transform.DOLocalRotateQuaternion(Quaternion.identity,.13f).SetEase(Ease.OutSine));
        }
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
        float carryLimit=1;
        public float CarryScale=>Mathf.Min(heldScale,carryLimit)*1.12f;
        bool physicsLanding;
        public bool ImpactFeedbackPlayed {get;set;}
        public bool Moving => physicsLanding||(motion!=null && motion.IsActive() && motion.IsPlaying());
        public void CompleteMotion() { selectionPresentation?.Complete(true);motion?.Complete(true);DropLandingPool.Stop(this,true); }
        public void FinishPhysicsLanding(Vector3 position,Quaternion rotation,System.Action done){DropLandingPool.Unreserve(this);physicsLanding=false;transform.SetPositionAndRotation(position,rotation);hitbox.enabled=location!=ItemLocation.Hand;done?.Invoke();PoseChanged();}
        public void ThrowWithPhysics(Vector3 destination,System.Action done)
        {
            selectionPresentation?.Kill();
            ImpactFeedbackPlayed=false;
            motion?.Kill();motionDestination=destination;motionRotation=Quaternion.identity;hitbox.enabled=false;Highlight(false);
            transform.rotation=Quaternion.identity;transform.localScale=originalScale;physicsLanding=true;
            DropLandingPool.Reserve(this,destination);
            if(!DropLandingPool.TryFlight(this,destination,done)){physicsLanding=false;Travel(destination,Quaternion.identity,done,true);}
            PoseChanged();
        }
        void Awake() { if(originalScale==Vector3.zero) originalScale=transform.localScale; if(visual){float radius=visual.bounds.extents.magnitude+Vector3.Distance(visual.bounds.center,transform.position);carryLimit=Mathf.Min(1,.16f/Mathf.Max(.001f,radius));} DisableShadows(); ApplyItemLayer(); }
        public void ApplyItemLayer()
        {
            int layer=LayerMask.NameToLayer("SortingItem");if(layer<0)return;
            foreach(var child in GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;
        }
        public void DisableShadows(){foreach(var renderer in GetComponentsInChildren<Renderer>(true)){renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}}
        public void Highlight(bool value) { if(hovered==value)return;hovered=value;ApplyHighlight();VisualChanged?.Invoke(); }
        public void SetSearchHighlight(bool value) { if(searched==value)return;searched=value;ApplyHighlight();VisualChanged?.Invoke(); }
        void ApplyHighlight()
        {
            if(!outline)return;
            // Cyan rim identifies the single aim target; gold belongs to paid search results.
            outline.OutlineColor=hovered?new Color(.2f,1f,1f):new Color(1f,.74f,.12f);
            outline.OutlineWidth=hovered?4f:2f;
            outline.enabled=hovered||(searched&&!batchHighlight);
        }
        public void Snap(Vector3 position, Quaternion rotation, Transform parent=null)
        { selectionPresentation?.Kill();DropLandingPool.Stop(this,false);physicsLanding=false;motion?.Kill(); transform.SetParent(parent,true); transform.SetPositionAndRotation(position,rotation); transform.localScale=originalScale*(parent?CarryScale:1f); hitbox.enabled=location!=ItemLocation.Hand;PoseChanged(); }
        // Placement glide or a calm floor drop without tumbling or bounce.
        // For a drop, done is called once at touchdown with the collider restored.
        public void Travel(Vector3 position, Quaternion rotation, System.Action done=null, bool drop=false)
        {
            selectionPresentation?.Kill();
            if(drop)ImpactFeedbackPlayed=false;
            motionDestination=position;motionRotation=rotation;
            motion?.Kill(); hitbox.enabled=false; Highlight(false);
            Vector3 from=transform.position;var fromRotation=transform.rotation;
            var sequence=DOTween.Sequence();
            if(drop)
            {
                float flight=Mathf.Clamp(.34f+Vector3.Distance(from,position)*.055f,.4f,.65f);
                sequence.Join(DOTween.To(()=>0f,t=>{
                    transform.position=DropPoint(from,position,t);
                    // Orient while still hand-sized, then expand into the validated landing box.
                    transform.rotation=Quaternion.Slerp(fromRotation,rotation,Mathf.Clamp01(t*4));
                },1f,flight).SetEase(Ease.Linear));
                sequence.Insert(flight*.25f,transform.DOScale(originalScale,flight*.75f).SetEase(Ease.InOutSine));
            }
            else
            {
                const float duration=.24f;
                sequence.Join(transform.DOMove(position,duration).SetEase(Ease.InOutSine));
                sequence.Join(transform.DORotateQuaternion(rotation,duration).SetEase(Ease.OutSine));
                sequence.Join(transform.DOScale(originalScale,duration).SetEase(Ease.OutCubic));
            }
            motion=sequence.OnComplete(()=>{transform.SetPositionAndRotation(position,rotation);transform.localScale=originalScale;if(drop&&location==ItemLocation.Loose){physicsLanding=true;DropLandingPool.Begin(this,position,rotation,done);}else{hitbox.enabled=location!=ItemLocation.Hand;done?.Invoke();}PoseChanged();});PoseChanged();
        }
        // Trick shot into a rack: a spinning arc to a point just in front of the shelf, then a short slide onto its point.
        public void Toss(Vector3 entry, Vector3 position, Quaternion rotation, System.Action landed=null)
        {
            selectionPresentation?.Kill();
            motionDestination=position;motionRotation=rotation;
            motion?.Kill(); hitbox.enabled=false; Highlight(false);
            Vector3 from=transform.position;var fromRotation=transform.rotation;float apex=TossApex(from,entry);
            float flight=TossFlight(from,entry);
            var axis=Quaternion.Euler(0,Random.Range(0f,360f),0)*Vector3.right;float spin=Random.value<.5f?720:-720;
            var sequence=DOTween.Sequence();
            sequence.Append(DOTween.To(()=>0f,t=>{
                transform.position=ArcPoint(from,entry,apex,t);
                transform.rotation=Quaternion.AngleAxis(spin*t,axis)*Quaternion.Slerp(fromRotation,rotation,t);
            },1f,flight).SetEase(Ease.Linear));
            sequence.Join(transform.DOScale(originalScale,flight*.6f).SetEase(Ease.OutCubic));
            sequence.Append(transform.DOMove(TossSettlePoint(entry,position),.12f).SetEase(Ease.OutQuad));
            sequence.Append(transform.DOMove(position,.1f).SetEase(Ease.InOutSine));
            sequence.AppendCallback(()=>{transform.SetPositionAndRotation(position,rotation);landed?.Invoke();});
            // Dense shelf points only have the measured model footprint available; do not expand into neighbours.
            motion=sequence.OnComplete(()=>{transform.SetPositionAndRotation(position,rotation);transform.localScale=originalScale;hitbox.enabled=location!=ItemLocation.Hand;PoseChanged();});PoseChanged();
        }
        // Shared by the toss animation and the path check, so the checked arc is the flown arc.
        public static Vector3 DropPoint(Vector3 from,Vector3 to,float t)=>Vector3.Lerp(from,to,t)+Vector3.up*(4*t*(1-t)*.28f);
        public static Vector3 TossSettlePoint(Vector3 entry,Vector3 destination)=>new Vector3(destination.x,entry.y,destination.z);
        public static float TossApex(Vector3 from,Vector3 to)=>Mathf.Max(from.y,to.y)+.4f+Flat(to-from)*.06f;
        public static float TossFlight(Vector3 from,Vector3 to)=>Mathf.Clamp(.35f+Flat(to-from)*.05f,.4f,.85f);
        public static Vector3 ArcPoint(Vector3 from,Vector3 to,float apex,float t)
        {
            // Quadratic curve whose highest point is about apex.
            var control=(from+to)*.5f;control.y=2*apex-(from.y+to.y)*.5f;
            float u=1-t;return u*u*from+2*u*t*control+t*t*to;
        }
        static float Flat(Vector3 v)=>new Vector2(v.x,v.z).magnitude;
        public void DockAt(Transform socket,bool magnet=false,float delay=0,System.Action arrived=null)
        {
            selectionPresentation?.Kill();
            motion?.Kill(); hitbox.enabled=false; Highlight(false);transform.SetParent(socket,true);
            float duration=magnet?.38f:.18f;
              // Unlimited-range collection is represented by a short, clearance-checked arrival.
              // Never fly full-size models through the room's walls or the camera near plane.
              var presentation=socket.GetComponent<LabSorting.Player.CarryPresentation>();
              if(magnet){transform.position=presentation?presentation.MagnetEntry(delay):socket.TransformPoint(new Vector3((delay*17%1-.5f)*.65f,.2f,.7f));transform.localScale=originalScale*CarryScale;}
            motion=DOTween.Sequence().AppendInterval(delay).Append(transform.DOLocalMove(Vector3.zero,duration).SetEase(magnet?Ease.InOutSine:Ease.OutCubic))
                .Join(transform.DOLocalRotateQuaternion(Quaternion.identity,duration).SetEase(Ease.OutSine))
                  .Join(transform.DOScale(originalScale*CarryScale,duration).SetEase(Ease.OutCubic)).OnUpdate(()=>{if(magnet&&presentation)transform.position=presentation.ConstrainFlight(transform.position);}).OnComplete(()=>{arrived?.Invoke();PoseChanged();});
            PoseChanged();
        }
        void OnEnable(){PoseChanged();}
        void OnDisable(){PoseChanged();}
        void OnDestroy() { DropLandingPool.Stop(this,false);motion?.Kill();selectionPresentation?.Kill(); }
    }
}

