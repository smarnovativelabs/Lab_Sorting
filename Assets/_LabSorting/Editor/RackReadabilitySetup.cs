using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        public static void ApplyRackReadability()
        {
            Load();var hud=Object.FindFirstObjectByType<SortingHUD>();
            var guide=Ensure<MatchingRackGuide>(hud);guide.session=hud.session;guide.tutorial=hud.GetComponent<GuidedTutorial>();
            guide.label=Label("MatchingRackDirection",hud.transform.Find("SafeArea"),"",new Vector2(0,-235),new Vector2(540,52),20);
            Pos(guide.label.rectTransform,new Vector2(.5f,1),new Vector2(0,-235),new Vector2(540,52));
            guide.label.horizontalOverflow=HorizontalWrapMode.Wrap;guide.label.resizeTextMinSize=18;
            Ensure<LabLocalizedLabel>(guide.label);var outline=Ensure<UnityEngine.UI.Outline>(guide.label);outline.effectColor=new Color(0,0,0,.9f);outline.effectDistance=new Vector2(1,-1);
            hud.inventoryCounts=new UnityEngine.UI.Text[hud.inventoryButtons.Length];
            for(int i=0;i<hud.inventoryButtons.Length;i++)
            {
                var row=hud.inventoryButtons[i];var layout=Ensure<UnityEngine.UI.LayoutElement>(row);layout.preferredHeight=72;layout.minHeight=72;
                var text=hud.inventoryLabels[i];Pos(text.rectTransform,new Vector2(0,.5f),new Vector2(122,0),new Vector2(142,64));
                text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.resizeTextForBestFit=true;text.resizeTextMinSize=16;text.resizeTextMaxSize=17;Ensure<LabLocalizedLabel>(text);
                var count=Label("ItemCount",row.transform,"",Vector2.zero,new Vector2(32,28),17);Pos(count.rectTransform,new Vector2(1,.5f),new Vector2(-19,0),new Vector2(32,28));count.color=new Color(.04f,.07f,.13f);hud.inventoryCounts[i]=count;
            }
            var progress=hud.GetComponent<ItemsProgressPanel>();foreach(var text in progress.rows){text.horizontalOverflow=HorizontalWrapMode.Wrap;text.resizeTextForBestFit=true;text.resizeTextMinSize=18;text.resizeTextMaxSize=22;Ensure<LabLocalizedLabel>(text);}
            var collection=LocalizationEditorSettings.GetStringTableCollection("LabUI");var en=collection.StringTables.First(t=>t.LocaleIdentifier.Code=="en");var es=collection.StringTables.First(t=>t.LocaleIdentifier.Code=="es");
            string[] pairs={"MATCHING RACK|ESTANTE ASIGNADO","EMPTY RACK|ESTANTE VACÍO","TURN LEFT|GIRA A LA IZQUIERDA","TURN RIGHT|GIRA A LA DERECHA","FIND STAIRS UP|SUBE LAS ESCALERAS","FIND STAIRS DOWN|BAJA LAS ESCALERAS","FOLLOW THE GLOW|SIGUE EL BRILLO","TO FIND|POR ENCONTRAR","LAST|ÚLTIMOS","ALL ITEMS|TODOS LOS OBJETOS","Left to sort:|Por ordenar:","of|de","SHORT GOAL|META CORTA","new items to next reward|objetos para la próxima recompensa"};
            foreach(var pair in pairs){var p=pair.Split('|');en.AddEntry(p[0],p[0]);es.AddEntry(p[0],p[1]);}
            EditorUtility.SetDirty(en);EditorUtility.SetDirty(es);EditorUtility.SetDirty(collection.SharedData);EditorUtility.SetDirty(collection);LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);
        }
    }
}
