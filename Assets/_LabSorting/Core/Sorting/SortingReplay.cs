using System.Collections.Generic;
namespace LabSorting.Sorting
{
    public sealed partial class SortingSession
    {
        public string areaId="main-lab";
        public int contentVersion=1;
        public string CompletedArea {get;private set;}
        public int CompletionCount {get;private set;}
        public int CompletedContentVersion {get;private set;}
        public bool HasCompletedArea=>CompletionCount>0;
        bool runCompletionRecorded;
        Snapshot replayLayout;
        void RecordAreaCompletion(bool telemetry=true){if(runCompletionRecorded)return;runCompletionRecorded=true;CompletedArea=areaId;CompletionCount++;CompletedContentVersion=contentVersion;if(telemetry)LabSorting.Services.LabTelemetry.Event("area_completed",areaId,CompletionCount);}
        void CaptureCompletionHistory(Snapshot data){data.completedArea=CompletedArea;data.completionCount=CompletionCount;data.completedContentVersion=CompletedContentVersion;data.runCompletionRecorded=runCompletionRecorded;}
        void RestoreCompletionHistory(Snapshot data)
        {
            CompletedArea=data.completedArea;CompletionCount=data.completionCount;CompletedContentVersion=data.completedContentVersion;runCompletionRecorded=data.runCompletionRecorded;
            // Migrate already completed saves without awarding cash again.
            if(!runCompletionRecorded&&data.milestones!=null&&data.milestones.Contains("complete"))RecordAreaCompletion(false);
        }
        public bool ReplayCompletedArea()
        {
            if(replayLayout==null||items.Length==0||SortedCount!=items.Length||!HasMilestone("complete"))return false;
            if(persistence&&(!RequestSave(true)||!FlushSaves()))return false;
            var previous=Capture();var next=new Snapshot{layoutRevision=layoutRevision,items=replayLayout.items,playerPosition=replayLayout.playerPosition,look=replayLayout.look,capacity=capacity,cash=cash,tutorialRewarded=TutorialRewarded,completedRacks=new List<string>()};
            CaptureSupplies(next);next.runCompletionRecorded=false;
            foreach(var item in items)item.CompleteMotion();
            if(!Restore(next))return false;
            InitializeSaveCache();
            if(!SaveRedundantTransaction()){Restore(previous);InitializeSaveCache();SaveRedundantTransaction();return false;}
            LabSorting.Services.LabTelemetry.Event("area_replay_started",areaId,CompletionCount);Commit("New sorting run started");return true;
        }
    }
}
