using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using LabSorting.Sorting;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class EconomySceneUpgrade
    {
        [MenuItem("Tools/Lab Sorting/Add Economy Controls")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            var session=Object.FindFirstObjectByType<SortingSession>();var hud=Object.FindFirstObjectByType<SortingHUD>();
            if(!session||!hud) throw new System.InvalidOperationException("Expected sorting scene and HUD");
            if(hud.GetComponent<EconomyHUD>()) {Debug.Log("Economy UI already installed");return;}
            var safe=(RectTransform)hud.transform.Find("SafeArea");var economy=hud.gameObject.AddComponent<EconomyHUD>();economy.session=session;economy.safeArea=safe;
            economy.wallet=Label("Wallet",safe,new Vector2(1,1),new Vector2(-160,-70),new Vector2(300,35));
            economy.searchButton=MakeButton("Search",safe,-120,session.UseSearch,out economy.searchLabel);
            economy.magnetButton=MakeButton("Magnet",safe,-182,session.UseMagnet,out economy.magnetLabel);
            economy.upgradeButton=MakeButton("CapacityUpgrade",safe,-244,session.UpgradeCapacity,out economy.upgradeLabel);
            economy.marker=Label("SearchLocator",safe,new Vector2(.5f,.5f),Vector2.zero,new Vector2(280,36));economy.marker.color=new Color(1,.9f,.2f);economy.marker.gameObject.SetActive(false);
            // Extra category slots remain available after capacity upgrades.
            var buttons=new Button[15];var labels=new Text[15];
            for(int i=0;i<15;i++)
            {
                if(i<hud.inventoryButtons.Length){buttons[i]=hud.inventoryButtons[i];labels[i]=hud.inventoryLabels[i];continue;}
                var obj=Object.Instantiate(hud.inventoryButtons[0].gameObject,safe);obj.name="Slot"+i;
                obj.GetComponent<RectTransform>().anchoredPosition=new Vector2((i%5-2)*108,75+(i/5)*32);
                buttons[i]=obj.GetComponent<Button>();labels[i]=obj.GetComponentInChildren<Text>();
            }
            hud.inventoryButtons=buttons;hud.inventoryLabels=labels;hud.active.rectTransform.anchoredPosition=new Vector2(0,180);
            var help=safe.Find("DesktopHelp").GetComponent<Text>();help.text+="\nR search / M magnet / U upgrade";
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);EditorSceneManager.SaveScene(session.gameObject.scene);AssetDatabase.SaveAssets();Debug.Log("LAB_ECONOMY_UI_OK");
        }
        static Text Label(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size)
        {
            var obj=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)obj.transform;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=pos;rect.sizeDelta=size;
            var text=obj.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=17;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;return text;
        }
        static Button MakeButton(string name,Transform parent,float y,UnityEngine.Events.UnityAction action,out Text label)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(Button));var r=(RectTransform)obj.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(1,1);r.anchoredPosition=new Vector2(-150,y);r.sizeDelta=new Vector2(240,54);
            var image=obj.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.08f,.26f,.32f,.96f);var button=obj.GetComponent<Button>();button.targetGraphic=image;UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick,action);
            label=Label(name+"Label",r,new Vector2(.5f,.5f),Vector2.zero,r.sizeDelta);return button;
        }
    }
}
