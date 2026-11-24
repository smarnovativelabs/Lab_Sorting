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
        public UnityEngine.UI.Button playAgain;
        public SettingsTutorialPanel panel;
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
            bool replay=panel&&panel.session&&panel.session.CanReplayCompletedArea;
            if(playAgain)playAgain.gameObject.SetActive(replay);
            if(replay&&playAgain)
            {
                Place(howToPlay,show?-228:-spacing,show?210:300);
                Place((RectTransform)playAgain.transform,show?0:spacing,show?210:300);
                if(privacyChoices){privacyChoices.SetActive(show);Place((RectTransform)privacyChoices.transform,228,210);}
                return;
            }
            if(privacyChoices){privacyChoices.SetActive(show);var r=(RectTransform)privacyChoices.transform;r.anchoredPosition=new Vector2(spacing,r.anchoredPosition.y);}
            Place(howToPlay,show?-spacing:0,300);
            if(privacyChoices)Place((RectTransform)privacyChoices.transform,spacing,300);
        }
        static void Place(RectTransform rect,float x,float width){if(!rect)return;rect.anchoredPosition=new Vector2(x,rect.anchoredPosition.y);rect.sizeDelta=new Vector2(width,rect.sizeDelta.y);}
    }
}
