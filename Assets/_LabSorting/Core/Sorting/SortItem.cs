using DG.Tweening;
using UnityEngine;
namespace LabSorting.Sorting
{
    public enum ItemLocation { Loose, Hand, Rack }
    public sealed partial class SortItem : MonoBehaviour
    {
        public string id;
        public ItemDefinition definition;
        public ItemLocation location;
        public SortRack rack;
        public int slot=-1;
        public bool PickLocked => location==ItemLocation.Rack && rack && rack.IsFull;
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
            selectionProgress=0;
            var presentation=transform.parent?transform.parent.GetComponent<LabSorting.Player.CarryPresentation>():null;
            if(!presentation)
            {
                var scale=originalScale*CarryScale;
                transform.localScale=scale*.88f;transform.localRotation=Quaternion.Euler(0,6,-4);
                selectionPresentation=DOTween.Sequence().Join(transform.DOScale(scale,.13f).SetEase(Ease.OutCubic)).Join(transform.DOLocalRotateQuaternion(Quaternion.identity,.13f).SetEase(Ease.OutSine));
                return;
            }
            // Tween only progress; CarryPresentation evaluates geometry after camera
            // and wall updates, once per rendered frame rather than twice.
            selectionPresentation=DOTween.To(()=>selectionProgress,t=>selectionProgress=t,1f,.16f).SetEase(Ease.OutCubic)
                .OnComplete(()=>{if(presentation)presentation.ResolveHeldPose(this).Apply(transform);});
            if(presentation)RefreshHeldPresentation(presentation);
        }
        float selectionProgress=1,pickupProgress;
        bool pickupFlight,pickupMagnet;
        public bool MagnetFlying=>pickupFlight&&pickupMagnet;
        public const float MagnetFlightDuration=.7f;
        Vector3 pickupFrom,pickupScale;
        Quaternion pickupRotation;
        public void RefreshHeldPresentation(LabSorting.Player.CarryPresentation presentation)
        {
            if(location!=ItemLocation.Hand||(!pickupFlight&&Moving))return;
            var pose=presentation.ResolveHeldPose(this);
            if(pickupFlight)
            {
                float t=pickupProgress,u=pickupMagnet?t:Mathf.Clamp01((t-.22f)/.78f),smooth=u*u*(3-2*u);
                var destination=transform.parent.TransformPoint(pose.position);
                var raised=pickupFrom+Vector3.up*.14f;
                var middle=(raised+destination)*.5f+Vector3.up*.14f;
                var position=pickupMagnet?MagnetPosition(destination,t):
                    t<.22f?Vector3.Lerp(pickupFrom,raised,1-Mathf.Pow(1-t/.22f,2)):
                    (1-smooth)*(1-smooth)*raised+2*(1-smooth)*smooth*middle+smooth*smooth*destination;
                transform.position=position;
                transform.rotation=Quaternion.Slerp(pickupRotation,transform.parent.rotation*pose.rotation,smooth)*Quaternion.Euler(new Vector3(10,48,-14)*Mathf.Sin(t*Mathf.PI));
                transform.localScale=Vector3.Lerp(pickupScale,pose.scale,smooth)*(1+.06f*Mathf.Sin(t*Mathf.PI));
            }
            else
            {
                pose.Apply(transform);
                if(Presenting)
                {
                    transform.localScale*=Mathf.Lerp(.9f,1,selectionProgress);
                    transform.localRotation*=Quaternion.Euler(new Vector3(0,6,-4)*(1-selectionProgress));
                }
            }
        }
        Vector3 MagnetPosition(Vector3 destination,float progress)
        {
            // Keep the source in world space, including while staggered and while
            // the player turns. Only the destination follows the moving hand.
            float t=progress*progress*(3-2*progress);
            return Vector3.Lerp(pickupFrom,destination,t)+Vector3.up*(.3f*4*t*(1-t));
        }
        Vector3 motionDestination;Quaternion motionRotation;
        internal NaturalItemSettling naturalSettling;
        public Vector3 SavedPosition=>naturalSettling&&naturalSettling.Running?transform.position:Moving&&location==ItemLocation.Loose?motionDestination:transform.position-Vector3.up*searchLift;
        public Quaternion SavedRotation=>naturalSettling&&naturalSettling.Running?transform.rotation:Moving&&location==ItemLocation.Loose?motionRotation:transform.rotation;
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
        public bool Moving => (naturalSettling&&naturalSettling.Running)||physicsLanding||(motion!=null && motion.IsActive() && motion.IsPlaying());
        public void CompleteMotion() { selectionPresentation?.Complete(true);motion?.Complete(true);DropLandingPool.Stop(this,true);GetComponent<NaturalItemSettling>()?.FinishNow(); }
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
        Tween searchPresentation;
        float searchLift;
        public void SetSearchHighlight(bool value)
        {
            value=value&&location==ItemLocation.Loose&&!Moving;
            if(searched==value)return;
            searchPresentation?.Kill();searched=value;
            if(value)
            {
                searchPresentation=DOTween.To(()=>searchLift,lift=>
                {
                    transform.position+=Vector3.up*(lift-searchLift);searchLift=lift;
                    transform.localScale=originalScale*Mathf.Lerp(1,1.2f,lift/.125f);
                    VisualChanged?.Invoke();
                },.125f,.2f).SetEase(Ease.OutCubic);
            }
            else
            {
                transform.position-=Vector3.up*searchLift;searchLift=0;
                transform.localScale=originalScale;
            }
            ApplyHighlight();VisualChanged?.Invoke();
        }
        void ApplyHighlight()
        {
            if(!outline)return;
            outline.SetReadableFeedback(!heldPresentation&&(hovered||searched),searched&&!hovered&&!heldPresentation);
            // Magenta rim identifies the single aim target (the pale cyan floor hid a cyan rim); gold belongs to paid search results.
            outline.OutlineColor=heldPresentation?new Color(.45f,1f,.85f):hovered?HoverColor:searched?new Color(1f,.74f,.12f):new Color(.55f,1f,.85f);
            outline.OutlineWidth=heldPresentation?2.5f:hovered?4.5f:searched?6f:shineAmount*2.5f;
            outline.enabled=heldPresentation||hovered||searched||IsShining;
        }
        public void Snap(Vector3 position, Quaternion rotation, Transform parent=null)
        { NaturalItemSettling.Suspend(this);SetSearchHighlight(false);StopCompletionFloat();ClearShine();selectionPresentation?.Kill();DropLandingPool.Stop(this,false);physicsLanding=false;motion?.Kill(); transform.SetParent(parent,true); transform.SetPositionAndRotation(position,rotation); transform.localScale=originalScale*(parent?CarryScale:1f); hitbox.enabled=location!=ItemLocation.Hand;PoseChanged(); }
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
            NaturalItemSettling.Suspend(this);
            ClearShine();
            selectionPresentation?.Kill();
            motion?.Kill(); hitbox.enabled=false; Highlight(false);transform.SetParent(socket,true);
            var presentation=socket.GetComponent<LabSorting.Player.CarryPresentation>();
            pickupFlight=true;pickupMagnet=magnet;pickupProgress=0;selectionProgress=1;
            pickupFrom=transform.position;pickupScale=transform.localScale;pickupRotation=transform.rotation;
            motion=DOTween.Sequence().AppendInterval(delay).Append(DOTween.To(()=>0f,t=>
            {
                pickupProgress=t;
                if(!presentation){transform.position=magnet?MagnetPosition(socket.position,t):Vector3.Lerp(pickupFrom,socket.position,t);transform.rotation=Quaternion.Slerp(pickupRotation,socket.rotation,t);transform.localScale=Vector3.Lerp(pickupScale,originalScale*CarryScale,t);}
            },1f,magnet?MagnetFlightDuration:.34f).SetEase(Ease.Linear)).OnUpdate(()=>
            {
                if(magnet&&!presentation&&pickupProgress==0)transform.SetPositionAndRotation(pickupFrom,pickupRotation);
            }).OnComplete(()=>
            {
                if(presentation)presentation.ResolveHeldPose(this).Apply(transform);
                pickupFlight=false;arrived?.Invoke();PoseChanged();
            }).OnKill(()=>pickupFlight=false);
            PoseChanged();
        }
        void OnEnable(){PoseChanged();}
        void OnDisable(){StopCompletionFloat();PoseChanged();}
        void OnDestroy() { searchPresentation?.Kill();DropLandingPool.Stop(this,false);motion?.Kill();selectionPresentation?.Kill();placementShine?.Kill(); }
    }
}
