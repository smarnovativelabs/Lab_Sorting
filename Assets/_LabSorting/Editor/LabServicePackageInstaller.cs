using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace LabSorting.Editor
{
    // Explicit invocation, without -quit. Vendor Firebase/MAX/UMP remain separate
    // verified imports; this step never claims those SDKs are installed.
    public static class LabServicePackageInstaller
    {
        static AddAndRemoveRequest request;
        static double deadline;
        public static void Install()
        {
            var drive=new DriveInfo(Path.GetPathRoot(Application.dataPath));
            if(drive.AvailableFreeSpace<10L*1024*1024*1024)
                throw new InvalidOperationException("Free at least 10 GiB before SDK resolution/import.");
            request=Client.AddAndRemove(new[]{"com.unity.purchasing","com.unity.external-dependency-manager"});
            deadline=EditorApplication.timeSinceStartup+600;
            EditorApplication.update+=Poll;
        }
        static void Poll()
        {
            if(request==null)return;
            if(!request.IsCompleted&&EditorApplication.timeSinceStartup<deadline)return;
            EditorApplication.update-=Poll;
            bool success=request.IsCompleted&&request.Status==StatusCode.Success;
            if(success)Debug.Log("LAB_SERVICE_PACKAGES_RESOLVED: "+string.Join(", ",request.Result.Select(p=>p.name+"@"+p.version)));
            else Debug.LogError(request.Error?.message??"Package resolution timed out");
            if(Application.isBatchMode)EditorApplication.Exit(success?0:1);
        }
    }
}
