using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static partial class DesignerUIUpgrade
    {
        public static void ApplyServicePresentation()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Stop Play Mode first");
            Load();var hud=Object.FindFirstObjectByType<SortingHUD>();var safe=hud.transform.Find("SafeArea");var ui=Ensure<ServicePresentation>(hud);ui.hud=hud;
            ui.loadingRoot=Modal("PurchaseLoading",safe,new Vector2(610,300),out var card);BackTitle(Label("Title",card,"PURCHASE",new Vector2(0,100),new Vector2(330,42),28));ui.loadingText=Label("Message",card,"Contacting store...",new Vector2(0,10),new Vector2(530,95),23);ui.dismissLoading=Button("Continue",card,"CONTINUE",new Vector2(0,-93),new Vector2(260,45),ui.DismissLoading);ui.loadingRoot.SetActive(false);
            var motion=Ensure<DesignerPanelMotion>(ui.loadingRoot);motion.card=card;
            var overlay=GameObject.Find("ServiceOverlays");if(!overlay)overlay=new GameObject("ServiceOverlays",typeof(RectTransform));var canvas=Ensure<Canvas>(overlay);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;Ensure<UnityEngine.UI.GraphicRaycaster>(overlay);var scaler=Ensure<UnityEngine.UI.CanvasScaler>(overlay);scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=hud.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution;scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;var overlaySafe=Rect("SafeArea",overlay.transform,Vector2.zero);overlaySafe.anchorMin=Vector2.zero;overlaySafe.anchorMax=Vector2.one;overlaySafe.offsetMin=overlaySafe.offsetMax=Vector2.zero;Ensure<SafeAreaPanel>(overlaySafe);
            if(ui.offlineRoot)ui.offlineRoot.transform.SetParent(overlaySafe,false);
            ui.offlineRoot=Modal("NoConnection",overlaySafe,new Vector2(650,330),out card);BackTitle(Label("Title",card,"NO CONNECTION",new Vector2(0,113),new Vector2(420,44),27));Label("Message",card,"Internet is required to play.\nYour progress is saved. Reconnect to continue.",new Vector2(0,18),new Vector2(560,90),22);Button("Retry",card,"RETRY",new Vector2(0,-98),new Vector2(260,50),ui.Retry,"gold");var offline=card.Find("Offline");if(offline)offline.gameObject.SetActive(false);Ensure<DesignerPanelMotion>(ui.offlineRoot).card=card;ui.offlineRoot.SetActive(false);
            var toast=Rect("ServiceToast",safe,new Vector2(700,65));Pos(toast,new Vector2(.5f,0),new Vector2(0,105),new Vector2(700,65));Skin(toast,"blue",true);ui.toast=Ensure<CanvasGroup>(toast);ui.toast.alpha=0;ui.toast.blocksRaycasts=false;ui.toastText=Label("Text",toast,"",Vector2.zero,new Vector2(650,52),22);
            var turn=Ensure<TutorialRackTurn>(hud);turn.tutorial=hud.GetComponent<GuidedTutorial>();
            var review=Ensure<LabReviewPrompt>(hud);review.hud=hud;review.root=Modal("ReviewPrompt",safe,new Vector2(650,350),out card);BackTitle(Label("Title",card,"RATE LAB SORTING",new Vector2(0,125),new Vector2(420,42),27));Label("Message",card,"Share your experience on the store.\nYour feedback helps us improve Lab Sorting.",new Vector2(0,25),new Vector2(560,90),22);Button("Review",card,"OPEN STORE",new Vector2(0,-55),new Vector2(300,50),review.OpenStore,"gold");Button("Dismiss",card,"NO THANKS",new Vector2(0,-120),new Vector2(300,42),review.Dismiss);Ensure<DesignerPanelMotion>(review.root).card=card;review.root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);
        }
        public static void ApplyOnlineBoot()
        {
            Load();var boot=Object.FindFirstObjectByType<LabSorting.Services.LabStartup>();var canvas=boot.loadingLabel.GetComponentInParent<Canvas>();boot.connectionRoot=Modal("BootConnection",canvas.transform,new Vector2(650,330),out var card);BackTitle(Label("Title",card,"INTERNET REQUIRED",new Vector2(0,112),new Vector2(440,44),27));boot.connectionMessage=Label("Message",card,"Checking internet connection...",new Vector2(0,15),new Vector2(560,85),23);Button("Retry",card,"RETRY",new Vector2(0,-98),new Vector2(260,50),boot.RetryConnection,"gold");boot.connectionRoot.SetActive(false);EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);EditorSceneManager.SaveScene(boot.gameObject.scene);
        }
    }
}
