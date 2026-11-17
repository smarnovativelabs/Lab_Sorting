using LabSorting.Services;
using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    public sealed class ServicesPanel:MonoBehaviour
    {
        public SupplyCommerce commerce;public SettingsTutorialPanel settingsPanel;
        public GameObject root;public Text status,removeLabel,analyticsLabel,attributionLabel,diagnosticsLabel,languageLabel;public Button removeButton;
        public void Open(){if(!settingsPanel.IsOpen)return;HideTelemetryOptions();root.SetActive(true);root.transform.SetAsLastSibling();Refresh();}
        public void Close(){if(commerce.Busy)return;root.SetActive(false);}
        public void RemoveAds(){commerce.BuyRemoveAds();Refresh();}
        public void Restore(){commerce.RestorePurchases();Refresh();}
        public void Retry(){commerce.RetryServices();Refresh();}
        public void Privacy(){commerce.ShowPrivacyOptions();}
        public void Policy(){if(commerce.settings&&System.Uri.TryCreate(commerce.settings.privacyPolicyUrl,System.UriKind.Absolute,out var uri)&&uri.Scheme=="https")Application.OpenURL(uri.AbsoluteUri);}
        public void Attribution(){LabTelemetry.SetAttribution(!LabTelemetry.AttributionAllowed);Refresh();}
        public void Language(){LabLocalization.NextLanguage();Refresh();}
        void HideTelemetryOptions(){foreach(var label in new[]{analyticsLabel,diagnosticsLabel}){if(!label)continue;var button=label.GetComponentInParent<Button>();if(button)button.gameObject.SetActive(false);}}
        void OnEnable(){HideTelemetryOptions();if(LabLocalization.EnglishOnly&&languageLabel){var button=languageLabel.GetComponentInParent<Button>();if(button)button.gameObject.SetActive(false);}}
        float nextRefresh;
        void Update(){if(root.activeSelf&&!settingsPanel.IsOpen)root.SetActive(false);if(root.activeSelf&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.25f;Refresh();}}
        void Refresh(){removeLabel.text=commerce.AdsRemoved?"ADS REMOVED":"REMOVE ADS  "+(commerce.RemoveAdsPrice??"Unavailable");removeButton.interactable=!commerce.AdsRemoved&&!commerce.Busy&&commerce.RemoveAdsPrice!=null;status.text=Application.internetReachability==NetworkReachability.NotReachable?"Internet is required to play.":commerce.Status;attributionLabel.text="Attribution: "+(LabTelemetry.AttributionAllowed?"ON":"OFF");languageLabel.text="Language: "+(UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale?.LocaleName??"English");}
    }
}
