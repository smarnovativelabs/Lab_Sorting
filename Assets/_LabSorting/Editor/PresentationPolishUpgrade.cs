using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
using LabSorting.Player;
namespace LabSorting.Editor
{
    public static class PresentationPolishUpgrade
    {
        static void Glyph(Transform parent,ControlGlyph.Symbol symbol,Color color)
        {
            var child=parent.Find("RoundSurface");
            if(!child)child=new GameObject("RoundSurface",typeof(RectTransform),typeof(CanvasRenderer),typeof(ControlGlyph)).transform;
            child.SetParent(parent,false);child.SetAsFirstSibling();var r=(RectTransform)child;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            var g=child.GetComponent<ControlGlyph>();g.symbol=symbol;g.color=color;g.raycastTarget=false;g.SetAllDirty();
        }
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit Play Mode");
            var h=Object.FindFirstObjectByType<SortingHUD>();if(!h)throw new System.InvalidOperationException("Open designer gameplay scene");
            h.target.rectTransform.anchoredPosition=new Vector2(0,-112);h.target.fontSize=17;
            h.targetBackdrop.rectTransform.anchoredPosition=new Vector2(0,-112);h.targetBackdrop.rectTransform.sizeDelta=new Vector2(340,30);
            foreach(var touch in h.GetComponentsInChildren<LabTouchArea>(true))if(touch.movement)
            {
                var background=touch.GetComponent<UnityEngine.UI.Image>();if(background)background.color=Color.clear;
                Glyph(touch.transform,ControlGlyph.Symbol.Joystick,new Color(.65f,.85f,.9f,.9f));
                if(touch.knob){touch.knob.sizeDelta=Vector2.one*56;var image=touch.knob.GetComponent<UnityEngine.UI.Image>();if(image)image.color=Color.clear;Glyph(touch.knob,ControlGlyph.Symbol.Thumb,new Color(.4f,1,.9f,1));}
            }
            var economy=h.GetComponent<EconomyHUD>();economy.marker.fontSize=18;economy.marker.rectTransform.sizeDelta=new Vector2(480,30);
            foreach(var glyph in h.GetComponentsInChildren<ControlGlyph>(true))if(glyph.symbol!=ControlGlyph.Symbol.Joystick&&glyph.symbol!=ControlGlyph.Symbol.Thumb)
            {var label=glyph.transform.parent.GetComponentInChildren<UnityEngine.UI.Text>(true);if(label){label.fontSize=14;var shadow=label.GetComponent<UnityEngine.UI.Shadow>();if(!shadow)shadow=label.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectDistance=new Vector2(1,-1);shadow.effectColor=new Color(0,0,0,.85f);}}
            var presentation=h.session.hand.GetComponent<CarryPresentation>();if(!presentation)presentation=h.session.hand.gameObject.AddComponent<CarryPresentation>();presentation.view=h.session.player.view;
            h.GetComponent<SortingRewardsHUD>().showMagnetChips=false;
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene);EditorSceneManager.SaveScene(h.gameObject.scene);
        }
    }
}
