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
        Tween placementShine;
        float shineAmount;
        public bool IsShining => shineAmount > 0;
        public void ShineAtPlacement()
        {
            placementShine?.Kill();
            shineAmount=.01f;ApplyHighlight();VisualChanged?.Invoke();
            placementShine=DOTween.To(()=>0f,t=>{shineAmount=Mathf.Sin(t*Mathf.PI);ApplyHighlight();},1f,.3f)
                .SetEase(Ease.Linear).OnComplete(()=>{shineAmount=0;ApplyHighlight();VisualChanged?.Invoke();});
        }
        void ClearShine(){placementShine?.Kill();shineAmount=0;ApplyHighlight();}
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
        bool hovered,searched,heldPresentation;
        public void SetHeldPresentation(bool value){if(heldPresentation==value)return;heldPresentation=value;ApplyHighlight();}
        public event System.Action VisualChanged;
        public event System.Action<SortItem> PersistenceChanged;
        void PoseChanged(){VisualChanged?.Invoke();PersistenceChanged?.Invoke(this);}
        public void MarkPoseDirty(){PersistenceChanged?.Invoke(this);}
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
        public void FinishPhysicsLanding(Vector3 position,Quaternion rotation,System.Action done){DropLandingPool.Unreserve(this);physicsLanding=false;transform.SetPositionAndRotation(position,rotation);transform.localScale=originalScale;hitbox.enabled=location!=ItemLocation.Hand;done?.Invoke();PoseChanged();}
        Vector3 releaseScale;
        Quaternion releaseRotation;
        float releaseTime;
        // The full-size collision proxy stays authoritative; only its visible presentation blends out of the hand.
        public void ApplyDropPose(Vector3 center,Vector3 offset,Quaternion rotation,bool flight)
        {
            float t=flight?Mathf.Clamp01((Time.time-releaseTime)/.18f):1;
            float blend=t*t*(3-2*t);
            transform.localScale=Vector3.Lerp(releaseScale,originalScale,blend);
            var shown=Quaternion.Slerp(releaseRotation,rotation,blend);
            var ratio=new Vector3(originalScale.x!=0?transform.localScale.x/originalScale.x:1,originalScale.y!=0?transform.localScale.y/originalScale.y:1,originalScale.z!=0?transform.localScale.z/originalScale.z:1);
            transform.SetPositionAndRotation(center-shown*Vector3.Scale(offset,ratio),shown);
        }
        public void ThrowWithPhysics(Vector3 destination,System.Action done)
        {
            selectionPresentation?.Kill();
            ClearShine();releaseScale=transform.localScale;releaseRotation=transform.rotation;releaseTime=Time.time;
            ImpactFeedbackPlayed=false;
            motion?.Kill();motionDestination=destination;motionRotation=Quaternion.identity;hitbox.enabled=false;Highlight(false);
            transform.rotation=Quaternion.identity;transform.localScale=originalScale;physicsLanding=true;
            DropLandingPool.Reserve(this,destination);
            if(!DropLandingPool.TryFlight(this,destination,done)){physicsLanding=false;transform.localScale=releaseScale;transform.rotation=releaseRotation;Travel(destination,Quaternion.identity,done,true);}
            PoseChanged();
        }
        void Awake() { if(originalScale==Vector3.zero) originalScale=transform.localScale; if(visual){float radius=visual.bounds.extents.magnitude+Vector3.Distance(visual.bounds.center,transform.position);carryLimit=Mathf.Min(1,.16f/Mathf.Max(.001f,radius));} DisableShadows(); ApplyItemLayer(); }
        public void ApplyItemLayer()
        {
            int layer=LayerMask.NameToLayer("SortingItem");if(layer<0)return;
            foreach(var child in GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;
        }
        public void DisableShadows(){foreach(var renderer in GetComponentsInChildren<Renderer>(true)){renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}}
        public static readonly Color HoverColor=new Color(1f,.18f,.77f);
        public static readonly Color HoverLabelColor=new Color(1f,.5f,.88f);
        public void Highlight(bool value) { if(hovered==value)return;hovered=value;ApplyHighlight();VisualChanged?.Invoke(); }
        public void SetSearchHighlight(bool value) { if(searched==value)return;searched=value;ApplyHighlight();VisualChanged?.Invoke(); }
        void ApplyHighlight()
        {
            if(!outline)return;
            // Magenta rim identifies the single aim target (the pale cyan floor hid a cyan rim); gold belongs to paid search results.
            outline.OutlineColor=heldPresentation?new Color(.45f,1f,.85f):hovered?HoverColor:searched?new Color(1f,.74f,.12f):new Color(.55f,1f,.85f);
            outline.OutlineWidth=heldPresentation?2.5f:(hovered||searched)?5f:shineAmount*2.5f;
            outline.enabled=heldPresentation||hovered||searched||IsShining;
        }
        public void Snap(Vector3 position, Quaternion rotation, Transform parent=null)
        { ClearShine();selectionPresentation?.Kill();DropLandingPool.Stop(this,false);physicsLanding=false;motion?.Kill(); transform.SetParent(parent,true); transform.SetPositionAndRotation(position,rotation); transform.localScale=originalScale*(parent?CarryScale:1f); hitbox.enabled=location!=ItemLocation.Hand;PoseChanged(); }
        public void PlaceOnRack(Pose pose,Transform socket,float delay,System.Action arrived)
        {
            ClearShine();selectionPresentation?.Kill();motion?.Kill();Highlight(false);hitbox.enabled=false;
            motionDestination=pose.position;motionRotation=pose.rotation;
            Vector3 from=transform.position,scale=transform.localScale;Quaternion rotation=transform.rotation;
            Vector3 localFrom=socket?socket.InverseTransformPoint(from):from;
            Quaternion localRotation=socket?Quaternion.Inverse(socket.rotation)*rotation:rotation;
            if(delay>0)transform.localScale=Vector3.zero;
            var sequence=DOTween.Sequence().AppendInterval(delay).AppendCallback(()=>
            {
                if(delay>0&&socket){from=socket.TransformPoint(localFrom);rotation=socket.rotation*localRotation;}
                transform.SetPositionAndRotation(from,rotation);transform.localScale=scale;
            });
            sequence.Append(DOTween.To(()=>0f,t=>
            {
                float u=t*t*(3-2*t);
                transform.position=Vector3.Lerp(from,pose.position,u)+Vector3.up*(4*.18f*t*(1-t));
                transform.rotation=Quaternion.Slerp(rotation,pose.rotation,u)*Quaternion.Euler(new Vector3(6,25,-8)*Mathf.Sin(t*Mathf.PI));
                // No oversize pulse: dense real rack slots only have the measured model footprint.
                transform.localScale=Vector3.Lerp(scale,originalScale,u);
            },1f,.38f).SetEase(Ease.Linear));
            motion=sequence.OnComplete(()=>{transform.SetPositionAndRotation(pose.position,pose.rotation);transform.localScale=originalScale;hitbox.enabled=location!=ItemLocation.Hand;arrived?.Invoke();PoseChanged();});
            PoseChanged();
        }
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
            ClearShine();
            selectionPresentation?.Kill();
            motion?.Kill(); hitbox.enabled=false; Highlight(false);transform.SetParent(socket,true);
            if(!magnet)
            {
                Vector3 from=transform.position,scale=transform.localScale;Quaternion rotation=transform.rotation;
                motion=DOTween.Sequence().AppendInterval(delay).Append(DOTween.To(()=>0f,t=>
                {
                    float u=Mathf.Clamp01((t-.2f)/.8f),smooth=u*u*(3-2*u);
                    var raised=from+Vector3.up*.12f;var middle=(raised+socket.position)*.5f+Vector3.up*.16f;
                    transform.position=t<.2f?Vector3.Lerp(from,raised,1-Mathf.Pow(1-t/.2f,2)):
                        (1-smooth)*(1-smooth)*raised+2*(1-smooth)*smooth*middle+smooth*smooth*socket.position;
                    transform.rotation=Quaternion.Slerp(rotation,socket.rotation,smooth)*Quaternion.Euler(new Vector3(8,35,-12)*Mathf.Sin(t*Mathf.PI));
                    transform.localScale=Vector3.Lerp(scale,originalScale*CarryScale,smooth)*(1+.045f*Mathf.Sin(t*Mathf.PI));
                },1f,.3f).SetEase(Ease.Linear)).OnComplete(()=>{transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;transform.localScale=originalScale*CarryScale;arrived?.Invoke();PoseChanged();});
                PoseChanged();return;
            }
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
        void OnDestroy() { DropLandingPool.Stop(this,false);motion?.Kill();selectionPresentation?.Kill();placementShine?.Kill(); }
    }
}

