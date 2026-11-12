using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LabSorting.Services
{
    // Lives only in the boot/MainMenu scene. Online services never gate offline play.
    public sealed class LabStartup : MonoBehaviour
    {
        public string gameplayScenePath="Assets/Scenes/Lab_Sorting.unity";
        public Text loadingLabel;
        public bool autoLoad=true;
        public bool Loading {get;private set;}
        public string Error {get;private set;}
        static InternetConnectivity connectivity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime(){connectivity=null;}

        IEnumerator Start()
        {
            if(!autoLoad)yield break;
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
            // Boot precedes the gameplay localization owner; use the saved language without delaying loading.
            if(loadingLabel)loadingLabel.text=PlayerPrefs.GetString("Lab.Language","en")=="es"?"Cargando laboratorio…":"Loading lab…";
            // Render the loading view once, then load without a Play-button interaction.
            yield return null;
            var load=SceneManager.LoadSceneAsync(gameplayScenePath,LoadSceneMode.Single);
            while(load!=null&&!load.isDone)yield return null;
        }
    }
}
