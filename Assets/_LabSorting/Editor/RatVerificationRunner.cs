using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LabSorting.Editor
{
    // A narrow local test request bridge for editor sessions without a connected MCP.
    [InitializeOnLoad]
    public sealed class RatVerificationRunner : ICallbacks
    {
        const string Request="Logs/RatVerification.request";
        static TestRunnerApi api;
        static double nextPoll;
        static string report;
        static RatVerificationRunner(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            if(api||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            var request=File.ReadAllText(Request).Trim();if(request.Length==0)return;
            File.WriteAllText(Request,"");
            try
            {
                if(request=="inspect-layout"){RatLayoutInspection.Capture();File.WriteAllText("Logs/RatVerificationState.txt","Rat layout inspected");return;}
                if(request=="visibility"){SetReviewSize();Start(request);return;}
                if(request=="visibility-preview"){SetReviewSize();Start(request);return;}
                if(request=="crowded-drop"){SetReviewSize();Start(request);return;}
                if(request=="settling-prototype"){SetReviewSize();Start(request);return;}
                if(request=="approved-upgrade"){SetReviewSize();Start(request);return;}
                if(request=="rack-guidance"){SetReviewSize();Start(request);return;}
                if(request=="replay-content"){SetReviewSize();Start(request);return;}
                if(request=="resize"){RatSizingUpgrade.Apply();File.WriteAllText("Logs/RatVerificationState.txt","Rat sizing applied");return;}
                if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save current scene before verification");
                if(request=="restore"){EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");File.WriteAllText("Logs/RatVerificationState.txt","Main scene restored");return;}

                if(!request.StartsWith("held-")&&request!="all"&&request!="core"&&request!="rats"&&request!="polish"&&request!="polish16"&&request!="connectivity"&&request!="regression"&&request!="corrections")throw new InvalidOperationException("Unknown verification request");
                if(request=="polish16")SetReviewSize();
                if(request.StartsWith("held-")){var dimensions=request.Substring(5).Split('x');SetReviewSize(int.Parse(dimensions[0]),int.Parse(dimensions[1]));}
                Start(request);
            }
            catch(Exception e){File.WriteAllText("Logs/RatVerificationState.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Lab Sorting/Verify Rats and Core")]
        public static void Run(){Start("all");}
        static void SetReviewSize(int width=1280,int height=720)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes=singleton.GetProperty("instance").GetValue(null);
            var group=sizesType.GetProperty("currentGroup").GetValue(sizes);
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");
            var mode=Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
            var size=Activator.CreateInstance(sizeType,new object[]{mode,width,height,"Lab held-item review"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(view,count-1);
        }
        static void Start(string mode)
        {
            if(api||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Another verification is running");
            report="Logs/RatVerification-"+mode+".xml";
            var names=mode=="polish"?new[]{"SortingIntegrationTests.RequestedGameplayPolish"}:mode=="connectivity"?new[]{"ConnectivityPanelTests.OfflineOnlinePanelRecovery"}:mode=="core"?new[]{"SortingIntegrationTests.CoreSortingAndController"}:mode=="rats"?new[]{"SortingIntegrationTests.RatsPickPlaceThrowSave"}:new[]{"SortingIntegrationTests.CoreSortingAndController","SortingIntegrationTests.RatsPickPlaceThrowSave"};
            File.WriteAllText("Logs/RatVerificationState.txt","Running "+mode);
            if(mode.StartsWith("held-"))names=new[]{"SortingIntegrationTests.HeldItemSizing"};
            if(mode=="corrections")names=new[]{"SortingIntegrationTests.RatPoseAndPresentationCorrections"};
            if(mode=="visibility")names=new[]{"SortingIntegrationTests.ReadableHoverAndSearch","SortingIntegrationTests.DesignerThumbControls"};
            if(mode=="visibility-preview")names=new[]{"SortingIntegrationTests.ReadableHoverAndSearch"};
            if(mode=="crowded-drop")names=new[]{"SortingIntegrationTests.RealLabCrowdedDrops"};
            if(mode=="settling-prototype")names=new[]{"SortingIntegrationTests.IsolatedSettlingPrototype"};
            if(mode=="approved-upgrade")names=new[]{"SortingIntegrationTests.RealLabCrowdedDrops","SortingIntegrationTests.RackGuidanceUpgrade"};
            if(mode=="replay-content")names=new[]{"SortingIntegrationTests.DesignerOffersReplay"};
            if(mode=="rack-guidance")names=new[]{"SortingIntegrationTests.RackGuidanceUpgrade"};
            if(mode=="polish16")names=new[]{"SortingIntegrationTests.RequestedGameplayPolish"};
            if(mode=="regression")names=new[]{"SortingIntegrationTests.RatsPickPlaceThrowSave","SortingIntegrationTests.DesignerGuidedTutorial","SortingIntegrationTests.DesignerFrontOnlyDrop","ConnectivityPanelTests.OfflineOnlinePanelRecovery"};
            api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new RatVerificationRunner());
            api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=names}));
        }
        public void RunStarted(ITestAdaptor tests){}
        public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor result){Debug.Log("RAT_VERIFY: "+result.Name+" "+result.TestStatus);}
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result,report);
            File.WriteAllText("Logs/RatVerificationState.txt","Finished: passed="+result.PassCount+" failed="+result.FailCount);
            TestRunnerApi.UnregisterTestCallback(this);
            if(api)UnityEngine.Object.DestroyImmediate(api);api=null;
        }
    }
}
