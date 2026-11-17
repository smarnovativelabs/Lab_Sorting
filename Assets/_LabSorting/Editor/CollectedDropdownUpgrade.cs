using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        // Folds Collected Items into a header bar; the list drops down only when the header is tapped.
        [MenuItem("Tools/Lab Sorting/Designer/Collected Items Dropdown")]
        public static void ApplyCollectedDropdown()
        {
            Load();var hud=Object.FindFirstObjectByType<SortingHUD>();
            if(!hud||!hud.inventoryPanel||!hud.inventorySummary)throw new System.Exception("Open the gameplay scene with the designer HUD first");
            var list=(RectTransform)hud.inventoryPanel.transform;const float barHeight=60;
            // Clips the list while it rolls up/down.
            Ensure<UnityEngine.UI.RectMask2D>(list);
            // Folded look: same gold/blue frame as the Selected Item card (the panel art has a baked header divider and can't shrink).
            var bar=Rect("CollapsedArt",list,Vector2.zero);bar.anchorMin=Vector2.zero;bar.anchorMax=Vector2.one;bar.offsetMin=bar.offsetMax=Vector2.zero;
            var barImage=Skin(bar,"frame",true);barImage.pixelsPerUnitMultiplier=Mathf.Max(1,sprites["frame"].border.x/22f);bar.SetAsFirstSibling();
            // Whole header row is the tap target, except the + (capacity) button on the right.
            var hit=Rect("CollapseToggle",list,Vector2.zero);hit.anchorMin=new Vector2(0,1);hit.anchorMax=Vector2.one;hit.pivot=new Vector2(.5f,1);hit.offsetMin=new Vector2(0,-barHeight);hit.offsetMax=new Vector2(-54,0);
            var hitImage=Ensure<UnityEngine.UI.Image>(hit);hitImage.sprite=null;hitImage.color=Color.clear;hitImage.raycastTarget=true;hit.SetSiblingIndex(1);
            var toggle=Ensure<UnityEngine.UI.Button>(hit);
            var chevron=Rect("Chevron",list,Vector2.one*22);Pos(chevron,new Vector2(0,1),new Vector2(26,-barHeight*.5f),Vector2.one*22);var chevronImage=Skin(chevron,"right");
            toggle.transition=UnityEngine.UI.Selectable.Transition.ColorTint;toggle.targetGraphic=chevronImage;toggle.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var colors=toggle.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,1,.87f);colors.pressedColor=new Color(1,.85f,.3f);colors.selectedColor=Color.white;colors.disabledColor=new Color(.6f,.65f,.75f,.8f);toggle.colors=colors;
            // Make room for the chevron on the left of the title.
            Pos(hud.inventorySummary.rectTransform,new Vector2(.5f,1),new Vector2(-11,-barHeight*.5f),new Vector2(184,28));
            var plus=list.Find("CapacityPlus");if(plus)((RectTransform)plus).anchoredPosition=new Vector2(((RectTransform)plus).anchoredPosition.x,-barHeight*.5f);
            var dropdown=Ensure<CollectedDropdown>(list);dropdown.hud=hud;dropdown.panel=list;dropdown.toggle=toggle;dropdown.chevron=chevron;dropdown.collapsedArt=barImage;dropdown.collapsedHeight=barHeight;
            EditorUtility.SetDirty(dropdown);EditorSceneManager.MarkSceneDirty(list.gameObject.scene);EditorSceneManager.SaveScene(list.gameObject.scene);
        }
    }
}
