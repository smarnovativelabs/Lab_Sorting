using UnityEngine;
namespace LabSorting.UI
{
    public sealed class SettingsReferenceArt:MonoBehaviour
    {
        public SettingsTutorialPanel panel;
        public UnityEngine.UI.Image image;
        public Sprite settingsSprite,tutorialSprite;
        public GameObject tutorialTitle;
        void LateUpdate()
        {
            bool tutorial=panel.ShowingTutorial;
            image.sprite=tutorial?tutorialSprite:settingsSprite;image.type=tutorial?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.preserveAspect=!tutorial;image.useSpriteMesh=!tutorial;
            tutorialTitle.SetActive(tutorial);
            panel.title.rectTransform.anchoredPosition=new Vector2(0,tutorial?220:image.rectTransform.rect.height*.427f);
        }
    }
}
