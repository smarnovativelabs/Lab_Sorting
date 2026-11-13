using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    public sealed partial class GuidedTutorial
    {
        Image toolBlocker, toolHighlight;
        CursorLockMode toolCursor; bool toolCursorVisible, toolModal;
        public bool ToolModalActive=>toolModal;
        void SetToolModal(bool value)
        {
            if(value==toolModal)return;
            toolModal=value;
            if(session) {session.CancelHold();session.player.SetTutorialBlocked(value);}
            if(value)
            {
                toolCursor=Cursor.lockState;toolCursorVisible=Cursor.visible;
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true; if(gotItButton){gotItButton.navigation=new Navigation{mode=Navigation.Mode.None};if(UnityEngine.EventSystems.EventSystem.current)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(gotItButton.gameObject);}
                if(!toolBlocker)
                {
                    toolBlocker=new GameObject("ToolExplanationBlocker",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                    toolBlocker.transform.SetParent(group.transform,false);
                    var r=toolBlocker.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
                    toolBlocker.color=new Color(0,0,0,.28f);toolBlocker.raycastTarget=true;
                    toolHighlight=new GameObject("ToolButtonHighlight",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                    toolHighlight.transform.SetParent(group.transform,false);toolHighlight.raycastTarget=false;
                    toolHighlight.color=Color.clear;
                    for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int axis=0;axis<2;axis++)
                    {
                        var bar=new GameObject("Corner",typeof(RectTransform),typeof(Image)).GetComponent<Image>();bar.transform.SetParent(toolHighlight.transform,false);
                        bar.color=new Color(1,.85f,.15f);bar.raycastTarget=false;var rect=bar.rectTransform;
                        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(x,y);rect.sizeDelta=axis==0?new Vector2(18,3):new Vector2(3,18);
                    }
                }
                toolBlocker.gameObject.SetActive(true);toolHighlight.gameObject.SetActive(true);
                group.transform.SetAsLastSibling();toolBlocker.transform.SetAsFirstSibling();bubble.SetAsLastSibling();
                if(hand)hand.gameObject.SetActive(false);handAlpha=0;Mode=HandMode.Hidden;
            }
            else
            {
                if(toolBlocker)toolBlocker.gameObject.SetActive(false);
                if(toolHighlight)toolHighlight.gameObject.SetActive(false);
                Cursor.lockState=toolCursor;Cursor.visible=toolCursorVisible;
            }
        }
        void UpdateToolHighlight()
        {
            if(!toolHighlight)return;
            var target=ToolPage==0?searchButton:ToolPage==1?magnetButton:capacityButton;
            if(!target)return;
            var r=toolHighlight.rectTransform;r.position=target.position;
            r.sizeDelta=new Vector2(target.rect.width*target.lossyScale.x/r.parent.lossyScale.x,target.rect.height*target.lossyScale.y/r.parent.lossyScale.y);
            r.localScale=Vector3.one*(1.04f+.025f*Mathf.Sin(Time.unscaledTime*4));
        }
    }
}
