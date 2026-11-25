using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace LabSorting.Editor
{
    [InitializeOnLoad]
    public sealed class CurrentQARunner : ICallbacks
    {
        static TestRunnerApi api;static string report;
        static CurrentQARunner(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            const string request="Logs/CurrentQA.request";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||api)return;
            var lines=File.ReadAllLines(request);File.Delete(request);report=lines[0];
            File.WriteAllText(report+".progress","START "+DateTime.UtcNow.ToString("O")+"\n");
            api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new CurrentQARunner());
            var filter=new Filter{testMode=TestMode.EditMode,assemblyNames=new[]{"LabSorting.Tests"}};
            if(lines.Length>1)filter.testNames=lines.Skip(1).Where(s=>s.Length>0).ToArray();
            api.Execute(new ExecutionSettings(filter));
        }
        public void RunStarted(ITestAdaptor t){}
        public void TestStarted(ITestAdaptor t){if(!t.IsSuite)File.AppendAllText(report+".progress","RUN "+t.FullName+"\n");}
        public void TestFinished(ITestResultAdaptor r)
        {
            if(r.Test.IsSuite)return;
            File.AppendAllText(report+".progress",r.TestStatus+" "+r.FullName+"\n");
            if(r.TestStatus==TestStatus.Failed)File.AppendAllText(report+".failures",r.FullName+"\n"+r.Message+"\n"+r.StackTrace+"\n");
        }
        public void RunFinished(ITestResultAdaptor r)
        {
            TestRunnerApi.SaveResultToFile(r,report);File.AppendAllText(report+".progress","DONE passed="+r.PassCount+" failed="+r.FailCount+"\n");
            api.UnregisterCallbacks(this);UnityEngine.Object.DestroyImmediate(api);api=null;
        }
    }
}
