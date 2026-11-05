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
    static void AssertDimensions(GameObject player)
    {
        var capsule=player.GetComponent<CapsuleCollider>();Assert.That(capsule.height,Is.EqualTo(1.7f).Within(.0001f));Assert.That(capsule.center.y,Is.EqualTo(.85f).Within(.0001f));Assert.That(capsule.radius,Is.EqualTo(.3f).Within(.0001f));
        var camera=player.GetComponentInChildren<Camera>(true);Assert.That(player.transform.InverseTransformPoint(camera.transform.position).y,Is.EqualTo(1.6f).Within(.0001f));Assert.That(player.transform.localScale,Is.EqualTo(Vector3.one));
        var motor=player.GetComponents<MonoBehaviour>().Single(m=>m&&m.GetType().Name=="KinematicCharacterMotor");var serialized=new SerializedObject(motor);
        Assert.That(serialized.FindProperty("CapsuleHeight").floatValue,Is.EqualTo(1.7f).Within(.0001f));Assert.That(serialized.FindProperty("CapsuleYOffset").floatValue,Is.EqualTo(.85f).Within(.0001f));
    }
}



