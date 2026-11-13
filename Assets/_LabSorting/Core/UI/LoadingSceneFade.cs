using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace LabSorting.UI
{
    public sealed class LoadingSceneFade:MonoBehaviour
    {
        CanvasGroup group;
        public static IEnumerator Cover()
        {
            var go=new GameObject("Loading Scene Fade",typeof(Canvas),typeof(CanvasGroup),typeof(GraphicRaycaster),typeof(LoadingSceneFade));DontDestroyOnLoad(go);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2000;
            var image=new GameObject("Black",typeof(RectTransform),typeof(Image));image.transform.SetParent(go.transform,false);var rect=(RectTransform)image.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;image.GetComponent<Image>().color=Color.black;
            var fade=go.GetComponent<LoadingSceneFade>();fade.group=go.GetComponent<CanvasGroup>();fade.group.alpha=0;
            SceneManager.sceneLoaded+=fade.Loaded;
            for(float t=0;t<.18f;t+=Time.unscaledDeltaTime){fade.group.alpha=t/.18f;yield return null;}fade.group.alpha=1;
        }
        void Loaded(Scene scene,LoadSceneMode mode){SceneManager.sceneLoaded-=Loaded;StartCoroutine(Reveal());}
        IEnumerator Reveal(){yield return null;yield return null;for(float t=0;t<.22f;t+=Time.unscaledDeltaTime){group.alpha=1-t/.22f;yield return null;}Destroy(gameObject);}
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;}
    }
}
