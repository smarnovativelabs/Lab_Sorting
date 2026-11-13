using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class DescriptiveItemNames
    {
        public static void Apply()
        {
            var session=UnityEngine.Object.FindFirstObjectByType<SortingSession>();
            var definitions=session.items.Select(i=>i.definition).Distinct().ToDictionary(d=>d.id);
            var icons=definitions.ToDictionary(p=>p.Key,p=>p.Value.icon);
            var collection=LocalizationEditorSettings.GetStringTableCollection("LabUI");
            var en=collection.StringTables.First(t=>t.LocaleIdentifier.Code=="en");var es=collection.StringTables.First(t=>t.LocaleIdentifier.Code=="es");
            var report=new System.Text.StringBuilder();
            foreach(var line in File.ReadAllLines("Assets/_LabSorting/Localization/DescriptiveItemNames.txt"))
            {
                var fields=line.Split('|');if(fields.Length!=3)continue;
                var d=definitions[fields[0]];report.AppendLine(d.id+" | "+d.displayName+" -> "+fields[1]);d.displayName=fields[1];EditorUtility.SetDirty(d);en.AddEntry(fields[1],fields[1]);es.AddEntry(fields[1],fields[2]);
            }
            // Match verified prefab colours/front faces; source atlas orders differ from prefab IDs.
            SetIcon(definitions["lab-first-aid-box"],icons["lab-first-aid-box"],icons["lab-first-aid-box-2"],1676);
            SetIcon(definitions["lab-first-aid-box-2"],icons["lab-first-aid-box"],icons["lab-first-aid-box-2"],76);
            SetIcon(definitions["lab-radio-1"],icons["lab-radio-1"],icons["lab-radio-2"],1434);
            SetIcon(definitions["lab-radio-2"],icons["lab-radio-1"],icons["lab-radio-2"],1086);
            if(definitions.Values.Select(d=>d.displayName).Distinct().Count()!=definitions.Count)throw new Exception("Duplicate display names remain");
            EditorUtility.SetDirty(en);EditorUtility.SetDirty(es);EditorUtility.SetDirty(collection.SharedData);EditorUtility.SetDirty(collection);LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);AssetDatabase.SaveAssets();File.WriteAllText("Logs/DescriptiveItemNameChanges.txt",report.ToString());
        }
        static void SetIcon(ItemDefinition d,Sprite a,Sprite b,float x)
        {
            var correct=Mathf.Abs(a.rect.x-x)<1?a:Mathf.Abs(b.rect.x-x)<1?b:null;
            if(!correct)throw new Exception("Verified atlas sprite missing for "+d.id);d.icon=correct;EditorUtility.SetDirty(d);
        }
    }
}
