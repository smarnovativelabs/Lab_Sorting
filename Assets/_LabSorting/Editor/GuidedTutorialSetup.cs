using System;
using System.Linq;
using LabSorting.Player;
using LabSorting.Sorting;
using LabSorting.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LabSorting.Editor
{
    // Adds the hand-guided first-run tutorial to the open lab scene. Safe to run again: it rebuilds its own UI.
    public static class GuidedTutorialSetup
    {
        public const string HandPath="Assets/_LabSorting/Art/UI/TutorialHand.png";
        // Fingertip position in the cropped hand image (measured when it was cut from the UI sheet).
        static readonly Vector2 FingerTip=new Vector2(.971f,.576f);
        const string RootName="GuidedTutorial";

        [MenuItem("Tools/Lab Sorting/Designer/Add Guided Tutorial")]
        public static void Menu()
        {
            try{Apply();EditorUtility.DisplayDialog("Lab Sorting","Guided tutorial added and scene saved.","OK");}
            catch(Exception e){Debug.LogException(e);EditorUtility.DisplayDialog("Lab Sorting",e.Message,"OK");}
        }

        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetActiveScene();
            var settings=Object.FindObjectsByType<SettingsTutorialPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None).SingleOrDefault(s=>s.gameObject.scene==scene);
            if(!settings) throw new InvalidOperationException("No SettingsTutorialPanel in the open scene; open the lab scene first.");
            var session=Object.FindObjectsByType<SortingSession>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(s=>s.gameObject.scene==scene);
            var actions=Object.FindObjectsByType<ContextActionHUD>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(s=>s.gameObject.scene==scene);
            var economy=Object.FindObjectsByType<EconomyHUD>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(s=>s.gameObject.scene==scene);
            var touch=Object.FindObjectsByType<LabTouchArea>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s.gameObject.scene==scene).ToArray();
            var safeArea=(RectTransform)settings.root.transform.parent;
            var font=settings.title.font;
            var sprite=ImportHand();

            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add guided tutorial");
            var old=safeArea.Find(RootName);if(old)Undo.DestroyObjectImmediate(old.gameObject);

            // Root: whole safe area, drawn under the modal panels, never catching touches itself.
            var root=NewUI(RootName,safeArea);Stretch(root);root.SetSiblingIndex(settings.root.transform.GetSiblingIndex());
            var canvasGroup=root.gameObject.AddComponent<CanvasGroup>();canvasGroup.alpha=0;canvasGroup.blocksRaycasts=false;

            // Hint bubble under the milestone line, top centre.
            var bubble=NewUI("Bubble",root);Anchor(bubble,new Vector2(.5f,1),new Vector2(0,-222),new Vector2(660,88));
            var panel=bubble.gameObject.AddComponent<Image>();panel.color=new Color(.05f,.12f,.17f,.92f);panel.raycastTarget=false;
            var stepLabel=Label("Step",bubble,font,14,new Color(.55f,.88f,.8f),TextAnchor.UpperLeft);Anchor(stepLabel.rectTransform,new Vector2(.5f,.5f),new Vector2(-270,30),new Vector2(100,20));
            var message=Label("Message",bubble,font,19,Color.white,TextAnchor.MiddleLeft);Anchor(message.rectTransform,new Vector2(.5f,.5f),new Vector2(-50,-6),new Vector2(520,64));
            var gotIt=NewButton("GotIt",bubble,font,"GOT IT",new Color(.08f,.52f,.43f));Anchor((RectTransform)gotIt.transform,new Vector2(.5f,.5f),new Vector2(275,20),new Vector2(90,36));
            var skip=NewButton("Skip",bubble,font,"SKIP",new Color(.10f,.30f,.36f));Anchor((RectTransform)skip.transform,new Vector2(.5f,.5f),new Vector2(275,-22),new Vector2(90,34));

            // The hand from the UI sheet, pivot on the fingertip so it can rotate and press around the target.
            var hand=NewUI("Hand",root);var image=hand.gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;image.preserveAspect=true;
            hand.anchorMin=hand.anchorMax=new Vector2(.5f,.5f);hand.pivot=FingerTip;hand.sizeDelta=new Vector2(118,95);hand.gameObject.SetActive(false);

            var guided=settings.GetComponent<GuidedTutorial>();if(!guided)guided=Undo.AddComponent<GuidedTutorial>(settings.gameObject);
            Undo.RecordObject(guided,"Wire guided tutorial");
            guided.session=session;guided.actions=actions;guided.area=root;guided.group=canvasGroup;guided.hand=hand;guided.bubble=bubble;
            guided.message=message;guided.stepLabel=stepLabel;guided.skipButton=skip;guided.gotItButton=gotIt;
            guided.joystick=(RectTransform)touch.Single(t=>t.movement).transform;guided.lookArea=(RectTransform)touch.Single(t=>!t.movement).transform;
            guided.pickButton=(RectTransform)actions.pick.transform;guided.placeButton=(RectTransform)actions.place.transform;guided.throwButton=(RectTransform)actions.drop.transform;
            guided.searchButton=(RectTransform)economy.searchButton.transform;guided.magnetButton=(RectTransform)economy.magnetButton.transform;
            guided.preferencesPrefix=settings.preferencesPrefix;
            UnityEventTools.AddPersistentListener(skip.onClick,new UnityAction(guided.Skip));
            UnityEventTools.AddPersistentListener(gotIt.onClick,new UnityAction(guided.GotIt));
            EditorUtility.SetDirty(guided);

            AddHowToPlay(settings,font);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Could not save "+scene.path);
            Debug.Log("GUIDED_TUTORIAL_ADDED scene="+scene.path);
        }

        // Settings card: "Replay tutorial" now restarts the guided tutorial; a new "How to play" opens the old cards.
        static void AddHowToPlay(SettingsTutorialPanel settings,Font font)
        {
            var content=settings.settingsContent.transform;
            var existing=content.Find("HowToPlay");if(existing)Undo.DestroyObjectImmediate(existing.gameObject);
            var reset=settings.resetButton;
            var copy=(GameObject)Object.Instantiate(reset.gameObject,content);copy.name="HowToPlay";Undo.RegisterCreatedObjectUndo(copy,"How to play");
            var button=copy.GetComponent<Button>();
            for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);
            UnityEventTools.AddPersistentListener(button.onClick,new UnityAction(settings.OpenHowToPlay));
            Anchor((RectTransform)copy.transform,new Vector2(.5f,.5f),new Vector2(0,-158),new Vector2(270,44));
            var label=copy.GetComponentInChildren<Text>();if(label){Undo.RecordObject(label,"Label");label.text="HOW TO PLAY";}
            var replay=settings.replayButton.GetComponentInChildren<Text>();if(replay){Undo.RecordObject(replay,"Label");replay.text="REPLAY TUTORIAL";}
            var help=content.Find("Help") as RectTransform;if(help){Undo.RecordObject(help,"Move help");help.anchoredPosition=new Vector2(help.anchoredPosition.x,-207);}
        }

        static Sprite ImportHand()
        {
            var importer=AssetImporter.GetAtPath(HandPath) as TextureImporter;
            if(!importer) throw new System.IO.FileNotFoundException("Hand sprite missing",HandPath);
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.textureType=TextureImporterType.Sprite;settings.spriteMode=(int)SpriteImportMode.Single;
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=FingerTip;settings.spriteMeshType=SpriteMeshType.FullRect;
            settings.mipmapEnabled=false;settings.alphaIsTransparency=true;settings.wrapMode=TextureWrapMode.Clamp;settings.sRGBTexture=true;
            importer.SetTextureSettings(settings);importer.maxTextureSize=256;importer.textureCompression=TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(HandPath);
            if(!sprite) throw new InvalidOperationException("Hand texture did not import as a sprite.");
            return sprite;
        }
        static RectTransform NewUI(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(RectTransform));Undo.RegisterCreatedObjectUndo(go,name);
            go.layer=parent.gameObject.layer;var rect=(RectTransform)go.transform;rect.SetParent(parent,false);return rect;
        }
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.pivot=new Vector2(.5f,.5f);}
        static void Anchor(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;}
        static Text Label(string name,Transform parent,Font font,int size,Color color,TextAnchor alignment)
        {
            var text=NewUI(name,parent).gameObject.AddComponent<Text>();text.font=font;text.fontSize=size;text.color=color;text.alignment=alignment;
            text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.supportRichText=true;return text;
        }
        static Button NewButton(string name,Transform parent,Font font,string caption,Color color)
        {
            var rect=NewUI(name,parent);var image=rect.gameObject.AddComponent<Image>();image.color=color;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var label=Label("Label",rect,font,16,Color.white,TextAnchor.MiddleCenter);Stretch(label.rectTransform);label.text=caption;
            return button;
        }
    }
}
