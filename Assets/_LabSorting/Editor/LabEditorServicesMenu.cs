using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LabSorting.Services;
namespace LabSorting.Editor
{
    // Editor test services (LabServiceSettings.editorTestServices) use the IAP fake store, so buying Remove Ads in the
    // Editor writes a real local entitlement and stops interstitials in Editor play. This clears that local file only.
    public static class LabEditorServicesMenu
    {
        [MenuItem("Tools/Lab Sorting/Services/Editor test: reset Remove Ads purchase")]
        public static void ResetEditorRemoveAds()
        {
            string path=CommerceEntitlements.PathName;
            foreach(var file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);
            typeof(CommerceEntitlements).GetMethod("ResetCacheForTests",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public)?.Invoke(null,null);
            Debug.Log("Editor Remove Ads entitlement cleared: "+path+". Device purchases are not affected.");
        }
    }
}
