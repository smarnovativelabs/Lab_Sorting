using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;

namespace LabSorting.Editor
{
    public static class FrameBudgetUpgrade
    {
        // Run only explicitly on saved, closed prototype scenes.
        public static void Apply()
        {
            foreach(var path in new[]{"Assets/_LabSorting/Prototype/Scenes/SortingTest.unity",
                "Assets/_LabSorting/Prototype/Scenes/SortingStressTest.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path);
                foreach(var batch in Object.FindObjectsByType<ItemBatchRenderer>(FindObjectsSortMode.None))
                    batch.cellSize=16;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("FRAME_BUDGET_SCENES_UPDATED");
        }
    }
}
