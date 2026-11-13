using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class SortingIntegrationTests
{
    [UnityTest,Timeout(240000)] public IEnumerator DesignerMinimumTouch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.MinimumTouchChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(240000)] public IEnumerator DesignerControlSizesAndWelcome()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.ControlWelcomeChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(180000)] public IEnumerator DesignerCleanupFocus()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.CleanupFocusChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(180000)] public IEnumerator LoadingArtworkRatios()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var startup=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Services.LabStartup")).First(t=>t!=null);
        startup.GetField("autoLoad").SetValue(UnityEngine.Object.FindFirstObjectByType(startup),false);
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.LoadingAspectChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(180000)] public IEnumerator DesignerFirstLaunchTour()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        var tourType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.UI.FirstLaunchTour")).First(t=>t!=null);
        var tour=UnityEngine.Object.FindFirstObjectByType(tourType);tourType.GetField("autoPlay").SetValue(tour,true);tourType.GetField("completionKey").SetValue(tour,"LabSorting.TestTour.Done");PlayerPrefs.DeleteKey("LabSorting.TestTour.Done");
        foreach(var name in new[]{"LabSorting.UI.SettingsTutorialPanel","LabSorting.UI.GuidedTutorial"}){var t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(a=>a!=null);var instance=UnityEngine.Object.FindFirstObjectByType(t);t.GetField("preferencesPrefix").SetValue(instance,"LabSorting.TestTour.");if(name.EndsWith("SettingsTutorialPanel"))t.GetField("autoTutorial").SetValue(instance,true);}
        PlayerPrefs.DeleteKey("LabSorting.TestTour.GuidedDone");PlayerPrefs.DeleteKey("LabSorting.TestTour.GuidedStep");
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.EnvironmentTourChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(180000)] public IEnumerator RewardedAdReturnRecovery()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.RewardReturnChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(300000)] public IEnumerator DesignerServiceExperience()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.ServiceExperienceChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(300000)] public IEnumerator DesignerOffersReplay()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.OffersReplayChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(300000)] public IEnumerator DesignerArtworkUI()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.DesignerUIChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(300000)] public IEnumerator DesignerRapidResume()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.RapidResumeChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(180000)] public IEnumerator MainMenuPersistentServices()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var startupType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Services.LabStartup")).First(t=>t!=null);
        startupType.GetField("autoLoad").SetValue(UnityEngine.Object.FindFirstObjectByType(startupType),false);
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene,UnityEngine.SceneManagement.LoadSceneMode> loaded=(scene,mode)=>{if(scene.path=="Assets/Scenes/Lab_Sorting.unity")PrepareDesignerSession();};
        UnityEngine.SceneManagement.SceneManager.sceneLoaded+=loaded;
        try {yield return new EnterPlayMode(false);Application.runInBackground=true;var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.BootstrapChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();}
        finally {UnityEngine.SceneManagement.SceneManager.sceneLoaded-=loaded;}
    }
    [UnityTest,Timeout(300000)] public IEnumerator DesignerGameplayPolish()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.GameplayPolishChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(300000)] public IEnumerator DesignerSpanishAndRackFeedback()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.SpanishRackChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(300000)] public IEnumerator DesignerServices()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.ServiceChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(300000)] public IEnumerator DesignerLandingFeel()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.LandingFeelChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(300000)] public IEnumerator DesignerFullFlight()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.FullFlightChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(300000)]
    public IEnumerator DesignerDevicePerformance()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.DevicePerformanceChecks")).First(t=>t!=null);yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator DesignerFrontOnlyDrop()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.FrontDropChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [Test] public void ReviewedRackAndTossSafety()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Editor.RackReviewChecks")).First(t=>t!=null);
        type.GetMethod("Run").Invoke(null,null);
    }
    [Test] public void ItemShadowsDisabledInScenesAndPrefabs()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Editor.ItemShadowUpgrade")).First(t=>t!=null);
        type.GetMethod("Validate").Invoke(null,null);
    }
    bool previousEnabled;
    StackTraceLogType previousLogStack;
    EnterPlayModeOptions previousOptions;
    [SetUp] public void Setup() { previousEnabled=EditorSettings.enterPlayModeOptionsEnabled;previousOptions=EditorSettings.enterPlayModeOptions;previousLogStack=Application.GetStackTraceLogType(LogType.Log);Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None); }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(EditorApplication.isPlaying) yield return new ExitPlayMode();
        EditorSettings.enterPlayModeOptionsEnabled=previousEnabled;EditorSettings.enterPlayModeOptions=previousOptions;
        Application.SetStackTraceLogType(LogType.Log,previousLogStack);
    }
    [Test] public void PlayerPrefabHasNoPrototypeDependencies()
    {
        var dependencies=AssetDatabase.GetDependencies("Assets/_LabSorting/Prefabs/Player/LabPlayer.prefab",true);
        Assert.IsFalse(dependencies.Any(p=>p.Contains("/Prototype/")),"Permanent player must not depend on disposable content");
    }
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator CoreSortingAndController()
    {
        EditorSceneManager.OpenScene("Assets/_LabSorting/Prototype/Scenes/SortingTest.unity");
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Sorting.SortingSession")).First(t=>t!=null);
        var session=UnityEngine.Object.FindFirstObjectByType(type);
        type.GetField("persistence").SetValue(session,false);
        var completionType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.UI.LabCompletionPanel")).First(t=>t!=null);
        var completion=UnityEngine.Object.FindFirstObjectByType(completionType);if(completion)completionType.GetField("autoShow").SetValue(completion,false);
        var recoveryType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Sorting.ItemRecovery")).First(t=>t!=null);
        var recovery=UnityEngine.Object.FindFirstObjectByType(recoveryType);if(recovery)recoveryType.GetField("automatic").SetValue(recovery,false);
        var settingsType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.UI.SettingsTutorialPanel")).First(t=>t!=null);
        var settings=UnityEngine.Object.FindFirstObjectByType(settingsType);
        if(settings){settingsType.GetField("autoTutorial").SetValue(settings,false);settingsType.GetField("preferencesPrefix").SetValue(settings,"LabSorting.AutomatedTutorial.");}
        foreach(var key in new[]{"LookSensitivity","Sound","Haptics","TutorialDone","TutorialPage"})PlayerPrefs.DeleteKey("LabSorting.AutomatedTutorial."+key);
        EditorSettings.enterPlayModeOptionsEnabled=true;
        EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);
        var checkType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.SortingSmokeChecks")).First(t=>t!=null);
        Assert.IsNotNull(checkType);
        yield return (IEnumerator)checkType.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [Test]
    public void PlayerHeightMatchesPrefabAndSavedScene()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_LabSorting/Prefabs/Player/LabPlayer.prefab");
        AssertDimensions(prefab);
        EditorSceneManager.OpenScene("Assets/_LabSorting/Prototype/Scenes/SortingTest.unity");
        var capsule=UnityEngine.Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None).Single(c=>c.GetComponents<MonoBehaviour>().Any(m=>m&&m.GetType().Name=="KinematicCharacterMotor"));
        AssertDimensions(capsule.gameObject);
    }
    [UnityTest,Timeout(900000)]
    public IEnumerator StressCatalogPerformance()
    {
        EditorSceneManager.OpenScene("Assets/_LabSorting/Prototype/Scenes/SortingStressTest.unity");
        var catalogType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.StressCatalog")).First(t=>t!=null);
        var catalog=UnityEngine.Object.FindFirstObjectByType(catalogType);catalogType.GetField("autoStart").SetValue(catalog,false);
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);UnityEngine.Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.StressPerformanceChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(1500000)]
    public IEnumerator LongSessionSaveAndMemory()
    {
        EditorSceneManager.OpenScene("Assets/_LabSorting/Prototype/Scenes/SortingStressTest.unity");
        var catalogType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.StressCatalog")).First(t=>t!=null);
        catalogType.GetField("autoStart").SetValue(UnityEngine.Object.FindFirstObjectByType(catalogType),false);
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.SortingSoakChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [Test] public void IntegrationValidatorDetectsBadContent()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Editor.IntegrationValidationChecks")).First(t=>t!=null);
        type.GetMethod("Run").Invoke(null,null);
    }
    static void PrepareDesignerSession()
    {
        var tourType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.UI.FirstLaunchTour")).FirstOrDefault(t=>t!=null);
        if(tourType!=null){var tour=UnityEngine.Object.FindFirstObjectByType(tourType);if(tour)tourType.GetField("autoPlay").SetValue(tour,false);}
        foreach(var pair in new[]{new[]{"LabSorting.UI.ServicePresentation","automaticConnectivity"},new[]{"LabSorting.Sorting.SortingSession","persistence"},new[]{"LabSorting.UI.SettingsTutorialPanel","autoTutorial"},new[]{"LabSorting.UI.LabCompletionPanel","autoShow"},new[]{"LabSorting.Sorting.ItemRecovery","automatic"}})
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(pair[0])).First(t=>t!=null);
            var instance=UnityEngine.Object.FindFirstObjectByType(type);if(instance)type.GetField(pair[1]).SetValue(instance,false);
        }
    }
    [UnityTest,Timeout(900000)]
    public IEnumerator DesignerAssetsAndGameplay()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.DesignerGameplayChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(600000)]
    public IEnumerator DesignerMaterialFeedback()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.MaterialFeedbackChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(600000)]
    public IEnumerator DesignerRackHints()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.RackHintChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(600000)]
    public IEnumerator DesignerBulkAndIcons()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.BulkIconChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(600000)]
    public IEnumerator DesignerCompactControls()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.CompactControlsChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(600000)]
    public IEnumerator DesignerReferenceCorrections()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.ReferenceCorrectionChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(600000)]
    public IEnumerator DesignerSoftDropAndRackTargeting()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.SoftInteractionChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(600000)]
    public IEnumerator DesignerGuidedTutorial()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");PrepareDesignerSession();
        var guidedType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.UI.GuidedTutorial")).First(t=>t!=null);
        var guided=UnityEngine.Object.FindFirstObjectByType(guidedType);Assert.IsNotNull(guided,"Lab scene has the guided tutorial");
        guidedType.GetField("preferencesPrefix").SetValue(guided,"LabSorting.AutomatedGuided.");
        foreach(var key in new[]{"GuidedStep","GuidedDone"})PlayerPrefs.DeleteKey("LabSorting.AutomatedGuided."+key);
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);Application.runInBackground=true;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.GuidedTutorialChecks")).First(t=>t!=null);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(null,null);
        yield return new ExitPlayMode();
    }
    [UnityTest,Timeout(180000)]
    public IEnumerator MainMenuAutomaticallyLoadsDesignerScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene,UnityEngine.SceneManagement.LoadSceneMode> loaded=(scene,mode)=>PrepareDesignerSession();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded+=loaded;
        try
        {
            yield return new EnterPlayMode(false);Application.runInBackground=true;
              // Deterministic connectivity fixture: test transition without an external endpoint.
              yield return null;
              var networkType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Services.InternetConnectivity")).First(t=>t!=null);
              var network=(MonoBehaviour)UnityEngine.Object.FindFirstObjectByType(networkType);Assert.IsNotNull(network);network.enabled=false;
              networkType.GetProperty("IsReady").SetValue(network,true);networkType.GetMethod("SetStatus",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(network,new[]{Enum.Parse(networkType.GetProperty("Status").PropertyType,"Online")});
              var bootMusic=GameObject.Find("Boot Music").GetComponent<AudioSource>();int musicId=bootMusic.GetInstanceID();
              double until=UnityEditor.EditorApplication.timeSinceStartup+60;
            while(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Lab_Sorting.unity"&&UnityEditor.EditorApplication.timeSinceStartup<until)yield return null;
            Assert.AreEqual("Assets/Scenes/Lab_Sorting.unity",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            Assert.AreEqual(1,UnityEngine.SceneManagement.SceneManager.sceneCount,"Boot scene unloaded after automatic transition");
            Assert.IsNotNull(Camera.main,"Designer player camera is available after boot");
              yield return new WaitForSecondsRealtime(.5f);
              Assert.IsNull(GameObject.Find("Loading Scene Fade"),"Transition overlay releases controls");
              Assert.IsTrue(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.GetInstanceID()==musicId&&a.loop),"Original music source continues into gameplay");
              Assert.AreEqual(1,UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a=>a.isActiveAndEnabled),"One active listener after boot");
            yield return new ExitPlayMode();
        }
        finally {UnityEngine.SceneManagement.SceneManager.sceneLoaded-=loaded;}
    }
      [UnityTest,Timeout(180000)] public IEnumerator CompleteFirstLaunchFlow()
      {
          EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
          var helper=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Prototype.FirstLaunchFlowChecks")).First(t=>t!=null);
          UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene,UnityEngine.SceneManagement.LoadSceneMode> loaded=(scene,mode)=>{if(scene.path=="Assets/Scenes/Lab_Sorting.unity"){PrepareDesignerSession();helper.GetMethod("Configure").Invoke(null,null);}};
          UnityEngine.SceneManagement.SceneManager.sceneLoaded+=loaded;
          try
          {
              yield return new EnterPlayMode(false);Application.runInBackground=true;yield return null;
              var networkType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LabSorting.Services.InternetConnectivity")).First(t=>t!=null);
              var network=(MonoBehaviour)UnityEngine.Object.FindFirstObjectByType(networkType);network.enabled=false;networkType.GetProperty("IsReady").SetValue(network,true);networkType.GetMethod("SetStatus",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(network,new[]{Enum.Parse(networkType.GetProperty("Status").PropertyType,"Online")});
              float deadline=Time.realtimeSinceStartup+45;while(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Lab_Sorting.unity"&&Time.realtimeSinceStartup<deadline)yield return null;
              Assert.AreEqual("Assets/Scenes/Lab_Sorting.unity",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
              yield return (IEnumerator)helper.GetMethod("Run").Invoke(null,null);yield return new ExitPlayMode();
          }
          finally{UnityEngine.SceneManagement.SceneManager.sceneLoaded-=loaded;helper.GetMethod("Cleanup").Invoke(null,null);}
      }
      static void AssertDimensions(GameObject player)
    {
        var capsule=player.GetComponent<CapsuleCollider>();Assert.That(capsule.height,Is.EqualTo(1.7f).Within(.0001f));Assert.That(capsule.center.y,Is.EqualTo(.85f).Within(.0001f));Assert.That(capsule.radius,Is.EqualTo(.3f).Within(.0001f));
        var camera=player.GetComponentInChildren<Camera>(true);Assert.That(player.transform.InverseTransformPoint(camera.transform.position).y,Is.EqualTo(1.6f).Within(.0001f));Assert.That(player.transform.localScale,Is.EqualTo(Vector3.one));
        var motor=player.GetComponents<MonoBehaviour>().Single(m=>m&&m.GetType().Name=="KinematicCharacterMotor");var serialized=new SerializedObject(motor);
        Assert.That(serialized.FindProperty("CapsuleHeight").floatValue,Is.EqualTo(1.7f).Within(.0001f));Assert.That(serialized.FindProperty("CapsuleYOffset").floatValue,Is.EqualTo(.85f).Within(.0001f));
    }
}



