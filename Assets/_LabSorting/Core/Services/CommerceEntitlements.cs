using System;
using System.IO;
using UnityEngine;
namespace LabSorting.Services
{
    // Separate from game progress: Reset Progress must never remove a paid entitlement.
    public static class CommerceEntitlements
    {
        public static string PathName=>Path.Combine(Application.persistentDataPath,StorageFile);
#if UNITY_EDITOR
        internal static string StorageFile="lab-entitlements.json";
        internal static void ResetCacheForTests(){cached=null;}
#else
        const string StorageFile="lab-entitlements.json";
#endif
        [Serializable] sealed class Data {public bool removeAds;}
        static bool? cached;
        public static bool RemoveAds=>cached??(cached=Read(PathName)||Read(PathName+".bak")).Value;
        static bool Read(string path){try{return File.Exists(path)&&JsonUtility.FromJson<Data>(File.ReadAllText(path)).removeAds;}catch{return false;}}
        public static bool GrantRemoveAds()
        {
            try{var path=PathName;File.WriteAllText(path+".tmp",JsonUtility.ToJson(new Data{removeAds=true}));using(var stream=new FileStream(path+".tmp",FileMode.Open,FileAccess.ReadWrite,FileShare.None))stream.Flush(true);if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);cached=true;return true;}catch{return false;}
        }
    }
}
