using UnityEngine;
namespace LabSorting.UI
{
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class UISoundFeedback:MonoBehaviour
    {
        public LabSoundscape soundscape;
        public bool closing;
        UnityEngine.UI.Button button;
        void Awake(){button=GetComponent<UnityEngine.UI.Button>();}
        void OnEnable(){if(!button)button=GetComponent<UnityEngine.UI.Button>();button.onClick.AddListener(Play);}
        void OnDisable(){if(button)button.onClick.RemoveListener(Play);}
        void Play(){if(soundscape)soundscape.Click(closing);}
    }
}
