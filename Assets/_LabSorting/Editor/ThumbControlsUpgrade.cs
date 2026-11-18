using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
using LabSorting.Player;
namespace LabSorting.Editor
{
    // Thumb-friendly controls (tester request): a floating joystick zone for the left thumb, camera drag on the
    // rest of the screen, a tighter PICK / PLACE / DROP arc in the right corner and a smaller task card.
    // Search and Magnet hang from the screen middle so short (20:9) screens no longer push them onto DROP.
    // Supersedes the button/card positions written by RightControlsUpgrade, CompactControlsUpgrade and UIMarginUpgrade.
    public static class ThumbControlsUpgrade
    {
        public const float ZoneWidth=.36f,ZoneHeight=.78f,TaskCardScale=.8f,JoystickIdleAlpha=.55f;
        // ~15% bigger buttons (tester request). At the 1.15x control size every pair keeps a 10+ unit finger gap
        // and PLACE stays right of the inventory pager (InventoryNext / InventoryPage at 1280 wide).
        public static readonly Vector2 PickPosition=new Vector2(-86,86),PlacePosition=new Vector2(-192,66),DropPosition=new Vector2(-162,184);
        public const float PickSize=88,PlaceSize=80,DropSize=72;
        public static readonly Vector2 SearchPosition=new Vector2(-72,12),MagnetPosition=new Vector2(-72,-93);
        // Top-right corner of the task card stays where the 440-wide card had it.
        const float TaskCardRight=-18,TaskCardTop=-94.5f;

        static void ActionButton(RectTransform button,Vector2 position,float size)
        {
            button.anchorMin=button.anchorMax=new Vector2(1,0);button.pivot=new Vector2(.5f,.5f);button.anchoredPosition=position;button.sizeDelta=Vector2.one*size;
            foreach(RectTransform child in button)
            {
                if(child.name=="DesignerIcon")child.sizeDelta=Vector2.one*size*.6f;
                else if(child.name=="ControlSymbol")child.sizeDelta=Vector2.one*size;
                else if(child.GetComponent<UnityEngine.UI.Text>()){child.anchoredPosition=new Vector2(0,-size*.5f-9);child.sizeDelta=new Vector2(size+24,child.sizeDelta.y);}
                EditorUtility.SetDirty(child);
            }
            EditorUtility.SetDirty(button);
        }
        static void Hang(Component c,Vector2 anchor,Vector2 position){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=position;EditorUtility.SetDirty(r);}

        [MenuItem("Tools/Lab Sorting/Designer/Thumb Controls (Floating Joystick, Compact Actions)")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Stop play mode first");
            var hud=Object.FindFirstObjectByType<SortingHUD>();if(!hud)throw new System.Exception("Open Assets/Scenes/Lab_Sorting.unity first");
            var touches=hud.GetComponentsInChildren<LabTouchArea>(true);var stick=touches.Single(t=>t.movement);var look=touches.Single(t=>!t.movement);
            var safe=(RectTransform)stick.transform.parent;

            // Camera drag everywhere the joystick zone and the buttons are not (they sit above it).
            var lookRect=(RectTransform)look.transform;lookRect.anchorMin=Vector2.zero;lookRect.anchorMax=Vector2.one;lookRect.pivot=new Vector2(.5f,.5f);lookRect.offsetMin=lookRect.offsetMax=Vector2.zero;
            lookRect.SetAsFirstSibling();EditorUtility.SetDirty(lookRect);

            // Floating joystick zone: behind the stick, above the look area.
            var zoneRect=safe.Find("MovementZone") as RectTransform;
            if(!zoneRect){zoneRect=new GameObject("MovementZone",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();zoneRect.SetParent(safe,false);Undo.RegisterCreatedObjectUndo(zoneRect.gameObject,"Thumb controls");}
            zoneRect.anchorMin=Vector2.zero;zoneRect.anchorMax=new Vector2(ZoneWidth,ZoneHeight);zoneRect.pivot=new Vector2(.5f,.5f);zoneRect.offsetMin=zoneRect.offsetMax=Vector2.zero;
            var zoneImage=zoneRect.GetComponent<UnityEngine.UI.Image>();zoneImage.color=Color.clear;zoneImage.raycastTarget=true;
            var zone=zoneRect.GetComponent<LabTouchZone>();if(!zone)zone=zoneRect.gameObject.AddComponent<LabTouchZone>();zone.joystick=stick;
            // Directly below the stick. Re-running must not flip the order: moving an earlier sibling shifts the stick down one.
            int stickIndex=stick.transform.GetSiblingIndex();
            zoneRect.SetSiblingIndex(zoneRect.GetSiblingIndex()<stickIndex?stickIndex-1:stickIndex);
            stick.floatingZone=zoneRect;stick.idleAlpha=JoystickIdleAlpha;
            var group=stick.GetComponent<CanvasGroup>();if(!group)group=stick.gameObject.AddComponent<CanvasGroup>();group.alpha=1;group.interactable=true;group.blocksRaycasts=true;
            EditorUtility.SetDirty(zoneRect);EditorUtility.SetDirty(zoneImage);EditorUtility.SetDirty(zone);EditorUtility.SetDirty(stick);EditorUtility.SetDirty(group);

            // PICK in the corner, PLACE to its left, DROP above between them.
            var actions=hud.GetComponent<ContextActionHUD>();
            ActionButton((RectTransform)actions.pick.transform,PickPosition,PickSize);
            ActionButton((RectTransform)actions.place.transform,PlacePosition,PlaceSize);
            ActionButton((RectTransform)actions.drop.transform,DropPosition,DropSize);
            var economy=hud.GetComponent<EconomyHUD>();
            Hang(economy.searchButton,new Vector2(1,.5f),SearchPosition);
            Hang(economy.magnetButton,new Vector2(1,.5f),MagnetPosition);

            // Smaller task card: scale the whole card so the doctor artwork and text keep their composition.
            var layout=hud.GetComponent<TaskCardLayout>();var card=layout.card;
            card.localScale=new Vector3(TaskCardScale,TaskCardScale,1);
            var scaled=card.rect.size*TaskCardScale;
            layout.position=new Vector2(TaskCardRight-scaled.x*.5f,TaskCardTop-scaled.y*.5f);card.anchoredPosition=layout.position;
            EditorUtility.SetDirty(card);EditorUtility.SetDirty(layout);

            Debug.Log("THUMB CONTROLS: zone="+zoneRect.anchorMax+" pick/place/drop="+PickSize+"/"+PlaceSize+"/"+DropSize+" card="+scaled+" at "+layout.position);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);
        }
        public static void ApplyBatch(){EditorSceneManager.OpenScene("Assets/Scenes/Lab_Sorting.unity");Apply();}
    }
}
