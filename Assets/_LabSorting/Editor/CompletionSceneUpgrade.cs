using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class CompletionSceneUpgrade
    {
        [MenuItem("Tools/Lab Sorting/Add Lab Completion Summary")]
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);var hud=Object.FindFirstObjectByType<SortingHUD>();if(hud.GetComponent<LabCompletionPanel>()){Debug.Log("COMPLETION_ALREADY_WIRED");return;}
            var panel=hud.gameObject.AddComponent<LabCompletionPanel>();panel.session=hud.session;var safe=hud.transform.Find("SafeArea");var root=Rect("LabCompletion",safe,Vector2.zero,Vector2.zero);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;root.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.8f);panel.root=root.gameObject;
            panel.card=Rect("Card",root,Vector2.zero,new Vector2(680,460));panel.card.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.04f,.14f,.18f);panel.card.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            panel.confetti=new RectTransform[20];for(int i=0;i<20;i++){var p=Rect("Celebration "+i,panel.card,Vector2.zero,new Vector2(10,16));var image=p.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Color.HSVToRGB(i/20f,.6f,1);image.raycastTarget=false;p.gameObject.SetActive(false);panel.confetti[i]=p;}
            Label("Title",panel.card,new Vector2(0,170),new Vector2(600,55),30).text="LAB ORGANIZED!";
            panel.summary=Label("Summary",panel.card,new Vector2(0,15),new Vector2(580,230),23);
            var button=Rect("ContinueExploring",panel.card,new Vector2(0,-165),new Vector2(370,56));var background=button.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=new Color(.1f,.45f,.38f);panel.continueButton=button.gameObject.AddComponent<UnityEngine.UI.Button>();panel.continueButton.targetGraphic=background;UnityEventTools.AddPersistentListener(panel.continueButton.onClick,panel.ContinueExploring);Label("Label",button,Vector2.zero,button.sizeDelta,21).text="CONTINUE EXPLORING";
            panel.root.SetActive(false);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("LAB_COMPLETION_WIRED");
        }
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;}
        static UnityEngine.UI.Text Label(string name,Transform parent,Vector2 position,Vector2 size,int font){var r=Rect(name,parent,position,size);var t=r.gameObject.AddComponent<UnityEngine.UI.Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=font;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;return t;}
    }
}
