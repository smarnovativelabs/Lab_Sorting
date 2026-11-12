using UnityEngine;
using UnityEngine.SceneManagement;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Services
{
    [DefaultExecutionOrder(-10000)]
    public sealed class LabServiceRoot : MonoBehaviour
    {
        public static LabServiceRoot Instance {get;private set;}
        public SupplyCommerce commerce;
        public LabTelemetry telemetry;
        public InternetConnectivity internet;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Instance=null;}
        void Awake()
        {
            if(Instance&&Instance!=this){gameObject.SetActive(false);Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);SceneManager.sceneLoaded+=Loaded;
            if(internet)StartCoroutine(internet.InitializeRoutine());
        }
        void Start(){BindScene();}
        void Loaded(Scene scene,LoadSceneMode mode){BindScene();}
        public void BindScene()
        {
            var session=FindFirstObjectByType<SortingSession>();commerce.BindSession(session);telemetry.BindSession(session);
            foreach(var panel in FindObjectsByType<SupplyPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None))panel.commerce=commerce;
            foreach(var panel in FindObjectsByType<ServicesPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None))panel.commerce=commerce;
        }
        void OnDestroy(){if(Instance!=this)return;SceneManager.sceneLoaded-=Loaded;Instance=null;}
    }
}
