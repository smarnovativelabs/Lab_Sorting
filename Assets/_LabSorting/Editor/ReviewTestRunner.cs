using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace LabSorting.Editor
{
    public sealed class ReviewTestRunner : ICallbacks
    {
        static TestRunnerApi api;
        static string reportPath="Logs/ReviewTests.xml";
        public static void RunCompact(){RunSelected(new[]{"DesignerCompactControls","DesignerBulkAndIcons","DesignerSoftDropAndRackTargeting","CoreSortingAndController"},"Logs/CompactControlsTests.xml");}
        public static void RunCompactFocused(){RunSelected(new[]{"DesignerCompactControls"},"Logs/CompactControlsFocusedTests.xml");}
        public static void RunReferenceFocused(){RunSelected(new[]{"DesignerReferenceCorrections"},"Logs/ReferenceFocusedTests.xml");}
        public static void RunReference(){RunSelected(new[]{"DesignerReferenceCorrections","DesignerBulkAndIcons","DesignerSoftDropAndRackTargeting","CoreSortingAndController"},"Logs/ReferenceCorrectionTests.xml");}
        public static void RunBulk(){RunSelected(new[]{"DesignerBulkAndIcons","DesignerSoftDropAndRackTargeting","CoreSortingAndController"},"Logs/BulkIconTests.xml");}
        public static void RunRackHints(){RunSelected(new[]{"DesignerRackHints","DesignerSoftDropAndRackTargeting","DesignerMaterialFeedback"},"Logs/RackHintTests.xml");}
        public static void RunMaterials(){RunSelected(new[]{"DesignerMaterialFeedback","DesignerSoftDropAndRackTargeting"},"Logs/MaterialFeedbackTests.xml");}
        public static void RunToss(){RunSelected(new[]{"ReviewedRackAndTossSafety","DesignerSoftDropAndRackTargeting"},"Logs/ReviewTossTests.xml");}
        public static void RunSoft(){RunSelected(new[]{"CoreSortingAndController","DesignerSoftDropAndRackTargeting","DesignerGuidedTutorial","DesignerAssetsAndGameplay"},"Logs/SoftSortingTests.xml");}
        public static void RunSoftRetest(){RunSelected(new[]{"CoreSortingAndController","DesignerGuidedTutorial"},"Logs/SoftSortingRetest.xml");}
        public static void RunDesigner(){RunSelected(new[]{"DesignerAssetsAndGameplay"},"Logs/ReviewDesignerTests.xml");}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before tests");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save current scene before tests");
            var names=new[]{"ReviewedRackAndTossSafety","CoreSortingAndController","DesignerAssetsAndGameplay","DesignerSoftDropAndRackTargeting","DesignerGuidedTutorial","IntegrationValidatorDetectsBadContent","ItemShadowsDisabledInScenesAndPrefabs","MainMenuAutomaticallyLoadsDesignerScene","PlayerHeightMatchesPrefabAndSavedScene","PlayerPrefabHasNoPrototypeDependencies","StressCatalogPerformance"};
            RunSelected(names,"Logs/ReviewTests.xml");
        }
        static void RunSelected(string[] names,string path)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before tests");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save current scene before tests");
            reportPath=path;api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new ReviewTestRunner());
            api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=names.Select(n=>"SortingIntegrationTests."+n).ToArray()}));
        }
        public void RunStarted(ITestAdaptor tests){}
        public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor result){Debug.Log("REVIEW_TEST: "+result.Name+" "+result.TestStatus);}
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result,reportPath);
            Debug.Log("REVIEW_TESTS_FINISHED passed="+result.PassCount+" failed="+result.FailCount);
            api.UnregisterCallbacks(this);UnityEngine.Object.DestroyImmediate(api);
            // Leave scene restoration to the caller after the runner has fully stopped.
            // A delayed OpenScene callback can otherwise fire inside the next run's Play Mode.
        }
    }
}
