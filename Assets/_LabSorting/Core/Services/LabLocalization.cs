using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
namespace LabSorting.Services
{
    public sealed class LabLocalization:MonoBehaviour
    {
        public static bool EnglishOnly=>true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void UseEnglish(){PlayerPrefs.SetString("Lab.Language","en");}
        static StringTable table;
        static LabLocalization owner;
        static Regex phrases;
        static readonly Dictionary<string,string> replacements=new Dictionary<string,string>();
        public static bool Ready=>table!=null;
        void Awake(){owner=this;table=null;phrases=null;cache.Clear();}
        static readonly Dictionary<string,string> cache=new Dictionary<string,string>();
        public static int Revision {get;private set;}
        async void Start(){await LocalizationSettings.InitializationOperation.Task;if(!this)return;var code=EnglishOnly?"en":PlayerPrefs.GetString("Lab.Language","en");var locale=LocalizationSettings.AvailableLocales.GetLocale(code);if(locale)LocalizationSettings.SelectedLocale=locale;LocalizationSettings.SelectedLocaleChanged+=Changed;Changed(LocalizationSettings.SelectedLocale);}
        void Changed(Locale locale){if(EnglishOnly&&locale&&locale.Identifier.Code!="en"){LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("en");return;}cache.Clear();table=null;phrases=null;Revision++;var handle=LocalizationSettings.StringDatabase.GetTableAsync("LabUI",locale);handle.Completed+=result=>{if(!this||locale!=LocalizationSettings.SelectedLocale)return;table=result.Result;CompilePhrases();cache.Clear();Revision++;};if(locale){PlayerPrefs.SetString("Lab.Language",locale.Identifier.Code);PlayerPrefs.Save();}}
        static void CompilePhrases()
        {
            replacements.Clear();phrases=null;if(!table)return;
            foreach(var entry in table.Values){var key=entry.Key;var value=entry.LocalizedValue;if(!string.IsNullOrEmpty(key)&&!string.IsNullOrEmpty(value)&&key!=value)replacements[key]=value;}
            // One nonrecursive pass: longest authored phrases win, numbers/prices are untouched.
            if(replacements.Count>0)phrases=new Regex("(?<![\\p{L}])(?:"+string.Join("|",replacements.Keys.OrderByDescending(k=>k.Length).Select(Regex.Escape))+")(?![\\p{L}])",RegexOptions.CultureInvariant);
        }
        public static string Text(string source){if(string.IsNullOrEmpty(source)||!table)return source;if(cache.TryGetValue(source,out var value))return value;value=table.GetEntry(source)?.LocalizedValue;if(string.IsNullOrEmpty(value)||value==source)value=phrases==null?source:phrases.Replace(source,m=>replacements[m.Value]);if(cache.Count>=512)cache.Clear();cache[source]=value;return value;}
        public static void NextLanguage(){if(EnglishOnly||!Ready)return;var locales=LocalizationSettings.AvailableLocales.Locales;if(locales.Count>0)LocalizationSettings.SelectedLocale=locales[(locales.IndexOf(LocalizationSettings.SelectedLocale)+1)%locales.Count];}
        void OnDestroy(){LocalizationSettings.SelectedLocaleChanged-=Changed;if(owner==this){owner=null;table=null;cache.Clear();}}
    }
}
