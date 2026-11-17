using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        const float ToastHeight=36,CardWidth=300,CardHeight=52;
        // Important messages move to a center toast above the crosshair; the Selected card takes the
        // freed bottom-center slot as a compact icon + name strip (totals live in Collected Items).
        [MenuItem("Tools/Lab Sorting/Designer/Center Toast + Compact Selected Card")]
        public static void ApplyCenterToast()
        {
            Load();var hud=Object.FindFirstObjectByType<SortingHUD>();var feedback=hud?hud.GetComponent<DesignerToasts>():null;
            if(!hud||!feedback||!feedback.toast)throw new System.Exception("Open the gameplay scene with the designer HUD first");
            var safe=(RectTransform)feedback.toast.transform.parent;
            RectTransform Find(string name){var t=safe.Find(name);if(!t)throw new System.Exception("Missing HUD element: "+name);return (RectTransform)t;}

            var toast=(RectTransform)feedback.toast.transform;
            Pos(toast,new Vector2(.5f,.5f),new Vector2(0,72),new Vector2(520,ToastHeight));toast.localScale=Vector3.one;
            var art=toast.GetComponent<UnityEngine.UI.Image>();art.pixelsPerUnitMultiplier=art.sprite.rect.height/ToastHeight;
            Pos(hud.status.rectTransform,Vector2.one*.5f,Vector2.zero,new Vector2(492,28));hud.status.resizeTextMaxSize=18;
            feedback.keepBelow=new[]{Find("MockupTaskCard"),Find("MatchingRackDirection"),Find("DesignerAdPrompt")};feedback.floor=Find("Crosshair");
            feedback.toast.alpha=0;feedback.toast.blocksRaycasts=false;

            var card=Find("SelectedItemCard");
            Pos(card,new Vector2(.5f,0),new Vector2(0,34),new Vector2(CardWidth,CardHeight));card.localScale=Vector3.one;
            if(hud.activeIcon)Pos(hud.activeIcon.rectTransform,new Vector2(0,.5f),new Vector2(40,0),Vector2.one*34);
            var rewards=hud.GetComponent<SortingRewardsHUD>();
            // The color swatch sits behind the icon, as before.
            if(rewards&&rewards.selectedSwatch)Pos(rewards.selectedSwatch.rectTransform,new Vector2(0,.5f),new Vector2(40,0),new Vector2(18,34));
            var label=hud.active.rectTransform;label.anchorMin=new Vector2(0,.5f);label.anchorMax=new Vector2(1,.5f);label.pivot=Vector2.one*.5f;
            label.offsetMin=new Vector2(66,-20);label.offsetMax=new Vector2(-20,20);label.localScale=Vector3.one;
            hud.active.alignment=TextAnchor.MiddleCenter;hud.active.resizeTextForBestFit=true;hud.active.resizeTextMinSize=14;hud.active.resizeTextMaxSize=20;
            hud.selectedCard=Ensure<CanvasGroup>(card);hud.selectedCard.interactable=false;hud.selectedCard.blocksRaycasts=false;

            ReportLayout(safe);
            foreach(var o in new Object[]{hud,feedback,toast,art,hud.status,card,hud.active,hud.selectedCard})EditorUtility.SetDirty(o);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);
        }
        // Batch entry: Unity -batchmode -executeMethod LabSorting.Editor.DesignerUIUpgrade.ApplyCenterToastBatch -quit
        public static void ApplyCenterToastBatch(){EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");ApplyCenterToast();}

        // Edit-time overlap report at the narrowest/shortest canvas LabResponsiveLayout produces
        // (1280x720), a wide phone and a 4:3 tablet. Toast is measured at its authored spot; the
        // runtime clamp only ever lowers it toward the crosshair.
        static void ReportLayout(RectTransform safe)
        {
            UnityEngine.Rect At(string name,Vector2 parent)
            {
                var r=(RectTransform)safe.Find(name);
                Vector2 min=Vector2.Scale(r.anchorMin,parent)+r.anchoredPosition-Vector2.Scale(r.sizeDelta,r.pivot);
                Vector2 max=Vector2.Scale(r.anchorMax,parent)+r.anchoredPosition+Vector2.Scale(r.sizeDelta,Vector2.one-r.pivot);
                return UnityEngine.Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            }
            var pairs=new Dictionary<string,string[]>{
                {"DesignerToast",new[]{"Crosshair","Target","MatchingRackDirection","DesignerAdPrompt","MilestoneReward","DesignerTitle_Progress","DesignerCollected"}},
                {"SelectedItemCard",new[]{"MovementJoystick","PICK","PLACE","THROW","ServiceToast","InventoryPrevious","InventoryNext","InventoryPage","Target","DesignerToast"}}};
            foreach(var size in new[]{new Vector2(1280,720),new Vector2(1600,720),new Vector2(1280,960)})
                foreach(var pair in pairs)
                {
                    var a=At(pair.Key,size);
                    foreach(var other in pair.Value)
                    {
                        if(!safe.Find(other))continue;var b=At(other,size);
                        if(a.Overlaps(b))throw new System.Exception("HUD overlap at "+size+": "+pair.Key+" "+a+" vs "+other+" "+b);
                    }
                    Debug.Log("CENTER TOAST LAYOUT PASS: "+pair.Key+" "+a+" clear at "+size);
                }
        }
    }
}
