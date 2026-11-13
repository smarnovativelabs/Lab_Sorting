using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class UIAnimationUpgrade
    {
        public static void Apply()
        {
            var hud=Object.FindFirstObjectByType<SortingHUD>();
            foreach(var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!button.GetComponent<UIButtonMotion>())button.gameObject.AddComponent<UIButtonMotion>();
            foreach(var panel in Object.FindObjectsByType<DesignerPanelMotion>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!panel.card)continue;
                foreach(var button in panel.card.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    if(button.name!="Close"||button.GetComponent<AnimatedPanelClose>())continue;
                    var close=button.gameObject.AddComponent<AnimatedPanelClose>();close.panel=panel;close.completed=button.onClick;button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(button.onClick,close.Close);
                }
            }
            var toasts=hud.GetComponent<DesignerToasts>();var prompt=toasts.adPrompt.GetComponent<DesignerPanelMotion>();if(!prompt)prompt=toasts.adPrompt.AddComponent<DesignerPanelMotion>();prompt.card=(RectTransform)toasts.adPrompt.transform;
            var rewards=hud.GetComponent<SupplyRewardMotion>();if(!rewards)rewards=hud.gameObject.AddComponent<SupplyRewardMotion>();rewards.hud=hud;rewards.supply=hud.GetComponent<SupplyPanel>();rewards.economy=hud.GetComponent<EconomyHUD>();
            var loading=hud.GetComponent<ServicePresentation>().loadingRoot.transform.Find("Card");
            if(!loading.Find("LoadingSpinner"))
            {
                var go=new GameObject("LoadingSpinner",typeof(RectTransform),typeof(UILoadingSpinner));go.transform.SetParent(loading,false);var rt=(RectTransform)go.transform;rt.anchoredPosition=new Vector2(235,-65);rt.sizeDelta=Vector2.one*36;
                for(int i=0;i<8;i++){var dot=new GameObject("Dot",typeof(RectTransform),typeof(UnityEngine.UI.Image));dot.transform.SetParent(rt,false);var image=dot.GetComponent<UnityEngine.UI.Image>();image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");image.color=new Color(.4f,1,.85f,(i+1)/8f);image.raycastTarget=false;image.rectTransform.sizeDelta=Vector2.one*6;float angle=i*Mathf.PI/4;image.rectTransform.anchoredPosition=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*13;}
            }
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
