using UnityEngine;
using UnityEngine.UI;
using LabSorting.Sorting;

namespace LabSorting.UI
{
    // First-run tutorial. Each step waits for the player to really do it. An animated hand shows which on-screen
    // control to use: it drags on the joystick or look area, taps a button, or points at the aim cross in the middle.
    // The hand only ever sits on fixed screen controls, so it never slides around when the camera turns, and it gets
    // out of the way while the player is using the control it demonstrates. Never blocks input.
    public sealed partial class GuidedTutorial : MonoBehaviour
    {
        public enum Step { Move, Look, Pick, Place, Throw, Tools, Done }
        public enum HandMode { Hidden, Tap, Drag, Aim }
        public SortingSession session;
        public ContextActionHUD actions;
        [Tooltip("Space the hand and bubble live in (the HUD safe area).")] public RectTransform area;
        public CanvasGroup group;
        public RectTransform hand;
        public RectTransform bubble;
        public Text message, stepLabel;
        public Button skipButton, gotItButton;
        public RectTransform joystick, lookArea, pickButton, placeButton, throwButton, searchButton, magnetButton;
        [Min(0)] public int reward=3;
        public string preferencesPrefix="LabSorting.";
        [Min(.1f)] public float moveDistance=1.5f, lookDegrees=45;
        [Tooltip("Seconds without moving/turning before the hand shows the gesture again.")] [Min(0)] public float idleDelay=1.2f;
        public bool IsRunning {get;private set;}
        public Step Current {get;private set;}
        public HandMode Mode {get;private set;}
        public bool Completed=>PlayerPrefs.GetInt(preferencesPrefix+"GuidedDone",0)==1;
        public const int StepCount=5;
        const float TargetGrace=.3f, FadeSpeed=6;
        Vector3 lastPosition;float lastYaw,walked,looked,hideAt,lastActivity=-99,lastPickTarget=-99,lastRackTarget=-99,handAlpha;
        bool subscribed,paid,fresh,canThrow;float nextThrowCheck;string shown;Image handImage;

        void Awake(){Hide();}
        void OnEnable(){if(IsRunning)Subscribe();}
        void OnDisable(){Unsubscribe();HideRackGuide();}

        // Start from the beginning, or from the step saved when the game was closed.
        public void Begin(bool restart)
        {
            LabSorting.Services.LabTelemetry.Event(restart?"tutorial_replayed":"tutorial_started");
            int saved=restart?0:Mathf.Clamp(PlayerPrefs.GetInt(preferencesPrefix+"GuidedStep",0),0,(int)Step.Tools);
            if(saved==(int)Step.Throw)saved=(int)Step.Tools; // Older saves: dropping is now optional.
            IsRunning=true;paid=false;Subscribe();Enter((Step)saved);
        }
        public void Skip()
        {
            if(!IsRunning)return;
            PlayerPrefs.SetInt(preferencesPrefix+"GuidedDone",1);PlayerPrefs.DeleteKey(preferencesPrefix+"GuidedStep");PlayerPrefs.Save();
            Stop();
        }
        public void GotIt(){if(IsRunning&&Current==Step.Tools)Enter(Step.Done);}
        void Stop(){IsRunning=false;Unsubscribe();Hide();}
        void Enter(Step step)
        {
            Current=step;fresh=true;lastActivity=-99;nextThrowCheck=0;canThrow=false;
            if(step==Step.Done)
            {
                LabSorting.Services.LabTelemetry.Event("tutorial_completed");
                paid=session.ClaimTutorialReward(reward);hideAt=Time.unscaledTime+5;
                PlayerPrefs.SetInt(preferencesPrefix+"GuidedDone",1);PlayerPrefs.DeleteKey(preferencesPrefix+"GuidedStep");
            }
            else PlayerPrefs.SetInt(preferencesPrefix+"GuidedStep",(int)step);
            PlayerPrefs.Save();
            if(gotItButton)gotItButton.gameObject.SetActive(step==Step.Tools);
            if(skipButton)skipButton.gameObject.SetActive(step!=Step.Done);
            if(stepLabel)stepLabel.text=step==Step.Done?"":"STEP "+Mathf.Min((int)step+1,StepCount)+" / "+StepCount;
            shown=null;
        }

