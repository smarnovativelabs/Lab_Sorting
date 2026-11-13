#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Prototype
{
    public static class DescriptiveNameChecks
    {
        static void Check(bool ok,string text){if(!ok)throw new Exception("ITEM NAMES: "+text);Debug.Log("ITEM NAMES PASS: "+text);}
        public static IEnumerator Run(SortingSession s)
        {
            var definitions=s.items.Select(i=>i.definition).Distinct().ToDictionary(d=>d.id);var rows=File.ReadAllLines("Assets/_LabSorting/Localization/DescriptiveItemNames.txt").Select(l=>l.Split('|')).Where(p=>p.Length==3).ToArray();
            Check(definitions.Count==46&&definitions.Values.Select(d=>d.displayName).Distinct().Count()==46,"All 46 categories have unique readable names");
            foreach(var row in rows)Check(definitions[row[0]].displayName==row[1],"Verified model descriptor: "+row[1]);
            Check(definitions["lab-first-aid-box"].icon.rect.x==1676&&definitions["lab-first-aid-box-2"].icon.rect.x==76,"Green/red first aid icons match actual prefabs");
            Check(definitions["lab-radio-1"].icon.rect.x==1434&&definitions["lab-radio-2"].icon.rect.x==1086,"Compact/wide radio icons match actual prefabs");
            var label=UnityEngine.Object.FindFirstObjectByType<SortingHUD>().inventoryLabels[0];
            foreach(var locale in new[]{"en","es"})
            {
                var operation=LocalizationSettings.StringDatabase.GetTableAsync("LabUI",LocalizationSettings.AvailableLocales.GetLocale(locale));yield return operation;
                foreach(var row in rows)
                {
                    var value=operation.Result.GetEntry(row[1])?.LocalizedValue;Check(value==row[locale=="en"?1:2],"Localized name exists: "+locale+" "+row[0]);
                    var settings=label.GetGenerationSettings(label.rectTransform.rect.size);settings.resizeTextForBestFit=false;settings.fontSize=label.resizeTextMinSize;
                    // Counts now have their own column; measure the name as actually rendered.
                    float height=label.cachedTextGeneratorForLayout.GetPreferredHeight(value,settings)/label.pixelsPerUnit;
                    Check(height<=label.rectTransform.rect.height+2,"Inventory name fits: "+value);
                }
            }
            var saved=s.Capture();int cash=s.Cash;
            try
            {
                var green=s.items.First(i=>i.definition.id=="lab-first-aid-box");var red=s.items.First(i=>i.definition.id=="lab-first-aid-box-2");var rack=s.racks.First(r=>r.Count==0);
                Check(s.TryPick(green),"Green variant picks");DG.Tweening.DOTween.CompleteAll();Check(s.TryPlace(rack),"Green variant places");DG.Tweening.DOTween.CompleteAll();
                Check(s.TryPick(red),"Red variant picks");DG.Tweening.DOTween.CompleteAll();Check(!s.TryPlace(rack),"Renaming preserves variant-specific rack rules");
                var snapshot=s.Capture();Check(s.Restore(snapshot)&&s.Active.definition==red.definition&&s.Cash==cash,"Snapshot preserves variant IDs and wallet after rename");
            }
            finally{s.Restore(saved);}
        }
    }
}
#endif
