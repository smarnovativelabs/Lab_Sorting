using UnityEngine;
using LabSorting.Services;
namespace LabSorting.UI
{
    [DefaultExecutionOrder(-9000)]
    public sealed class ServicePresentation:MonoBehaviour
    {
        public SortingHUD hud;
        public GameObject loadingRoot,offlineRoot;
        public UnityEngine.UI.Text loadingText,toastText;
        public CanvasGroup toast;
        public UnityEngine.UI.Button dismissLoading;
        public bool automaticConnectivity=true;
        SupplyCommerce commerce;InternetConnectivity internet;
        bool visible,dismissedWait;
        CursorLockMode cursor;bool cursorVisible;float toastUntil;
        readonly ToastPriority messages=new ToastPriority();
        public bool ToastBusy=>Time.unscaledTime<toastUntil;
        void Awake(){if(automaticConnectivity&&hud)hud.session.player.SetNetworkBlocked(true);}
        void Update()
        {
            var owner=LabServiceRoot.Instance?LabServiceRoot.Instance.commerce:FindFirstObjectByType<SupplyCommerce>();
            if(owner!=commerce){if(commerce)commerce.StatusChanged-=Status;commerce=owner;if(commerce)commerce.StatusChanged+=Status;}
            if(!internet)internet=FindFirstObjectByType<InternetConnectivity>();
            bool waiting=commerce&&commerce.PurchaseBusy;
            if(!waiting)dismissedWait=false;
            loadingRoot.SetActive(waiting&&!dismissedWait);
            if(loadingRoot.activeSelf){loadingRoot.transform.SetAsLastSibling();dismissLoading.gameObject.SetActive(commerce.CanClosePurchaseUI);loadingText.text=commerce.CanClosePurchaseUI?"Purchase is still pending. You can continue playing; delivery will finish when the store responds.":"Contacting store...\nPlease wait";}
            messages.Tick(Time.unscaledTime);toastUntil=messages.Until;toastText.text=messages.Text;
            toast.alpha=Mathf.MoveTowards(toast.alpha,Time.unscaledTime<toastUntil?1:0,Time.unscaledDeltaTime*7);if(toast.alpha>0)toast.transform.SetAsLastSibling();
            bool offline=automaticConnectivity&&internet&&internet.IsReady&&(internet.ConnectionProblem||(visible&&internet.Status!=ConnectivityStatus.Online));
            if(offline){if(!visible)OpenOffline();offlineRoot.transform.SetAsLastSibling();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            else if(visible){LabTelemetry.Event("internet_restored");CloseOffline();}
            else if(hud.session.player.NetworkBlocked&&(!automaticConnectivity||(internet&&internet.Status==ConnectivityStatus.Online)))hud.session.player.SetNetworkBlocked(false);
        }
        void Status(string text){if(string.IsNullOrWhiteSpace(text)||text=="Opening store..."||text=="Preparing video..."||text=="Watching reward video...")return;ShowToast(text);}
        public void ShowToast(string text){if(text!=null&&text.StartsWith("Purchase",System.StringComparison.OrdinalIgnoreCase))messages.Supersede("Purchase",Time.unscaledTime);messages.Push(text,ToastPriority.Classify(text),Time.unscaledTime,4);toastText.text=messages.Text;toastUntil=messages.Until;}
        public void DismissLoading(){if(commerce&&commerce.CanClosePurchaseUI){dismissedWait=true;loadingRoot.SetActive(false);ShowToast("Purchase pending. Your reward will be delivered after confirmation.");}}
        public void OpenOffline(){if(visible)return;visible=true;var p=hud.session.player;cursor=Cursor.lockState;cursorVisible=Cursor.visible;p.SetNetworkBlocked(true);hud.session.SuspendForConnection();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;offlineRoot.SetActive(true);offlineRoot.transform.SetAsLastSibling();LabTelemetry.Event("internet_gate_shown",internet?internet.Status.ToString():"Unknown");}
        public void Retry(){LabTelemetry.Event("internet_retry");if(internet)internet.CheckNow();ShowToast("Checking connection...");}
        void CloseOffline(){if(!visible)return;visible=false;offlineRoot.SetActive(false);hud.session.player.SetNetworkBlocked(false);Cursor.lockState=cursor;Cursor.visible=cursorVisible;}
        void OnDisable(){if(commerce)commerce.StatusChanged-=Status;commerce=null;CloseOffline();if(hud&&hud.session.player.NetworkBlocked)hud.session.player.SetNetworkBlocked(false);if(loadingRoot)loadingRoot.SetActive(false);}
    }
}
