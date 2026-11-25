using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LabSorting.Sorting;
using LabSorting.Prototype;
namespace LabSorting.Editor
{
    [InitializeOnLoad]
    public static class PhysicsDropTestRunner
    {
        const string Request="Logs/PhysicsDropReview/run.request";
        static double next;
        static PhysicsDropTestRunner(){EditorApplication.update+=Poll;}
        [MenuItem("Tools/Lab Sorting/Run Physics Drop Checks")]
        static void Run(){Directory.CreateDirectory("Logs/PhysicsDropReview");File.WriteAllText(Request,"run");}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(SceneManager.GetActiveScene().isDirty){File.WriteAllText("Logs/PhysicsDropReview/runner.txt","Blocked: unsaved scene");return;}
                EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");
                EditorApplication.isPlaying=true;return;
            }
            var s=Object.FindFirstObjectByType<SortingSession>();
            if(!s){SceneManager.LoadScene("Assets/Scenes/Lab_Sorting.unity");return;}
            if(!s.Initialized)return;
            bool extended=File.ReadAllText(Request).Trim()=="extended";
            File.Delete(Request);File.WriteAllText("Logs/PhysicsDropReview/runner.txt","Running in "+Application.dataPath);
            Application.runInBackground=true;
            s.StartCoroutine(extended?PhysicsDropEdgeChecks.Run(s):PhysicsDropChecks.Run(s));
        }
    }
}
