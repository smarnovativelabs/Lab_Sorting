using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace LabSorting.Editor
{
    public sealed class ServiceRecoveryTestRunner:ICallbacks
    {
        static TestRunnerApi api;
        public static void Run()
        {
            api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new ServiceRecoveryTestRunner());
            api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{"ServiceRecoveryTests"}}));
        }
        public void RunStarted(ITestAdaptor tests){}
        public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor test){Debug.Log("SERVICE_RECOVERY: "+test.Name+" "+test.TestStatus);}
        public void RunFinished(ITestResultAdaptor result){TestRunnerApi.SaveResultToFile(result,"Logs/ServiceRecoveryTests.xml");Debug.Log("SERVICE_RECOVERY_FINISHED passed="+result.PassCount+" failed="+result.FailCount);api.UnregisterCallbacks(this);Object.DestroyImmediate(api);}
    }
}
