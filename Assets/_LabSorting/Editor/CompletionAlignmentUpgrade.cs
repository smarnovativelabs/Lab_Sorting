using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class CompletionAlignmentUpgrade
    {
        static void Center(UnityEngine.UI.Text text){text.alignment=TextAnchor.MiddleCenter;}
        public static void Apply()
        {
            var h=Object.FindFirstObjectByType<SortingHUD>();var p=h.GetComponent<LabCompletionPanel>();
            p.heading=p.card.Find("Title").GetComponent<UnityEngine.UI.Text>();p.replayLabel=p.continueButton.GetComponentInChildren<UnityEngine.UI.Text>(true);p.exploreLabel=p.card.Find("Explore").GetComponentInChildren<UnityEngine.UI.Text>(true);
            p.card.sizeDelta=new Vector2(680,520);p.heading.rectTransform.anchoredPosition=new Vector2(0,210);((RectTransform)p.card.Find("DesignerTitle_Title")).anchoredPosition=new Vector2(0,210);
            p.summary.rectTransform.anchoredPosition=new Vector2(0,60);p.summary.rectTransform.sizeDelta=new Vector2(580,200);Center(p.summary);
            if(!p.replayHint){p.replayHint=Object.Instantiate(p.summary,p.card);p.replayHint.name="ReplayPreservedHint";}
            p.replayHint.rectTransform.anchoredPosition=new Vector2(0,-75);p.replayHint.rectTransform.sizeDelta=new Vector2(580,62);p.replayHint.resizeTextForBestFit=true;p.replayHint.resizeTextMinSize=16;p.replayHint.resizeTextMaxSize=20;Center(p.replayHint);
            ((RectTransform)p.continueButton.transform).anchoredPosition=new Vector2(0,-155);((RectTransform)p.continueButton.transform).sizeDelta=new Vector2(320,54);
            var explore=(RectTransform)p.card.Find("Explore");explore.anchoredPosition=new Vector2(0,-215);explore.sizeDelta=new Vector2(320,44);
            foreach(var button in h.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                var texts=button.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                if(texts.Length!=1||button.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length>1)continue;
                var text=texts[0];if(text.transform.parent!=button.transform)continue;
                Center(text);var r=text.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=Vector2.one*.5f;r.offsetMin=new Vector2(12,5);r.offsetMax=new Vector2(-12,-5);
            }
            foreach(var text in h.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                var art=text.transform.parent.Find("DesignerTitle_"+text.name) as RectTransform;if(!art||!art.gameObject.activeSelf)continue;
                Center(text);var r=text.rectTransform;r.anchorMin=art.anchorMin;r.anchorMax=art.anchorMax;r.pivot=art.pivot;r.anchoredPosition=art.anchoredPosition;r.sizeDelta=art.sizeDelta-new Vector2(48,16);
            }
            var c=LocalizationEditorSettings.GetStringTableCollection("LabUI");string[] en={"PLAY AGAIN?","LAB COMPLETE!","Start a fresh sorting run?","Items return to the floor.","Racks and run progress reset.","Cash, tools, capacity upgrades and\ncompletion history stay saved.","START NEW RUN","racks completed"};string[] es={"¿VOLVER A JUGAR?","¡LABORATORIO COMPLETO!","¿Empezar una nueva partida?","Los objetos vuelven al suelo.","Los estantes y el progreso se reinician.","Conservas dinero, herramientas, capacidad\ne historial de partidas completadas.","NUEVA PARTIDA","estantes completados"};foreach(var t in c.StringTables){for(int i=0;i<en.Length;i++)t.AddEntry(en[i],t.LocaleIdentifier.Code=="es"?es[i]:en[i]);EditorUtility.SetDirty(t);}EditorUtility.SetDirty(c.SharedData);EditorUtility.SetDirty(c);LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,c);
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene);EditorSceneManager.SaveScene(h.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
