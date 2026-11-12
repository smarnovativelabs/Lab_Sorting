using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class ReferenceHudPolish
    {
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Exit Play Mode");
            var hud=Object.FindFirstObjectByType<SortingHUD>();var safe=hud.transform.Find("SafeArea");
            for(int i=0;i<hud.inventoryButtons.Length;i++)
            {
                var r=(RectTransform)hud.inventoryButtons[i].transform;r.sizeDelta=new Vector2(124,50);r.anchoredPosition=new Vector2((i%5-2)*130,55+i/5*56);
                hud.inventoryIcons[i].rectTransform.sizeDelta=new Vector2(40,42);
            }
            hud.active.rectTransform.anchoredPosition=new Vector2(30,174);hud.active.rectTransform.sizeDelta=new Vector2(310,58);hud.activeIcon.rectTransform.anchoredPosition=new Vector2(-160,174);
            var card=(RectTransform)safe.Find("SelectedItemCard");card.anchoredPosition=new Vector2(0,174);card.sizeDelta=new Vector2(400,60);
            var rewards=hud.GetComponent<SortingRewardsHUD>();rewards.ability.rectTransform.anchoredPosition=new Vector2(0,230);
            var existing=safe.Find("FocusItemBackdrop");if(!existing){existing=new GameObject("FocusItemBackdrop",typeof(RectTransform),typeof(UnityEngine.UI.Image)).transform;existing.SetParent(safe,false);}
            var bg=existing.GetComponent<UnityEngine.UI.Image>();bg.color=new Color(.025f,.08f,.11f,.85f);bg.raycastTarget=false;bg.rectTransform.anchorMin=bg.rectTransform.anchorMax=new Vector2(.5f,.5f);bg.rectTransform.anchoredPosition=new Vector2(0,-46);bg.rectTransform.sizeDelta=new Vector2(390,32);existing.SetSiblingIndex(hud.target.transform.GetSiblingIndex());hud.targetBackdrop=bg;
            hud.target.rectTransform.anchorMin=hud.target.rectTransform.anchorMax=new Vector2(.5f,.5f);hud.target.rectTransform.anchoredPosition=new Vector2(0,-46);hud.target.rectTransform.sizeDelta=new Vector2(380,30);hud.target.fontSize=18;
            var economy=hud.GetComponent<EconomyHUD>();economy.searchLabel.color=new Color(1,.84f,.4f);economy.magnetLabel.color=new Color(.5f,1,1);
            foreach(var text in new[]{hud.progress,hud.status,economy.wallet}){var shadow=text.GetComponent<UnityEngine.UI.Shadow>();if(!shadow)shadow=text.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(0,0,0,.85f);shadow.effectDistance=new Vector2(1,-1);}
            EditorUtility.SetDirty(hud);EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);EditorSceneManager.SaveScene(hud.gameObject.scene);
        }
    }
}
