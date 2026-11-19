#if UNITY_EDITOR || DEVELOPMENT_BUILD
using LabSorting.Services;
using UnityEngine;

namespace LabSorting.UI
{
    // Runtime-only UI: no scene/prefab reference can leak it into a release player.
    public sealed class CommerceDebugPanel : MonoBehaviour
    {
        CommerceDebugControls controls;
        SupplyCommerce commerce;
        GameObject overlay;
        UnityEngine.UI.Text details;
        UnityEngine.UI.Button crash;
        UnityEngine.UI.Button[] arms;
        bool crashConfirmed,previousBlocked,previousCursorVisible;
        CursorLockMode previousCursor;
        LabSorting.Player.LabPlayerInput blockedPlayer;
        public bool IsOpen=>overlay&&overlay.activeSelf;
        public static void Open(SupplyCommerce commerce,Font font)
        {
            if(!commerce)return;
            var controls=commerce.GetComponent<CommerceDebugControls>();
            if(!controls)controls=commerce.gameObject.AddComponent<CommerceDebugControls>();
            var panel=commerce.GetComponent<CommerceDebugPanel>();
            if(!panel)panel=commerce.gameObject.AddComponent<CommerceDebugPanel>();
            if(!panel.overlay)panel.Build(commerce,controls,font);
            panel.Show();
        }
        void Build(SupplyCommerce owner,CommerceDebugControls debug,Font font)
        {
            commerce=owner;controls=debug;
            overlay=new GameObject("Development Purchase Debug",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            overlay.transform.SetParent(transform,false);
            var canvas=overlay.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var scaler=overlay.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var background=new GameObject("Input Blocker",typeof(RectTransform),typeof(UnityEngine.UI.Image));background.transform.SetParent(overlay.transform,false);
            Stretch((RectTransform)background.transform);background.GetComponent<UnityEngine.UI.Image>().color=new Color(.035f,.055f,.09f,.98f);
            var content=new GameObject("Controls",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup));content.transform.SetParent(background.transform,false);
            var rect=(RectTransform)content.transform;Stretch(rect);rect.offsetMin=new Vector2(60,25);rect.offsetMax=new Vector2(-60,-25);
            var layout=content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=10;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            AddText(content.transform,font,"PURCHASE RECOVERY TESTS — DEVELOPMENT ONLY",32,48);
            AddText(content.transform,font,"Use sandbox purchases. Arm one test, close, then buy. Holds reset on app restart.\nEditor uses fake purchases; durable crash checkpoints must also be verified on device.",20,60);
            details=AddText(content.transform,font,"",21,150);
            arms=new[]{
                AddButton(content.transform,font,"1. STALL NEXT CONFIRMATION (30s)",()=>Arm(CommerceDebugControls.TestMode.ConfirmationStall)),
                AddButton(content.transform,font,"2. PAUSE AFTER REVENUE SAVE / BEFORE CONFIRM",()=>Arm(CommerceDebugControls.TestMode.AfterJournal)),
                AddButton(content.transform,font,"3. PAUSE AFTER STORE SUCCESS / BEFORE HANDLER",()=>Arm(CommerceDebugControls.TestMode.AfterStoreSuccess))};
            AddButton(content.transform,font,"RESUME / CANCEL ARMED TEST",()=>{crashConfirmed=false;controls.Resume();Refresh();});
            crash=AddButton(content.transform,font,"CRASH AT CHECKPOINT (DEVICE ONLY)",()=>
            {
                if(!crashConfirmed){crashConfirmed=true;Refresh();return;}
                controls.CrashAtCheckpoint();
            });
            AddButton(content.transform,font,"CLOSE — KEEP TEST ARMED",Hide);
            controls.CheckpointReached+=Show;
            overlay.SetActive(false);
        }
        void Arm(CommerceDebugControls.TestMode mode){crashConfirmed=false;controls.Arm(mode);Refresh();}
        void Show()
        {
            if(!overlay)return;
            if(!IsOpen)
            {
                previousCursor=Cursor.lockState;previousCursorVisible=Cursor.visible;
                blockedPlayer=commerce.session?commerce.session.player:null;
                if(blockedPlayer){previousBlocked=blockedPlayer.blocked;blockedPlayer.SetBlocked(true);}
            }
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            overlay.SetActive(true);crashConfirmed=false;Refresh();
        }
        public void Hide()
        {
            if(!IsOpen)return;
            overlay.SetActive(false);
            if(blockedPlayer)blockedPlayer.SetBlocked(previousBlocked);
            blockedPlayer=null;Cursor.lockState=previousCursor;Cursor.visible=previousCursorVisible;
        }
        void Update(){if(IsOpen)Refresh();}
        void Refresh()
        {
            if(!details)return;
            var telemetry=commerce.GetComponent<LabTelemetry>();
            details.text=controls.Notice+"\n"+controls.State+"\n"+commerce.DebugPurchaseState+"\n"+(telemetry?telemetry.DebugOutboxState:"Telemetry component missing");
            foreach(var button in arms)button.interactable=!controls.Armed&&commerce.DebugCanArm;
            crash.interactable=controls.Paused&&!Application.isEditor;
            crash.GetComponentInChildren<UnityEngine.UI.Text>().text=crashConfirmed?"CONFIRM CRASH NOW — RELAUNCH APP AFTERWARDS":"CRASH AT CHECKPOINT (DEVICE ONLY)";
        }
        void OnDestroy(){Hide();if(controls)controls.CheckpointReached-=Show;if(overlay)Destroy(overlay);}
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static UnityEngine.UI.Text AddText(Transform parent,Font font,string text,int size,float height)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(UnityEngine.UI.Text),typeof(UnityEngine.UI.LayoutElement));go.transform.SetParent(parent,false);
            var label=go.GetComponent<UnityEngine.UI.Text>();label.font=font?font:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.fontSize=size;label.color=Color.white;label.alignment=TextAnchor.MiddleLeft;label.raycastTarget=false;
            go.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=height;return label;
        }
        static UnityEngine.UI.Button AddButton(Transform parent,Font font,string text,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button),typeof(UnityEngine.UI.LayoutElement));go.transform.SetParent(parent,false);
            go.GetComponent<UnityEngine.UI.Image>().color=new Color(.12f,.26f,.39f);
            go.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=48;
            var label=AddText(go.transform,font,text,21,48);Stretch((RectTransform)label.transform);label.alignment=TextAnchor.MiddleCenter;
            var button=go.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=go.GetComponent<UnityEngine.UI.Image>();button.onClick.AddListener(action);return button;
        }
    }
}
#endif
