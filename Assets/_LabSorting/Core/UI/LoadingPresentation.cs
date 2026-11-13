using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
namespace LabSorting.UI
{
    public sealed class LoadingPresentation:MonoBehaviour
    {
        public RectTransform background,logo,shine;
        public Text tip;
        public LabSorting.Services.LabStartup startup;
        public AudioClip musicClip;
        float began;Tween entrance;
        static readonly string[] tips={"Each rack holds one item type.","Select a carried item to guide SEARCH and MAGNET.","Use + beside Collected Items to add carry slots."};
        static readonly string[] spanish={"Cada estante guarda un tipo de objeto.","Selecciona un objeto para usar SEARCH y MAGNET.","Pulsa + junto al inventario para añadir espacios."};
        void Start()
        {
            began=Time.unscaledTime;var group=logo.GetComponent<CanvasGroup>();if(!group)group=logo.gameObject.AddComponent<CanvasGroup>();group.alpha=0;logo.localScale=Vector3.one*.96f;
            entrance=DOTween.Sequence().Join(group.DOFade(1,.3f)).Join(logo.DOScale(1,.4f).SetEase(Ease.OutCubic)).SetUpdate(true);
            BootMusic.StartMusic(musicClip);
        }
        void Update()
        {
            float elapsed=Time.unscaledTime-began;
            var image=background.GetComponent<Image>();var area=((RectTransform)background.parent).rect.size;
            if(image.sprite){float ratio=image.sprite.rect.width/image.sprite.rect.height;float height=Mathf.Max(area.y,area.x/ratio);background.sizeDelta=new Vector2(height*ratio,height);background.localScale=Vector3.one*Mathf.Lerp(1,1.03f,Mathf.Clamp01(elapsed/20));}
            int index=(int)(elapsed/6)%tips.Length;tip.text=PlayerPrefs.GetString("Lab.Language","en")=="es"?spanish[index]:tips[index];
            if(shine){float t=Mathf.Repeat(elapsed*.35f,1);shine.gameObject.SetActive(startup.loadingFill.fillAmount>t);var track=(RectTransform)shine.parent;shine.anchoredPosition=new Vector2(Mathf.Lerp(track.rect.xMin+8,track.rect.xMax-8,t),0);}
        }
        void OnDestroy(){entrance?.Kill();}
    }
    public sealed class BootMusic:MonoBehaviour
    {
        static AudioSource source;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){source=null;}
        public static void StartMusic(AudioClip clip)
        {
            if(source||!clip)return;var go=new GameObject("Boot Music");DontDestroyOnLoad(go);go.AddComponent<BootMusic>();source=go.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.spatialBlend=0;source.priority=180;source.volume=0;source.Play();
        }
        public static AudioSource Take(){var result=source;source=null;if(result)Destroy(result.GetComponent<BootMusic>());return result;}
        void Update(){if(source){float target=PlayerPrefs.GetInt("LabSorting.Sound",1)!=0?.1f:0;source.volume=Mathf.MoveTowards(source.volume,target,Time.unscaledDeltaTime*.08f);}}
        void OnApplicationPause(bool paused){if(source){if(paused)source.Pause();else source.UnPause();}}
        void OnApplicationFocus(bool focused){if(source){if(focused)source.UnPause();else source.Pause();}}
    }
}
