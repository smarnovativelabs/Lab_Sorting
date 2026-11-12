using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using LabSorting.Services;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class LabServicesSetup
    {
        public const string SettingsPath="Assets/_LabSorting/Settings/LabServiceSettings.asset";
        [MenuItem("Tools/Lab Sorting/Services/Configure and wire services")]
        public static void Apply()
        {
            var config=AssetDatabase.LoadAssetAtPath<LabServiceSettings>(SettingsPath);
            config.removeAdsProductId="lab_sorting_remove_ads";config.smallCashProductId="lab_sorting_cash_500";config.largeCashProductId="lab_sorting_cash_1500";
            config.magnetPackProductId="lab_sorting_magnets_10";config.searchPackProductId="lab_sorting_searches_25";config.slotPackProductId="lab_sorting_slots_5";
            config.smallCashAmount=500;config.largeCashAmount=1500;config.magnetPackAmount=10;config.searchPackAmount=25;config.slotPackAmount=5;
            config.rewardedCashAmount=25;config.rewardedSearchAmount=2;config.rewardedMagnetAmount=1;config.rewardedSlotAmount=1;EditorUtility.SetDirty(config);
            CreateLocalization();
            foreach(var path in new[]{"Assets/Scenes/Lab_Sorting.unity","Assets/_LabSorting/Prototype/Scenes/SortingTest.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path);var session=UnityEngine.Object.FindFirstObjectByType<SortingSession>();var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();
                var commerce=session.GetComponent<SupplyCommerce>()??session.gameObject.AddComponent<SupplyCommerce>();commerce.session=session;commerce.settings=config;
                var telemetry=session.GetComponent<LabTelemetry>()??session.gameObject.AddComponent<LabTelemetry>();telemetry.settings=config;telemetry.session=session;
                if(!session.GetComponent<LabLocalization>())session.gameObject.AddComponent<LabLocalization>();
                var settings=hud.GetComponent<SettingsTutorialPanel>();var services=hud.GetComponent<ServicesPanel>()??hud.gameObject.AddComponent<ServicesPanel>();services.commerce=commerce;services.settingsPanel=settings;
                if(!services.root)CreatePanel(services,hud.transform.Find("SafeArea"),settings);
                foreach(var label in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true))if(!label.GetComponent<LabLocalizedLabel>())label.gameObject.AddComponent<LabLocalizedLabel>();
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            ApplyAccountSettings();AssetDatabase.SaveAssets();GameplayPolishUpgrade.Apply();
        }
        [MenuItem("Tools/Lab Sorting/Services/Apply account IDs")]
        public static void ApplyAccountSettings()
        {
            var config=AssetDatabase.LoadAssetAtPath<LabServiceSettings>(SettingsPath);
            if(!string.IsNullOrWhiteSpace(config.androidPackageName))PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,config.androidPackageName);
            AppLovinSettings.Instance.AdMobAndroidAppId=config.googleMobileAdsAppId??"";EditorUtility.SetDirty(AppLovinSettings.Instance);
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings")).FirstOrDefault(t=>t!=null);
            if(type!=null){var asset=(UnityEngine.Object)type.GetMethod("LoadInstance",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);var so=new SerializedObject(asset);so.FindProperty("adMobAndroidAppId").stringValue=config.googleMobileAdsAppId??"";so.ApplyModifiedPropertiesWithoutUndo();}
            AssetDatabase.SaveAssets();
        }
        static void CreatePanel(ServicesPanel panel,Transform safe,SettingsTutorialPanel settings)
        {
            var help=settings.settingsContent.transform.Find("HowToPlay") as RectTransform;if(help)help.anchoredPosition=new Vector2(-150,-158);
            Button(settings.settingsContent.transform,"Services",new Vector2(150,-158),"STORE & PRIVACY",panel.Open);
            var root=Rect(safe,"ServicesModal",Vector2.zero,new Vector2(700,560));root.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.035f,.09f,.13f,1);panel.root=root.gameObject;
            Label(root,"Title",new Vector2(0,240),"STORE & PRIVACY",25);
            var remove=Button(root,"RemoveAds",new Vector2(-155,175),"REMOVE ADS",panel.RemoveAds);panel.removeButton=remove;panel.removeLabel=remove.GetComponentInChildren<UnityEngine.UI.Text>();
            Button(root,"Restore",new Vector2(155,175),"RESTORE PURCHASES",panel.Restore);
            panel.languageLabel=Button(root,"Language",new Vector2(-155,110),"Language",panel.Language).GetComponentInChildren<UnityEngine.UI.Text>();
            Button(root,"Retry",new Vector2(155,110),"RETRY CONNECTION",panel.Retry);
            panel.analyticsLabel=Button(root,"Analytics",new Vector2(-155,45),"Analytics",panel.Analytics).GetComponentInChildren<UnityEngine.UI.Text>();
            panel.diagnosticsLabel=Button(root,"Diagnostics",new Vector2(155,45),"Crash reports",panel.Diagnostics).GetComponentInChildren<UnityEngine.UI.Text>();
            panel.attributionLabel=Button(root,"Attribution",new Vector2(-155,-20),"Attribution",panel.Attribution).GetComponentInChildren<UnityEngine.UI.Text>();
            Button(root,"AdPrivacy",new Vector2(155,-20),"AD PRIVACY OPTIONS",panel.Privacy);
            Button(root,"Policy",new Vector2(-155,-85),"PRIVACY POLICY",panel.Policy);Button(root,"Close",new Vector2(155,-85),"BACK",panel.Close);
            panel.status=Label(root,"Status",new Vector2(0,-155),"",17);panel.status.rectTransform.sizeDelta=new Vector2(620,70);
            var note=Label(root,"Note",new Vector2(0,-225),"Remove Ads disables automatic ads. Reward videos remain optional.",16);note.rectTransform.sizeDelta=new Vector2(620,50);root.gameObject.SetActive(false);
        }
        static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;}
        static UnityEngine.UI.Text Label(Transform parent,string name,Vector2 position,string text,int size){var r=Rect(parent,name,position,new Vector2(280,48));var t=r.gameObject.AddComponent<UnityEngine.UI.Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.text=text;t.color=Color.white;t.raycastTarget=false;return t;}
        static UnityEngine.UI.Button Button(Transform parent,string name,Vector2 position,string text,UnityAction action){var r=Rect(parent,name,position,new Vector2(280,50));var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.08f,.26f,.31f);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;UnityEventTools.AddPersistentListener(b.onClick,action);var label=Label(r,"Label",Vector2.zero,text,17);label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=17;return b;}
        static void CreateLocalization()
        {
            const string folder="Assets/_LabSorting/Localization";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            if(!LocalizationEditorSettings.ActiveLocalizationSettings){var settings=ScriptableObject.CreateInstance<LocalizationSettings>();AssetDatabase.CreateAsset(settings,folder+"/LocalizationSettings.asset");LocalizationEditorSettings.ActiveLocalizationSettings=settings;}
            foreach(var code in new[]{"en","es"})if(!LocalizationEditorSettings.GetLocales().Any(l=>l.Identifier.Code==code)){var locale=Locale.CreateLocale(code);AssetDatabase.CreateAsset(locale,folder+"/"+code+".asset");LocalizationEditorSettings.AddLocale(locale);}
            var collection=LocalizationEditorSettings.GetStringTableCollection("LabUI")??LocalizationEditorSettings.CreateStringTableCollection("LabUI",folder);
            var en=collection.StringTables.First(t=>t.LocaleIdentifier.Code=="en");var es=collection.StringTables.First(t=>t.LocaleIdentifier.Code=="es");
            foreach(var file in Directory.GetFiles("Assets/_LabSorting/Core","*.cs",SearchOption.AllDirectories))foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file),"\"([^\"\\r\\n]{2,200})\"")){var value=match.Groups[1].Value;if(value.Contains("\\")||!value.Any(char.IsLetter)||value.Contains("/")||value.Contains("."))continue;if(en.GetEntry(value)==null)en.AddEntry(value,value);}
            var translations=new[]{"PICK|RECOGER","PLACE|COLOCAR","THROW|SOLTAR","SEARCH|BUSCAR","MAGNET|IMÁN","SETTINGS|AJUSTES","NEXT|SIGUIENTE","BACK|ATRÁS","SKIP|OMITIR","HOW TO PLAY|CÓMO JUGAR","REPLAY TUTORIAL|REPETIR TUTORIAL","RESET SETTINGS|RESTABLECER AJUSTES","STORE & PRIVACY|TIENDA Y PRIVACIDAD","REMOVE ADS|QUITAR ANUNCIOS","ADS REMOVED|ANUNCIOS ELIMINADOS","RESTORE PURCHASES|RESTAURAR COMPRAS","RETRY CONNECTION|REINTENTAR CONEXIÓN","AD PRIVACY OPTIONS|PRIVACIDAD DE ANUNCIOS","PRIVACY POLICY|POLÍTICA DE PRIVACIDAD","Unavailable|No disponible","CARRY CAPACITY|CAPACIDAD","MAX CAPACITY|CAPACIDAD MÁXIMA","Analytics: ON|Estadísticas: SÍ","Analytics: OFF|Estadísticas: NO","Attribution: ON|Atribución: SÍ","Attribution: OFF|Atribución: NO","Crash reports: ON|Informes de errores: SÍ","Crash reports: OFF|Informes de errores: NO","Highlight the selected item type anywhere in the lab.|Resalta el tipo seleccionado en todo el laboratorio.","Bring matching loose items into your hands.|Trae a tus manos los objetos del mismo tipo.","Add one permanent carry slot.|Añade un espacio permanente para llevar objetos.","Use cash for search, magnet and extra carry slots.|Usa dinero para buscar, atraer objetos y ampliar la capacidad."};
            foreach(var pair in translations){var parts=pair.Split('|');en.AddEntry(parts[0],parts[0]);es.AddEntry(parts[0],parts[1]);}
            EditorUtility.SetDirty(en);EditorUtility.SetDirty(es);EditorUtility.SetDirty(collection.SharedData);EditorUtility.SetDirty(collection);LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);AssetDatabase.SaveAssets();
        }
    }
}
