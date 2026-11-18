using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    // Navy plate behind a small HUD caption (SEARCH, MAGNET, PAUSE...). White text alone disappears over the
    // bright lab floor; the plate follows the caption's width as it changes ("SEARCH" <-> "3.2s").
    [RequireComponent(typeof(RectTransform))]
    public sealed class CaptionPlate:MonoBehaviour
    {
        public Text label;
        public float padding=18,minWidth=44,height=22;
        string shown;Image image;
        void Awake(){image=GetComponent<Image>();}
        void LateUpdate()
        {
            if(!label)return;
            bool visible=label.isActiveAndEnabled&&!string.IsNullOrEmpty(label.text);
            if(image&&image.enabled!=visible)image.enabled=visible;
            if(!visible||label.text==shown)return;
            shown=label.text;
            var rect=(RectTransform)transform;var target=label.rectTransform;
            rect.anchorMin=target.anchorMin;rect.anchorMax=target.anchorMax;rect.pivot=target.pivot;rect.anchoredPosition=target.anchoredPosition;
            float width=Mathf.Clamp(TextFit.Width(label,shown,TextFit.MaxSize(label))+padding,minWidth,Mathf.Max(minWidth,target.rect.width+padding));
            rect.sizeDelta=new Vector2(width,height);
        }
    }
}
