using UnityEngine;

namespace LabSorting.Services
{
    [CreateAssetMenu(menuName="Lab Sorting/Service Settings")]
    public sealed class LabServiceSettings : ScriptableObject
    {
        [Header("Android release configuration")]
        public string appTitle = "Quarantine Lab sort Simulator";
        public string androidPackageName;
        public bool firebaseConfigurationAdded;
        public string maxSdkKey, androidInterstitialId, androidRewardedId;
        public string googleMobileAdsAppId;
        [Tooltip("Configure these in the MAX dashboard's Google mediated placements; runtime ads use the MAX IDs above.")]
        public string admobInterstitialId, admobRewardedId;
        public string removeAdsProductId, smallCashProductId, largeCashProductId;
        public string searchPackProductId,magnetPackProductId,slotPackProductId;
        [Min(1)] public int searchPackAmount=25,magnetPackAmount=10,slotPackAmount=5;
        [Min(1)] public int rewardedCashAmount=50,rewardedSearchAmount=3,rewardedMagnetAmount=2,rewardedSlotAmount=1;
        [Min(1)] public int smallCashAmount=1000, largeCashAmount=5000;
        [TextArea] public string googlePlayPublicKey;
        public string privacyPolicyUrl;
        public string tenjinApiKey;
        [Header("iOS advertising and attribution")]
        public string iosInterstitialId,iosRewardedId,iosGoogleMobileAdsAppId;
        [Tooltip("Configure these AdMob placements in the MAX iOS mediation dashboard.")]
        public string iosAdmobInterstitialId,iosAdmobRewardedId,iosTenjinApiKey;
        [Header("Apple App Store (same product IDs as the catalog above)")]
        public string iosBundleId;
        public string iosTrackingUsageDescription="Your device information helps us show relevant ads and measure ad performance.";
        public string InterstitialId=>Application.platform==RuntimePlatform.IPhonePlayer?iosInterstitialId:androidInterstitialId;
        public string RewardedId=>Application.platform==RuntimePlatform.IPhonePlayer?iosRewardedId:androidRewardedId;
        public string MobileAdsAppId=>Application.platform==RuntimePlatform.IPhonePlayer?iosGoogleMobileAdsAppId:googleMobileAdsAppId;
        public string TenjinApiKey=>Application.platform==RuntimePlatform.IPhonePlayer?iosTenjinApiKey:tenjinApiKey;
        public bool underAgeOfConsent;
        public bool enableLiveServices;
        [Tooltip("Advertising IDs registered as test devices in MAX. No live ads by default.")]
        public string[] testDeviceAdvertisingIds = new string[0];
        [Tooltip("Seconds of active gameplay between automatic interstitials. Also restarts after every rewarded video.")]
        [Min(30)] public float interstitialCooldownSeconds=120;
        [Tooltip("On-screen 5, 4, 3, 2, 1 warning shown before an automatic interstitial.")]
        [Range(1,10)] public int interstitialWarningSeconds=5;
        [Header("Editor testing")]
        [Tooltip("Editor only: MAX placeholder ads and Unity IAP's fake store (fake $0.01 prices, receipts accepted without Google Play validation). Never affects device builds.")]
        public bool editorTestServices=true;
    }
}
