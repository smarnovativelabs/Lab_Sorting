#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
namespace LabSorting.Editor
{
    public static class LabTrackingBuild
    {
        [PostProcessBuild(999)] public static void Configure(BuildTarget target,string path)
        {
            if(target!=BuildTarget.iOS)return;
            var settings=AssetDatabase.LoadAssetAtPath<LabSorting.Services.LabServiceSettings>(LabServicesSetup.SettingsPath);
            var plistPath=Path.Combine(path,"Info.plist");var plist=new PlistDocument();plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription",settings.iosTrackingUsageDescription);
            // MAX's automatic ATT flow must not start another unbounded wait after our timeout.
            plist.WriteToFile(plistPath);
            var maxPath=Path.Combine(path,"AppLovin-Settings.plist");
            if(File.Exists(maxPath)){var max=new PlistDocument();max.ReadFromFile(maxPath);if(!max.root.values.TryGetValue("ConsentFlowInfo",out var flow))flow=max.root.CreateDict("ConsentFlowInfo");flow.AsDict().SetBoolean("ConsentFlowEnabled",false);max.WriteToFile(maxPath);}
            var projectPath=PBXProject.GetPBXProjectPath(path);var project=new PBXProject();project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(),"AppTrackingTransparency.framework",true);project.WriteToFile(projectPath);
        }
    }
}
#endif