        void Subscribe(){if(subscribed||!session)return;session.Feedback+=OnFeedback;session.SearchStarted+=OnSearch;session.MagnetCollected+=OnMagnet;subscribed=true;}
        void Unsubscribe(){if(!subscribed||!session)return;session.Feedback-=OnFeedback;session.SearchStarted-=OnSearch;session.MagnetCollected-=OnMagnet;subscribed=false;}
        // Doing a later action early skips ahead (someone who already picks things up knows how to walk).
        void OnFeedback(SortingCue cue)
        {
            if(!IsRunning)return;
            if(cue==SortingCue.Pick&&Current<=Step.Pick)Enter(Step.Place);
            else if(cue==SortingCue.Place&&Current<=Step.Place){Enter(Step.Tools);placedHintUntil=Time.unscaledTime+1.3f;}
            else if(cue==SortingCue.Throw&&Current==Step.Throw)Enter(Step.Tools);
            // A toss is a placement made with the THROW button: it counts for either step.
            else if(cue==SortingCue.Toss){if(Current<=Step.Place)Enter(Step.Throw);else if(Current==Step.Throw)Enter(Step.Tools);}
        }
        void OnSearch(ItemDefinition type,int count){GotIt();}
        void OnMagnet(ItemDefinition type,Vector3[] origins){GotIt();}

        void Update()
        {
            if(!IsRunning)return;
            // Walking and turning are measured frame by frame from the first frame of the step. One-frame jumps
            // (loading a save restores the player's position and view) are not counted as walking or looking.
            var position=session.player.transform.position;float yaw=session.player.look.Yaw;
            if(fresh){fresh=false;lastPosition=position;lastYaw=yaw;walked=looked=0;}
            float stepDistance=Flat(position-lastPosition),turn=Mathf.Abs(Mathf.DeltaAngle(lastYaw,yaw));lastPosition=position;lastYaw=yaw;
            if(stepDistance<1){walked+=stepDistance;if(stepDistance>.002f)lastActivity=Time.unscaledTime;}
            if(turn<90){looked+=turn;if(turn>.05f)lastActivity=Time.unscaledTime;}
            if(actions&&actions.PickVisible)lastPickTarget=Time.unscaledTime;
            if(RackReady())lastRackTarget=Time.unscaledTime;
            bool blocked=session.player.blocked;
            UpdateRackGuide(blocked);
            group.alpha=blocked?0:1;group.blocksRaycasts=!blocked;
            if(Current==Step.Done){SetHand(HandMode.Hidden);SetText();FadeHand();if(Time.unscaledTime>=hideAt)Stop();return;}
            if(blocked)return;
            bool idle=Time.unscaledTime-lastActivity>idleDelay;
            switch(Current)
            {
                case Step.Move:
                    if(walked>=moveDistance){Enter(Step.Look);return;}
                    if(idle)Drag(joystick,new Vector2(0,-10),new Vector2(0,55),55);else SetHand(HandMode.Hidden);
                    break;
                case Step.Look:
                    if(looked>=lookDegrees){Enter(Step.Pick);return;}
                    if(idle)Drag(lookArea,new Vector2(-140,-60),new Vector2(140,-60),55);else SetHand(HandMode.Hidden);
                    break;
                case Step.Pick:
                    // Hands already full (e.g. replaying the tutorial later): picking can't be practised, move on.
                    if(session.carried.Count>=session.capacity){Enter(Step.Place);return;}
                    ShowPick();break;
                case Step.Place:
                    if(!session.Active){ShowPick();break;}
                    if(Recent(lastRackTarget))Tap(placeButton);else Aim();
                    break;
                case Step.Throw:
                    if(!session.Active||session.Active.Moving){ShowPick();break;}
                    // A throw needs clear floor ahead, and the lab is full of piles: only show THROW when it will work.
                    if(Time.unscaledTime>=nextThrowCheck){nextThrowCheck=Time.unscaledTime+.25f;canThrow=session.TossRack||session.TryThrowPosition(session.Active,out _);}
                    if(canThrow)Tap(throwButton);else Aim();
                    break;
                case Step.Tools:
                    Tap(Mathf.Repeat(Time.unscaledTime,3.2f)<1.6f?searchButton:magnetButton);break;
            }
            SetText();FadeHand();
        }
        // Aiming at an item shows PICK; otherwise the hand points at the aim cross. Short gaps (moving over a pile)
        // keep the hand where it is, so it does not flicker between the two.
        void ShowPick(){if(Recent(lastPickTarget))Tap(pickButton);else Aim();}
        bool Recent(float time)=>Time.unscaledTime-time<=TargetGrace;
        bool RackReady()=>session.TargetRack&&session.Active&&!session.Active.Moving&&session.TargetRack.CanAccept(session.Active.definition);

