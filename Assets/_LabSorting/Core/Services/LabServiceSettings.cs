using UnityEngine;

namespace LabSorting.Services
{
    [CreateAssetMenu(menuName="Lab Sorting/Service Settings")]
    public sealed class LabServiceSettings : ScriptableObject
    {
        [Header("Lab Sorting accounts only. Blank until registered.")]
        public string androidPackageName;
        public bool firebaseConfigurationAdded;
        public string maxSdkKey, androidInterstitialId, androidRewardedId;
        public string googleMobileAdsAppId;
        public string removeAdsProductId, smallCashProductId, largeCashProductId;
        public string searchPackProductId,magnetPackProductId,slotPackProductId;
        [Min(1)] public int searchPackAmount=25,magnetPackAmount=10,slotPackAmount=5;
        [Min(1)] public int rewardedCashAmount=50,rewardedSearchAmount=3,rewardedMagnetAmount=2,rewardedSlotAmount=1;
        [Min(1)] public int smallCashAmount=1000, largeCashAmount=5000;
        [TextArea] public string googlePlayPublicKey;
        public string privacyPolicyUrl;
        public string tenjinApiKey;
        public bool underAgeOfConsent;
        public bool enableLiveServices;
        [Tooltip("Advertising IDs registered as test devices in MAX. No live ads by default.")]
        public string[] testDeviceAdvertisingIds = new string[0];
        [Min(60)] public float interstitialCooldownSeconds=180;
    }
}
