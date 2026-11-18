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
            // Tutorial heading sits inside its top plate (it used to float just under it).
            float plate=tutorialTitle?((RectTransform)tutorialTitle.transform).anchoredPosition.y:220;
            panel.title.rectTransform.anchoredPosition=new Vector2(0,tutorial?plate:image.rectTransform.rect.height*.427f);
        }
    }
}