        // ---------- Hand ----------
        void SetHand(HandMode mode){Mode=mode;}
        // Fade in/out instead of popping, so short changes do not look like flicker.
        void FadeHand()
        {
            if(!hand)return;if(!handImage)handImage=hand.GetComponent<Image>();
            handAlpha=Mathf.MoveTowards(handAlpha,Mode==HandMode.Hidden?0:1,FadeSpeed*Time.unscaledDeltaTime);
            hand.gameObject.SetActive(handAlpha>0);
            if(handImage){var c=handImage.color;c.a=handAlpha;handImage.color=c;}
        }
        Vector2 Local(RectTransform target)=>Local(target,.5f);
        // A point across the target's width (0 = left edge, 1 = right edge) in the hand's space.
        Vector2 Local(RectTransform target,float across){var r=target.rect;return area.InverseTransformPoint(target.TransformPoint(new Vector2(Mathf.Lerp(r.xMin,r.xMax,across),r.center.y)));}
        // The sprite points right with its pivot on the fingertip. Mirror it for targets on the left of the screen.
        void PutHand(Vector2 tip,float angle,bool mirror,float press)
        {
            hand.localPosition=new Vector3(tip.x,tip.y,0);
            hand.localRotation=Quaternion.Euler(0,0,angle);hand.localScale=new Vector3(mirror?-press:press,press,1);
        }
        void Tap(RectTransform target)
        {
            if(!target||!target.gameObject.activeInHierarchy){SetHand(HandMode.Hidden);return;}
            SetHand(HandMode.Tap);
            bool mirror=Local(target).x<area.rect.center.x-area.rect.width*.2f;
            // Touch the near end of the button so its label stays readable.
            var tip=Local(target,mirror?.85f:.15f);
            float angle=mirror?20:-20;var direction=Quaternion.Euler(0,0,angle)*(mirror?Vector3.left:Vector3.right);
            // Move in, press, move back out: one tap per second.
            float t=Mathf.Repeat(Time.unscaledTime,1f),away=t<.4f?Mathf.Lerp(30,0,Smooth(t/.4f)):t<.65f?0:Mathf.Lerp(0,30,Smooth((t-.65f)/.35f));
            PutHand(tip-(Vector2)direction*away,angle,mirror,t>=.4f&&t<.65f?.9f:1);
        }
        void Drag(RectTransform target,Vector2 from,Vector2 to,float angle)
        {
            if(!target||!target.gameObject.activeInHierarchy){SetHand(HandMode.Hidden);return;}
            SetHand(HandMode.Drag);
            float t=Mathf.Repeat(Time.unscaledTime,1.6f)/1.6f;float k=t<.5f?Smooth(t*2):Smooth(2-t*2);
            PutHand(Local(target)+Vector2.Lerp(from,to,k),angle,false,.92f);
        }
        // Points at the aim cross in the middle of the screen from below-left, nudging gently. Fixed on screen.
        void Aim()
        {
            SetHand(HandMode.Aim);
            var center=(Vector2)area.InverseTransformPoint(new Vector3(Screen.width*.5f,Screen.height*.5f,0));
            float angle=35;var direction=(Vector2)(Quaternion.Euler(0,0,angle)*Vector3.right);
            float away=26+Mathf.Sin(Time.unscaledTime*5)*6;
            PutHand(center-direction*away,angle,false,.9f);
        }
        static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        static float Flat(Vector3 v)=>new Vector2(v.x,v.z).magnitude;

        // ---------- Text ----------
        void SetText()
        {
            string text=Instruction();if(text==shown)return;shown=text;if(message)message.text=text;
        }
        string Instruction()
        {
            bool pc=!Application.isMobilePlatform;bool carrying=session.Active;
            switch(Current)
            {
                case Step.Move: return "Use the joystick to walk around the lab."+(pc?"  (W A S D)":"");
                case Step.Look: return "Drag on the right side of the screen to look around."+(pc?"  (drag with mouse or touchpad)":"");
                case Step.Pick: return Mode==HandMode.Tap?"Tap PICK to grab the item you are aiming at."+(pc?"  (E)":""):"Aim the + at any item on the floor. PICK appears when you can grab it.";
                case Step.Place:
                    if(!carrying)return "Pick up an item first.";
                    return Mode==HandMode.Tap?"Tap PLACE. The first item on a rack decides what that rack holds."+(pc?"  (F)":""):GuidedRack?"Walk to the marked rack ("+Mathf.CeilToInt(Vector3.Distance(session.player.transform.position,GuidedRack.transform.position))+" m), then aim the + anywhere on it.":"Find an empty rack on this floor, or one holding the same item.";
                case Step.Throw:
                    if(!carrying)return "Pick up another item first.";
                    if(Mode!=HandMode.Tap)return "Your item will drop forward when there is room.";
                    return "Tap DROP to throw one item gently forward."+(pc?"  (Q)":"");
                case Step.Tools: return Time.unscaledTime<placedHintUntil?"Nice! This rack now holds that item type.":"DROP throws one item gently forward. Select a type in your hand to use SEARCH or MAGNET. Get tools with cash, optional reward videos or the store. Tap GOT IT to continue.";
                default: return paid?"Well done! +$"+reward+"\nNow sort every item to finish the lab.":"Well done!\nNow sort every item to finish the lab.";
            }
        }
        void Hide(){HideRackGuide();Mode=HandMode.Hidden;handAlpha=0;if(hand)hand.gameObject.SetActive(false);if(group){group.alpha=0;group.blocksRaycasts=false;}}
    }
}

