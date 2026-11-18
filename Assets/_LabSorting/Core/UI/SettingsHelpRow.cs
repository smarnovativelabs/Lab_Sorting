using UnityEngine;
using GoogleMobileAds.Ump.Api;
namespace LabSorting.UI
{
    // Settings help row: HOW TO PLAY, plus PRIVACY CHOICES only when Google's consent SDK (UMP) says this player
    // must be able to change their ad consent (EEA/UK). With one button it sits centred. Re-checked each time
    // the settings page opens, since the requirement is known only after consent gathering.
    public sealed class SettingsHelpRow:MonoBehaviour
    {
        public RectTransform howToPlay;
        public GameObject privacyChoices;
        public float spacing=170;
        public static bool PrivacyRequired
        {
            get
            {
                if(Application.isEditor)return false;
                try{return ConsentInformation.PrivacyOptionsRequirementStatus==PrivacyOptionsRequirementStatus.Required;}
                catch(System.Exception){return false;}
            }
        }
        void OnEnable(){Refresh();}
        public void Refresh()
        {
            bool show=PrivacyRequired;
            if(privacyChoices){privacyChoices.SetActive(show);var r=(RectTransform)privacyChoices.transform;r.anchoredPosition=new Vector2(spacing,r.anchoredPosition.y);}
            if(howToPlay)howToPlay.anchoredPosition=new Vector2(show?-spacing:0,howToPlay.anchoredPosition.y);
        }
    }
}
