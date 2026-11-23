using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using LabSorting.Sorting;

namespace LabSorting.FeelPreview.Editor
{
    public static class FeelPreviewBuilder
    {
        public const string ScenePath = "Assets/_LabSorting/FeelPreview/InteractionFeelPreview.unity";
        [MenuItem("Tools/Lab Sorting/Feel Preview/Open Comparison")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before opening the preview.");
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Lab Sorting/Feel Preview/Build Comparison")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes first.");
            // Rebuilding from our own open scene must close it before saving its replacement.
            if (SceneManager.GetActiveScene().path == ScenePath)
                EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            var original = SceneManager.GetActiveScene();
            var source = EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity", OpenSceneMode.Additive);
            var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(preview);
            try
            {
                var roots = source.GetRootGameObjects();
                var sourceRack = roots.SelectMany(x => x.GetComponentsInChildren<SortRack>(true)).First(x => !x.ratsOnly && x.SizeBased);
                var rack = UnityEngine.Object.Instantiate(sourceRack);
                rack.name = "Production rack - preview copy";
                rack.transform.SetParent(null); rack.transform.SetPositionAndRotation(new Vector3(.35f, 0, .85f), Quaternion.identity);
                var runtime = new GameObject("Interaction Feel Preview").AddComponent<InteractionFeelPreview>(); runtime.rack = rack;
                runtime.placementPreviewMaterial = roots.SelectMany(x => x.GetComponentsInChildren<RackPlacementPreview>(true)).First().previewMaterial;
                var allItems = roots.SelectMany(x => x.GetComponentsInChildren<SortItem>(true)).ToArray();
                string[] names = { "hand-sanitizer-bottles-1", "battries-large", "radio-1" };
                runtime.templates = new SortItem[names.Length];
                var templates = new GameObject("Source models (inactive)");
                for (int i = 0; i < names.Length; i++)
                {
                    var item = allItems.First(x => x.definition && x.definition.name == names[i]);
                    var copy = UnityEngine.Object.Instantiate(item, templates.transform);
                    copy.name = names[i]; copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    copy.location = ItemLocation.Loose; copy.rack = null; copy.slot = -1;
                    copy.gameObject.SetActive(false); runtime.templates[i] = copy;
                }
                runtime.pickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_LabSorting/Audio/Pick.wav");
                runtime.placeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_LabSorting/Audio/Place.wav");
                runtime.dropClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_LabSorting/Audio/Throw.wav");
                var camera = new GameObject("Preview Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.transform.position = new Vector3(-.1f, 1.85f, -3.6f);
                camera.transform.LookAt(new Vector3(-.1f, .85f, .45f)); camera.fieldOfView = 48; camera.nearClipPlane = .05f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f, .10f, .15f);
                runtime.view = camera;
                var light = new GameObject("Soft key").AddComponent<Light>(); light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45, -30, 0); light.intensity = 1.4f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.65f, .70f, .76f);
                Cube("Floor", new Vector3(0, -.055f, .2f), new Vector3(8, .1f, 7), new Color(.19f, .26f, .3f));
                Cube("Pickup plinth", new Vector3(-.9f, .36f, -.38f), new Vector3(.95f, .72f, .85f), new Color(.11f, .22f, .27f));
                Cube("Back wall", new Vector3(0, 1.5f, 2.2f), new Vector3(8, 3, .1f), new Color(.10f, .16f, .21f));
                if (!EditorSceneManager.SaveScene(preview, ScenePath)) throw new InvalidOperationException("Preview scene could not be saved.");
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(preview, true);
            }
            AssetDatabase.SaveAssets();
            Open();
            Debug.Log("FEEL_PREVIEW_BUILT " + ScenePath);
        }
        static void Cube(string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = scale;
            string path = "Assets/_LabSorting/FeelPreview/" + name.Replace(" ", "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
