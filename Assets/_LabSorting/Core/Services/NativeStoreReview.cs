using System;
using System.Collections;
using UnityEngine;
namespace LabSorting.Services
{
    public enum NativeReviewResult { Requested, Unavailable }
    public static class NativeStoreReview
    {
        // Native completion never proves the dialog was shown or a rating was submitted.
        public static IEnumerator Request(Action<NativeReviewResult> complete)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaObject activity=null,manager=null,task=null,info=null,launch=null;
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))activity=player.GetStatic<AndroidJavaObject>("currentActivity");
                using(var factory=new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory"))manager=factory.CallStatic<AndroidJavaObject>("create",activity);
                task=manager.Call<AndroidJavaObject>("requestReviewFlow");
            }
            catch(Exception e){Debug.Log("Native review unavailable: "+e.GetType().Name);}
            float until=Time.realtimeSinceStartup+15;
            while(task!=null&&!Flag(task,"isComplete")&&Time.realtimeSinceStartup<until)yield return null;
            if(task!=null&&Flag(task,"isSuccessful"))
            {
                try{info=task.Call<AndroidJavaObject>("getResult");launch=manager.Call<AndroidJavaObject>("launchReviewFlow",activity,info);}catch(Exception){}
            }
            until=Time.realtimeSinceStartup+60;
            while(launch!=null&&!Flag(launch,"isComplete")&&Time.realtimeSinceStartup<until)yield return null;
            bool requested=launch!=null&&Flag(launch,"isSuccessful");
            launch?.Dispose();info?.Dispose();task?.Dispose();manager?.Dispose();activity?.Dispose();complete(requested?NativeReviewResult.Requested:NativeReviewResult.Unavailable);
#elif UNITY_IOS && !UNITY_EDITOR
            complete(UnityEngine.iOS.Device.RequestStoreReview()?NativeReviewResult.Requested:NativeReviewResult.Unavailable);yield break;
#else
            complete(NativeReviewResult.Unavailable);yield break;
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        static bool Flag(AndroidJavaObject task,string name){try{return task.Call<bool>(name);}catch(Exception){return false;}}
#endif
    }
}
