using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using LabSorting.Services;
namespace LabSorting.UI
{
    // Settings language picker. While the game ships English only (LabLocalization.EnglishOnly) it shows a
    // locked "English" with a "more soon" note; turning that flag off lists every available locale.
    public sealed class LanguageDropdown:MonoBehaviour
    {
        public Dropdown dropdown;
        [Tooltip("Shown while only English is offered.")]
        public GameObject comingSoon;
        readonly List<Locale> locales=new List<Locale>();
        void Awake(){if(!dropdown)dropdown=GetComponent<Dropdown>();if(dropdown)dropdown.onValueChanged.AddListener(Choose);}
        void OnEnable(){Refresh();}
        public bool Locked=>LabLocalization.EnglishOnly||locales.Count<2;
        public void Refresh()
        {
            if(!dropdown)return;
            locales.Clear();var names=new List<string>();int selected=0;
            if(!LabLocalization.EnglishOnly&&LocalizationSettings.InitializationOperation.IsDone)
            {
                foreach(var locale in LocalizationSettings.AvailableLocales.Locales)
                {
                    if(locale==LocalizationSettings.SelectedLocale)selected=locales.Count;
                    locales.Add(locale);var native=locale.Identifier.CultureInfo!=null?locale.Identifier.CultureInfo.NativeName:locale.LocaleName;
                    names.Add(string.IsNullOrEmpty(native)?locale.LocaleName:char.ToUpperInvariant(native[0])+native.Substring(1));
                }
            }
            if(names.Count==0)names.Add("English");
            dropdown.ClearOptions();dropdown.AddOptions(names);dropdown.SetValueWithoutNotify(selected);dropdown.RefreshShownValue();
            dropdown.interactable=!Locked;if(comingSoon)comingSoon.SetActive(Locked);
        }
        void Choose(int index)
        {
            if(Locked||index<0||index>=locales.Count)return;
            var locale=locales[index];PlayerPrefs.SetString("Lab.Language",locale.Identifier.Code);PlayerPrefs.Save();LocalizationSettings.SelectedLocale=locale;
        }
    }
}
