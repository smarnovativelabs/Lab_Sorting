using UnityEngine;
namespace LabSorting.UI
{
    public sealed class TutorialRackTurn:MonoBehaviour
    {
        public GuidedTutorial tutorial;
        public bool Turning {get;private set;}
        float elapsed,startTime,startYaw,startPitch,targetYaw,targetPitch,duration;
        float expectedYaw,expectedPitch;Vector3 origin;
        string Key=>tutorial.preferencesPrefix+"RackTurnShown.v1";
        void Update()
        {
            var s=tutorial.session;var player=s.player;
            if(!tutorial.IsRunning||tutorial.Current!=GuidedTutorial.Step.Place||player.blocked||!s.Active){Turning=false;return;}
            if(!Turning)
            {
                if(PlayerPrefs.GetInt(Key,0)!=0||!tutorial.GuidedRack||s.Active.Moving)return;
                if(Time.unscaledTime-player.LastLookInputTime<.5f)return;
                var rack=tutorial.GuidedRack;var point=rack.label?rack.label.transform.position:rack.transform.position+Vector3.up*1.4f;
                Vector3 direction=point-player.view.transform.position;targetYaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;targetPitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                startYaw=expectedYaw=player.look.Yaw;startPitch=expectedPitch=player.look.Pitch;origin=player.transform.position;duration=Mathf.Max(.8f,Mathf.Abs(Mathf.DeltaAngle(startYaw,targetYaw))/65f);elapsed=0;startTime=Time.unscaledTime;Turning=true;PlayerPrefs.SetInt(Key,1);PlayerPrefs.Save();LabSorting.Services.LabTelemetry.Event("tutorial_rack_turn_started");
            }
            if(player.LastLookInputTime>=startTime||player.Move.sqrMagnitude>.01f||Vector3.Distance(origin,player.transform.position)>.4f||Mathf.Abs(Mathf.DeltaAngle(expectedYaw,player.look.Yaw))>3||Mathf.Abs(expectedPitch-player.look.Pitch)>3){Turning=false;LabSorting.Services.LabTelemetry.Event("tutorial_rack_turn_cancelled");return;}
            elapsed+=Mathf.Min(Time.unscaledDeltaTime,.05f);float t=Mathf.SmoothStep(0,1,elapsed/duration);
            player.look.SetYawPitch(new Vector2(Mathf.LerpAngle(startYaw,targetYaw,t),Mathf.Lerp(startPitch,targetPitch,t)));
            expectedYaw=player.look.Yaw;expectedPitch=player.look.Pitch;
            if(elapsed>=duration){Turning=false;LabSorting.Services.LabTelemetry.Event("tutorial_rack_turn_completed");}
        }
        void OnDisable(){Turning=false;}
    }
}
