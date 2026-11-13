using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LabSorting.Services
{
    // Janitor connectivity probe/retry flow with Lab Sorting's mandatory online gate.
    public sealed class LabStartup : MonoBehaviour
    {
        public string gameplayScenePath="Assets/Scenes/Lab_Sorting.unity";
        public Text loadingLabel;
        public Image loadingFill;
        public bool autoLoad=true;
        public GameObject connectionRoot;
        public Text connectionMessage;
        public void RetryConnection(){LabTelemetry.Event("internet_retry","boot");if(connectivity)connectivity.CheckNow();}
        public bool Loading {get;private set;}
        public string Error {get;private set;}
        static InternetConnectivity connectivity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime(){connectivity=null;}

        IEnumerator Start()
        {
            if(!autoLoad)yield break;
            if(loadingLabel)loadingLabel.text=PlayerPrefs.GetString("Lab.Language","en")=="es"?"Comprobando conexión…":"Checking connection…";
            if(!connectivity)connectivity=FindFirstObjectByType<InternetConnectivity>();
            if(!connectivity)
            {
                var owner=new GameObject("Lab Connectivity");
                DontDestroyOnLoad(owner);
                connectivity=owner.AddComponent<InternetConnectivity>();
                yield return connectivity.InitializeRoutine();
            }
            if(!Application.CanStreamedLevelBeLoaded(gameplayScenePath))
            {
                Error="Gameplay scene is not included in Build Settings.";
                if(loadingLabel)loadingLabel.text=PlayerPrefs.GetString("Lab.Language","en")=="es"?"No se pudo cargar el laboratorio.":"Unable to load the lab.";
                Debug.LogError(Error,this);yield break;
            }
            Loading=true;
            bool waiting=false;
            while(!connectivity||!connectivity.IsReady||connectivity.Status!=ConnectivityStatus.Online)
            {
                if(!waiting){waiting=true;LabTelemetry.Event("internet_gate_shown","boot");}
                if(connectionRoot)connectionRoot.SetActive(true);
                if(connectionMessage)connectionMessage.text=connectivity&&connectivity.Status==ConnectivityStatus.Offline?"Internet is required to play.\nReconnect, then tap RETRY.":"Checking internet connection...\nPlease wait or tap RETRY.";
                yield return null;
            }
            if(connectionRoot)connectionRoot.SetActive(false);if(waiting)LabTelemetry.Event("internet_restored","boot");
            // Boot precedes the gameplay localization owner; use the saved language without delaying loading.
            if(loadingLabel)loadingLabel.text=PlayerPrefs.GetString("Lab.Language","en")=="es"?"Cargando laboratorio…":"Loading lab…";
            // Render the loading view once, then load without a Play-button interaction.
            yield return null;
            var load=SceneManager.LoadSceneAsync(gameplayScenePath,LoadSceneMode.Single);
            if(load==null)yield break;
            load.allowSceneActivation=false;
            float displayed=0;
            while(!load.isDone)
            {
                displayed=Mathf.MoveTowards(displayed,Mathf.Clamp01(load.progress/.9f),Time.unscaledDeltaTime*2f);
                SetLoadingProgress(displayed);
                if(loadingLabel)loadingLabel.text=(PlayerPrefs.GetString("Lab.Language","en")=="es"?"Cargando laboratorio… ":"Loading lab… ")+Mathf.RoundToInt(displayed*100)+"%";
                if(load.progress>=.9f){SetLoadingProgress(1);if(loadingLabel)loadingLabel.text=PlayerPrefs.GetString("Lab.Language","en")=="es"?"Cargando laboratorio… 100%":"Loading lab… 100%";yield return LabSorting.UI.LoadingSceneFade.Cover();load.allowSceneActivation=true;while(!load.isDone)yield return null;yield break;}
                yield return null;
            }
        }
        public void SetLoadingProgress(float progress)
        {
            if(!loadingFill)return;
            // Crop a fixed-size image: never squeeze its end caps at low progress.
            loadingFill.enabled=true;
            loadingFill.type=Image.Type.Filled;
            loadingFill.fillMethod=Image.FillMethod.Horizontal;
            loadingFill.fillOrigin=0;
            loadingFill.fillAmount=Mathf.Clamp01(progress);
        }
    }
}
