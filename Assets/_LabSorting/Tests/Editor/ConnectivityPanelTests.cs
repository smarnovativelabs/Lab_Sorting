using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public class ConnectivityPanelTests
{
    [UnityTest, Timeout(150000)]
    public IEnumerator OfflineOnlinePanelRecovery()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");
        foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var type = behaviour.GetType();
            if (type.Name == "SortingSession") type.GetField("persistence").SetValue(behaviour, false);
            if (type.Name == "SettingsTutorialPanel") type.GetField("autoTutorial").SetValue(behaviour, false);
            if (type.Name == "FirstLaunchTour") type.GetField("autoPlay").SetValue(behaviour, false);
        }
        yield return new EnterPlayMode(false);
        Application.runInBackground = true;
        yield return null;
        var helper = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("LabSorting.Prototype.ConnectivityPanelChecks")).First(t => t != null);
        yield return (IEnumerator)helper.GetMethod("Run").Invoke(null, null);
        yield return new ExitPlayMode();
    }
}
