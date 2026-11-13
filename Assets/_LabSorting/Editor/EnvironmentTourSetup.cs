using UnityEngine;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        public static void AddEnvironmentTour()
        {
            Load();var hud=Object.FindFirstObjectByType<SortingHUD>();var tour=Ensure<FirstLaunchTour>(hud);
            tour.session=hud.session;tour.settings=hud.GetComponent<SettingsTutorialPanel>();tour.gameplayUI=Ensure<CanvasGroup>(hud);
            tour.brain=Ensure<CinemachineBrain>(hud.session.player.view);tour.brain.UpdateMethod=CinemachineBrain.UpdateMethods.ManualUpdate;tour.brain.IgnoreTimeScale=true;tour.brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut,0);tour.brain.enabled=false;
            var rig=GameObject.Find("EnvironmentTour");if(!rig)rig=new GameObject("EnvironmentTour");
            var cameraRoot=rig.transform.Find("TourCamera");if(!cameraRoot){cameraRoot=new GameObject("TourCamera").transform;cameraRoot.SetParent(rig.transform);}
            tour.tourCamera=Ensure<CinemachineCamera>(cameraRoot);tour.tourCamera.Lens.NearClipPlane=hud.session.player.view.nearClipPlane;tour.tourCamera.Lens.FarClipPlane=hud.session.player.view.farClipPlane;cameraRoot.gameObject.SetActive(false);
            var positions=new[]{new Vector3(0,2.6f,-5.6f),new Vector3(4.5f,2.7f,-3.5f),new Vector3(3,2.8f,-.5f),new Vector3(0,2,-6)};
            var targets=new[]{new Vector3(0,2,5),new Vector3(-4,1.7f,3),new Vector3(5,1.8f,7),new Vector3(0,1.2f,3)};
            tour.shots=new Transform[positions.Length];
            for(int i=0;i<positions.Length;i++){var shot=rig.transform.Find("Shot"+i);if(!shot){shot=new GameObject("Shot"+i).transform;shot.SetParent(rig.transform);}shot.SetPositionAndRotation(positions[i],Quaternion.LookRotation(targets[i]-positions[i]));tour.shots[i]=shot;}
            var overlay=Rect("TourOverlay",rig.transform,Vector2.zero);var canvas=Ensure<Canvas>(overlay);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            Ensure<UnityEngine.UI.GraphicRaycaster>(overlay);var scaler=Ensure<UnityEngine.UI.CanvasScaler>(overlay);scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=hud.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution;scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            tour.overlay=overlay.gameObject;
            var curtain=Rect("Curtain",overlay,Vector2.zero);curtain.anchorMin=Vector2.zero;curtain.anchorMax=Vector2.one;curtain.offsetMin=curtain.offsetMax=Vector2.zero;var image=Ensure<UnityEngine.UI.Image>(curtain);image.color=Color.black;image.raycastTarget=true;tour.curtain=Ensure<CanvasGroup>(curtain);
            var safe=Rect("SafeArea",overlay,Vector2.zero);safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.offsetMin=safe.offsetMax=Vector2.zero;Ensure<SafeAreaPanel>(safe);
            var title=Label("Title",safe,"LAB SORTING",Vector2.zero,new Vector2(340,50),28);Pos(title.rectTransform,new Vector2(.5f,1),new Vector2(0,-60),new Vector2(340,50));title.font=headingFont;BackTitle(title);
            var skip=Button("Skip",safe,"SKIP",Vector2.zero,new Vector2(150,58),tour.Skip,"gold");Pos((RectTransform)skip.transform,new Vector2(1,0),new Vector2(-110,65),new Vector2(150,58));
            tour.overlay.SetActive(false);UnityEditor.EditorUtility.SetDirty(tour);EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);if(!EditorSceneManager.SaveScene(hud.gameObject.scene))throw new System.Exception("Tour scene save failed");
        }
    }
}
