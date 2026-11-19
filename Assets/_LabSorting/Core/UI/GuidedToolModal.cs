using UnityEngine;
using UnityEngine.UI;
namespace LabSorting.UI
{
    public sealed partial class GuidedTutorial
    {
        Image toolBlocker, toolHighlight, toolTail;
        Vector3 bubbleRest;
        readonly Vector3[] toolCorners=new Vector3[4];
        CursorLockMode toolCursor; bool toolCursorVisible, toolModal;
        public bool ToolModalActive=>toolModal;
        void SetToolModal(bool value)
        {
            if(value==toolModal)return;
            toolModal=value;
            if(session) {session.CancelHold();session.player.SetTutorialBlocked(value);}
            if(value)
            {
                bubbleRest=bubble.localPosition;
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
                if(toolTail)toolTail.gameObject.SetActive(false);
                if(bubble)bubble.localPosition=bubbleRest;
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
            LayoutToolBubble(target);
        }
        void LayoutToolBubble(RectTransform target)
        {
            // Work in the bubble parent's space so the callout follows safe-area/resolution changes.
            var parent=(RectTransform)bubble.parent;
            target.GetWorldCorners(toolCorners);
            Vector2 min=parent.InverseTransformPoint(toolCorners[0]),max=parent.InverseTransformPoint(toolCorners[2]);
            var bounds=parent.rect;float width=bubble.rect.width,height=bubble.rect.height;
            bool left=(min.x+max.x)*.5f>bounds.center.x;
            float x=left?min.x-22-width*.5f:max.x+22+width*.5f;
            float y=(min.y+max.y)*.5f;
            x=Mathf.Clamp(x,bounds.xMin+width*.5f+12,bounds.xMax-width*.5f-12);
            y=Mathf.Clamp(y,bounds.yMin+height*.5f+50,bounds.yMax-height*.5f-12);
            bubble.localPosition=new Vector3(x+(bubble.pivot.x-.5f)*width,y+(bubble.pivot.y-.5f)*height,0);
            if(!toolTail)
            {
                toolTail=new GameObject("ToolCalloutTail",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                toolTail.transform.SetParent(bubble,false);toolTail.raycastTarget=false;
                toolTail.color=new Color(1,.85f,.15f);toolTail.rectTransform.sizeDelta=Vector2.one*14;
                toolTail.rectTransform.localRotation=Quaternion.Euler(0,0,45);
            }
            toolTail.gameObject.SetActive(true);
            var tip=bubble.InverseTransformPoint(target.position);
            toolTail.rectTransform.localPosition=new Vector3(left?bubble.rect.xMax:bubble.rect.xMin,Mathf.Clamp(tip.y,bubble.rect.yMin+18,bubble.rect.yMax-18),0);
        }
    }
}
