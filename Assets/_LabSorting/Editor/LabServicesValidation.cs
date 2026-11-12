using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using LabSorting.Services;
namespace LabSorting.Editor
{
    public sealed class LabServicesValidation:IPreprocessBuildWithReport
    {
        public int callbackOrder=>-2000;
        public static List<string> Problems(LabServiceSettings s)
        {
            var errors=new List<string>();if(!s){errors.Add("Missing LabServiceSettings");return errors;}
            var ids=new[]{s.removeAdsProductId,s.smallCashProductId,s.largeCashProductId,s.magnetPackProductId,s.searchPackProductId,s.slotPackProductId};
            if(ids.Any(string.IsNullOrWhiteSpace)||ids.Distinct().Count()!=6)errors.Add("Configure six unique product IDs");
            if(!s.enableLiveServices)return errors;
            if(string.IsNullOrWhiteSpace(s.androidPackageName))errors.Add("Android package name is missing");
            if(string.IsNullOrWhiteSpace(s.googlePlayPublicKey))errors.Add("Google Play licensing public key is missing");
            if(string.IsNullOrWhiteSpace(s.maxSdkKey)||string.IsNullOrWhiteSpace(s.androidRewardedId)||string.IsNullOrWhiteSpace(s.androidInterstitialId)||string.IsNullOrWhiteSpace(s.googleMobileAdsAppId))errors.Add("MAX/AdMob account IDs are missing");
            if(string.IsNullOrWhiteSpace(s.tenjinApiKey))errors.Add("Tenjin API key is missing");
            if(!Uri.TryCreate(s.privacyPolicyUrl,UriKind.Absolute,out var url)||url.Scheme!="https")errors.Add("HTTPS privacy policy URL is missing");
            if(!s.firebaseConfigurationAdded||!File.Exists("Assets/google-services.json"))errors.Add("Add Lab Sorting Firebase google-services.json to Assets and enable firebaseConfigurationAdded");
            else if(!System.Text.RegularExpressions.Regex.Matches(File.ReadAllText("Assets/google-services.json"),"\"package_name\"\\s*:\\s*\"([^\"]+)\"").Cast<System.Text.RegularExpressions.Match>().Any(m=>m.Groups[1].Value==s.androidPackageName))errors.Add("Firebase config does not match the Lab Sorting Android package");
            if(!string.IsNullOrWhiteSpace(s.googlePlayPublicKey)){try{if(Convert.FromBase64String(s.googlePlayPublicKey).Length<128)errors.Add("Google Play licensing key is incomplete");}catch{errors.Add("Google Play licensing key is not valid Base64");}}
            return errors;
        }
        [MenuItem("Tools/Lab Sorting/Services/Validate configuration")]
        public static void Validate(){var s=AssetDatabase.LoadAssetAtPath<LabServiceSettings>(LabServicesSetup.SettingsPath);var problems=Problems(s);File.WriteAllLines("Logs/LabServiceValidation.txt",problems.Count==0?new[]{s.enableLiveServices?"Account fields present; device/store validation still required.":"Unconfigured mode valid. Live IDs and Firebase config are still required."}:problems);}
        public void OnPreprocessBuild(BuildReport report){var problems=Problems(AssetDatabase.LoadAssetAtPath<LabServiceSettings>(LabServicesSetup.SettingsPath));if(problems.Count>0)throw new BuildFailedException(string.Join("\n",problems));LabServicesSetup.ApplyAccountSettings();}
    }
}
