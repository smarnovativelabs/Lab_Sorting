using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace LabSorting.Editor
{
    public sealed class SortingTestRunner : ICallbacks
    {
        static TestRunnerApi api;
        [InitializeOnLoadMethod]
        static void RunRequestedChecks()
        {
            const string request="Logs/RunEconomyChecks.request";
            if(!System.IO.File.Exists(request)) return;
            EditorApplication.delayCall+=()=>
            {
                System.IO.File.Delete(request);
                EconomySceneUpgrade.Apply();
                Run();
            };
        }
        [MenuItem("Tools/Lab Sorting/Run Integration Tests")]
        public static void Run()
        {
            api=ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new SortingTestRunner());
            api.Execute(new ExecutionSettings(new Filter {testMode=TestMode.EditMode,testNames=new[]{"SortingIntegrationTests"}}));
        }
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            System.IO.Directory.CreateDirectory("Logs");
            TestRunnerApi.SaveResultToFile(result,"Logs/SortingTests.xml");
            Debug.Log("SORTING_TEST_RESULT: "+result.TestStatus+" passed="+result.PassCount+" failed="+result.FailCount);
            api.UnregisterCallbacks(this);Object.DestroyImmediate(api);
        }
    }
}

