using LabSorting.Services;
using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    public sealed class ServicesPanel:MonoBehaviour
    {
        public SupplyCommerce commerce;public SettingsTutorialPanel settingsPanel;
        public GameObject root;public Text status,removeLabel,analyticsLabel,attributionLabel,diagnosticsLabel,languageLabel;public Button removeButton;
        Button adDebugger;
        public void Open(){if(!settingsPanel.IsOpen)return;HideTelemetryOptions();root.SetActive(true);root.transform.SetAsLastSibling();Refresh();}
        public void Close(){if(commerce.Busy)return;root.SetActive(false);}
        public void RemoveAds(){commerce.BuyRemoveAds();Refresh();}
        public void Restore(){commerce.RestorePurchases();Refresh();}
        public void Retry(){commerce.RetryServices();Refresh();}
        public void Privacy(){commerce.ShowPrivacyOptions();}
        public void AdDebugger(){commerce.ShowAdDebugger();Refresh();}
        public void Policy(){if(commerce.settings&&System.Uri.TryCreate(commerce.settings.privacyPolicyUrl,System.UriKind.Absolute,out var uri)&&uri.Scheme=="https")Application.OpenURL(uri.AbsoluteUri);}
        public void Language(){LabLocalization.NextLanguage();Refresh();}
        void HideTelemetryOptions(){foreach(var label in new[]{analyticsLabel,attributionLabel,diagnosticsLabel}){if(!label)continue;var button=label.GetComponentInParent<Button>();if(button)button.gameObject.SetActive(false);}}
        void OnEnable(){HideTelemetryOptions();if(LabLocalization.EnglishOnly&&languageLabel){var button=languageLabel.GetComponentInParent<Button>();if(button)button.gameObject.SetActive(false);}CreateAdDebugger();}
        // Development builds only (never in a release build): an AD DEBUGGER button in the slot of the hidden Analytics
        // button opens the MAX Mediation Debugger, and the status line shows the live ad pipeline state.
        void CreateAdDebugger()
        {
            if(adDebugger||!Debug.isDebugBuild||!root)return;
            var template=root.transform.Find("Retry");if(!template)return;
            var slot=analyticsLabel?analyticsLabel.GetComponentInParent<Button>(true):null;
            var copy=Instantiate(template.gameObject,template.parent,false);copy.name="AdDebugger";
            if(slot)((RectTransform)copy.transform).anchoredPosition=((RectTransform)slot.transform).anchoredPosition;
            adDebugger=copy.GetComponent<Button>();adDebugger.onClick=new Button.ButtonClickedEvent();adDebugger.onClick.AddListener(AdDebugger);
            var label=copy.GetComponentInChildren<Text>(true);if(label)label.text="AD DEBUGGER";
            copy.SetActive(true);
        }
        float nextRefresh;
        void Update(){if(root.activeSelf&&!settingsPanel.IsOpen)root.SetActive(false);if(root.activeSelf&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.25f;Refresh();}}
        void Refresh()
        {
            removeLabel.text=commerce.AdsRemoved?"ADS REMOVED":"REMOVE ADS  "+(commerce.RemoveAdsPrice??"Unavailable");removeButton.interactable=!commerce.AdsRemoved&&!commerce.Busy&&commerce.RemoveAdsPrice!=null;
            status.text=Application.internetReachability==NetworkReachability.NotReachable?"Internet is required to play.":commerce.Status;
            if(Debug.isDebugBuild)status.text=(string.IsNullOrEmpty(status.text)?"":status.text+"\n")+"[dev] "+commerce.AdDiagnostics;
            languageLabel.text="Language: "+(UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale?.LocaleName??"English");
        }
    }
}
