using System.IO;
using UnityEditor.Android;
namespace LabSorting.Editor
{
    // Review and Task APIs are called through JNI, so R8 cannot discover their C# references.
    public sealed class LabReviewAndroidKeep:IPostGenerateGradleAndroidProject
    {
        public int callbackOrder=>100;
        public void OnPostGenerateGradleAndroidProject(string path)=>EnsureRules(path);
        public static void EnsureRules(string path)
        {
            string file=Path.Combine(path,"proguard-unity.txt");const string marker="# Lab Sorting native review JNI";
            string text=File.Exists(file)?File.ReadAllText(file):"";if(text.Contains(marker))return;
            File.WriteAllText(file,text+"\n"+marker+"\n-keep class com.google.android.play.core.review.** { *; }\n-keep class com.google.android.gms.tasks.** { *; }\n");
        }
    }
}
