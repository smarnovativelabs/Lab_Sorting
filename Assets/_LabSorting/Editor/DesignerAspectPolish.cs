using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
using LabSorting.Services;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        public static void PolishCurrentAspect()
        {
            Load();
            foreach(var image in Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!image.sprite||!image.sprite.name.StartsWith("LabUI_"))continue;
                if(image.type==UnityEngine.UI.Image.Type.Simple)image.preserveAspect=true;
                if(image.type==UnityEngine.UI.Image.Type.Sliced)
                {
                    // Preserve corners; only the flat centre is allowed to resize.
                    image.pixelsPerUnitMultiplier=image.sprite.name=="LabUI_frame"
                        ?Mathf.Max(.01f,image.sprite.border.x/24f)
                        :Mathf.Max(.01f,image.sprite.rect.height/Mathf.Max(1,image.rectTransform.rect.height));
                }
            }
            foreach(var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                Ensure<UIButtonMotion>(button);
            var rewards=Object.FindFirstObjectByType<SortingRewardsHUD>();
            if(rewards)
            {
                // Leave the right-hand tool column clear on short, wide screens.
                Pos(rewards.milestone.rectTransform,new Vector2(1,1),new Vector2(-360,-290),new Vector2(430,44));
                BackTitle(rewards.milestone);
                Pos((RectTransform)rewards.shortGoalFill.parent,new Vector2(1,1),new Vector2(-360,-321),new Vector2(360,10));
            }
            var startup=Object.FindFirstObjectByType<LabStartup>();
            if(startup)
            {
                var canvas=startup.loadingLabel.GetComponentInParent<Canvas>();
                var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
                scaler.referenceResolution=new Vector2(1280,720);
                var root=canvas.transform;
                var logo=(RectTransform)root.Find("DesignerLoadingLogo");
                Pos(logo,Vector2.one*.5f,new Vector2(0,65),new Vector2(340*sprites["logo"].rect.width/sprites["logo"].rect.height,340));
                Pos(startup.loadingLabel.rectTransform,Vector2.one*.5f,new Vector2(0,-145),new Vector2(600,40));
                var track=(RectTransform)root.Find("DesignerLoadingTrack");
                float width=440,height=width*sprites["track"].rect.height/sprites["track"].rect.width;
                Pos(track,Vector2.one*.5f,new Vector2(0,-190),new Vector2(width,height));
                Skin(track,"track");
                var fill=startup.loadingFill.rectTransform;
                fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;
                fill.offsetMin=new Vector2(4,3);fill.offsetMax=new Vector2(-4,-3);
                startup.SetLoadingProgress(0);
                startup.loadingFill.preserveAspect=false;
            }
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
