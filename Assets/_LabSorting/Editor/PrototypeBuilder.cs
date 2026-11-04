using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using KinematicCharacterController;
using LabSorting.Player;
using LabSorting.Sorting;
using LabSorting.UI;

namespace LabSorting.Editor
{
    public static class PrototypeBuilder
    {
        const string Root="Assets/_LabSorting/";
        public const string ScenePath=Root+"Prototype/Scenes/SortingTest.unity";
        static Font font;
        [MenuItem("Tools/Lab Sorting/Create SortingTest (new only)")]
        public static void Build()
        {
            if(File.Exists(ScenePath)) throw new InvalidOperationException("SortingTest exists. Builder refuses to overwrite authored work.");
            Directory.CreateDirectory(Root+"Prototype/Materials");Directory.CreateDirectory(Root+"Prototype/Data");
            Directory.CreateDirectory(Root+"Prototype/Prefabs");Directory.CreateDirectory(Root+"Prefabs/Player");
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var floorMat=Material("Floor",new Color(.13f,.18f,.22f));var rackMat=Material("Rack",new Color(.25f,.34f,.39f));
            var green=Material("Valid",new Color(.12f,.8f,.43f));var red=Material("Invalid",new Color(.95f,.2f,.2f));var neutral=Material("Neutral",new Color(.12f,.3f,.35f));
            Cube("Floor",new Vector3(0,-.15f,0),new Vector3(19,.3f,17),floorMat);
            Cube("BackWall",new Vector3(0,2,7.5f),new Vector3(19,4,.3f),floorMat);
            Cube("LeftWall",new Vector3(-9.4f,2,0),new Vector3(.3f,4,17),floorMat);
            Cube("RightWall",new Vector3(9.4f,2,0),new Vector3(.3f,4,17),floorMat);
            Cube("FrontWall",new Vector3(0,2,-8.4f),new Vector3(19,4,.3f),floorMat);
            Cube("StepTest",new Vector3(-7,.1f,-3),new Vector3(2,.2f,2),rackMat);
            var light=new GameObject("KeyLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(50,-30,0);
            RenderSettings.ambientLight=new Color(.65f,.7f,.75f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            var player=new GameObject("LabPlayer");player.layer=2;player.transform.position=new Vector3(0,.1f,-4);
            var capsule=player.AddComponent<CapsuleCollider>();capsule.radius=.3f;capsule.height=1.8f;capsule.center=new Vector3(0,.9f,0);
            var motor=player.AddComponent<KinematicCharacterMotor>();motor.SetCapsuleDimensions(.3f,1.7f,.85f);motor.MaxStepHeight=.3f;
            var character=player.AddComponent<LabCharacterMotor>();character.Motor=motor;character.MaxStableMoveSpeed=3.2f;character.StableMovementSharpness=18;character.OrientationSharpness=18;character.FramePerfectRotation=false;
            var yaw=new GameObject("LookYaw");yaw.transform.SetParent(player.transform,false);yaw.transform.localPosition=new Vector3(0,1.6f,0);
            var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";cameraObject.transform.SetParent(yaw.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.transform.localRotation=Quaternion.Euler(15,0,0);camera.fieldOfView=65;camera.nearClipPlane=.05f;camera.farClipPlane=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.13f,.18f);cameraObject.AddComponent<AudioListener>();
            var look=yaw.AddComponent<LabLookRig>();var so=new SerializedObject(look);so.FindProperty("yawPivot").objectReferenceValue=yaw.transform;so.FindProperty("cameraTarget").objectReferenceValue=camera.transform;so.ApplyModifiedPropertiesWithoutUndo();
            var input=player.AddComponent<LabPlayerInput>();input.character=character;input.look=look;input.view=camera;
            var hand=new GameObject("HeldItemSocket").transform;hand.SetParent(camera.transform,false);hand.localPosition=new Vector3(.42f,-.32f,.85f);
            var prefab=PrefabUtility.SaveAsPrefabAsset(player,Root+"Prefabs/Player/LabPlayer.prefab");
            UnityEngine.Object.DestroyImmediate(player);
            player=(GameObject)PrefabUtility.InstantiatePrefab(prefab);input=player.GetComponent<LabPlayerInput>();hand=player.transform.Find("LookYaw/Main Camera/HeldItemSocket");
            var session=new GameObject("SortingSession").AddComponent<SortingSession>();session.player=input;session.hand=hand;
            session.racks=new SortRack[5];session.items=new SortItem[50];
            Color[] colors={new Color(.95f,.55f,.18f),new Color(.24f,.7f,.95f),new Color(.7f,.4f,.9f),new Color(.25f,.85f,.55f),new Color(.95f,.35f,.42f)};
            string[] names={"Panadol","Gloves","Sample Jar","Test Tube","Bandages"};
            var defs=new ItemDefinition[5];var itemPrefabs=new GameObject[5];
            for(int t=0;t<5;t++)
            {
                var def=ScriptableObject.CreateInstance<ItemDefinition>();def.id="type-"+t;def.displayName=names[t];def.color=colors[t];AssetDatabase.CreateAsset(def,Root+"Prototype/Data/"+names[t]+".asset");defs[t]=def;
                var obj=Cube(names[t],Vector3.zero,new Vector3(.24f,.3f,.24f),Material("Item"+t,colors[t]));
                var item=obj.AddComponent<SortItem>();item.definition=def;item.originalScale=obj.transform.localScale;item.hitbox=obj.GetComponent<Collider>();item.visual=obj.GetComponent<Renderer>();item.outline=obj.AddComponent<Outline>();item.outline.enabled=false;
                itemPrefabs[t]=PrefabUtility.SaveAsPrefabAsset(obj,Root+"Prototype/Prefabs/"+names[t]+".prefab");UnityEngine.Object.DestroyImmediate(obj);
                var rackObject=new GameObject("Rack "+(t+1));rackObject.transform.position=new Vector3((t-2)*3.2f,0,4.2f);
                var rack=rackObject.AddComponent<SortRack>();rack.id="rack-"+t;rack.slots=new Transform[15];rack.neutral=neutral;rack.valid=green;rack.invalid=red;
                for(int level=0;level<3;level++)
                {
                    float y=.5f+level*.5f;
                    var shelf=Cube("Shelf "+level,rackObject.transform.position+new Vector3(0,y,0),new Vector3(2.5f,.08f,.8f),rackMat);shelf.transform.SetParent(rackObject.transform,true);
                    for(int slot=0;slot<5;slot++){var point=new GameObject("Slot "+(level*5+slot)).transform;point.SetParent(rackObject.transform,false);point.localPosition=new Vector3((slot-2)*.44f,y+.19f,0);rack.slots[level*5+slot]=point;}
                }
                foreach(float x in new[]{-1.2f,1.2f}) {var post=Cube("Post",rackObject.transform.position+new Vector3(x,.85f,.3f),new Vector3(.08f,1.7f,.08f),rackMat);post.transform.SetParent(rackObject.transform,true);}
                var strip=Cube("PlacementIndicator",rackObject.transform.position+new Vector3(0,1.9f,0),new Vector3(2.5f,.4f,.08f),neutral);strip.transform.SetParent(rackObject.transform,true);rack.indicator=strip.GetComponent<Renderer>();
                var label=new GameObject("RackLabel").AddComponent<TextMesh>();label.transform.SetParent(rackObject.transform,false);label.transform.localPosition=new Vector3(0,1.9f,-.055f);label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=48;label.characterSize=.045f;label.color=Color.white;rack.label=label;rack.Initialize();session.racks[t]=rack;
            }
            for(int i=0;i<50;i++) {var go=(GameObject)PrefabUtility.InstantiatePrefab(itemPrefabs[i%5]);go.name="Item "+i;go.transform.position=new Vector3((i%10-4.5f)*.62f,.17f,-1.8f+(i/10)*.7f);var item=go.GetComponent<SortItem>();item.id="item-"+i;session.items[i]=item;}
            BuildUI(session);
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            var builds=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);builds.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=builds.ToArray();
            AssetDatabase.SaveAssets();Debug.Log("LAB_SORTING_BUILD_OK: "+ScenePath);
        }
        static Material Material(string name,Color color) {var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;AssetDatabase.CreateAsset(m,Root+"Prototype/Materials/"+name+".mat");return m;}
        static GameObject Cube(string name,Vector3 pos,Vector3 scale,Material mat) {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;return go;}
        static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;}
        static Text Text(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,int sizeFont=20)
        {var text=Rect(name,parent,anchor,pos,size).gameObject.AddComponent<Text>();text.font=font;text.fontSize=sizeFont;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;}
        static Button Button(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
        {var rect=Rect(name,parent,anchor,pos,size);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.12f,.26f,.32f,.95f);var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;if(action!=null)button.onClick.AddListener(action);Text(name+"Label",rect,new Vector2(.5f,.5f),Vector2.zero,size,18).text=name;return button;}
        static void BuildUI(SortingSession session)
        {
            var canvas=new GameObject("SortingHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var safe=Rect("SafeArea",canvas.transform,Vector2.zero,Vector2.zero,Vector2.zero);safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.offsetMin=safe.offsetMax=Vector2.zero;safe.gameObject.AddComponent<Crystal.SafeArea>();
            var lookArea=Rect("LookDragArea",safe,new Vector2(.75f,.5f),Vector2.zero,new Vector2(640,720));lookArea.gameObject.AddComponent<UnityEngine.UI.Image>().color=Color.clear;lookArea.gameObject.AddComponent<LabTouchArea>().input=session.player;
            var hud=canvas.AddComponent<SortingHUD>();hud.session=session;
            hud.progress=Text("Progress",safe,new Vector2(.5f,1),new Vector2(0,-48),new Vector2(640,70),23);
            hud.active=Text("ActiveItem",safe,new Vector2(.5f,0),new Vector2(0,144),new Vector2(550,35));
            hud.status=Text("Status",safe,new Vector2(.5f,0),new Vector2(0,28),new Vector2(650,30),18);
            hud.target=Text("Target",safe,new Vector2(.5f,.5f),new Vector2(0,-45),new Vector2(450,36));
            Text("Crosshair",safe,new Vector2(.5f,.5f),Vector2.zero,new Vector2(30,30),22).text="+";
            Text("DesktopHelp",safe,new Vector2(0,1),new Vector2(170,-120),new Vector2(320,85),16).text="WASD move / drag right to look\nE pick / F place / Q throw\nTab cycle held item";
            var joystick=Rect("MovementJoystick",safe,Vector2.zero,new Vector2(140,135),new Vector2(180,180));joystick.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.15f,.3f,.36f,.75f);var touch=joystick.gameObject.AddComponent<LabTouchArea>();touch.input=session.player;touch.movement=true;var knob=Rect("Knob",joystick,new Vector2(.5f,.5f),Vector2.zero,new Vector2(60,60));knob.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.3f,.8f,.72f);knob.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;touch.knob=knob;
            // Persist listeners: runtime AddListener callbacks are not serialized into the scene.
            var pick=Button("PICK",safe,new Vector2(1,0),new Vector2(-240,120),new Vector2(130,65),null);InteractionUpgrade.Configure(pick,session,false);
            var place=Button("PLACE",safe,new Vector2(1,0),new Vector2(-90,120),new Vector2(130,65),null);InteractionUpgrade.Configure(place,session,true);
            var drop=Button("THROW",safe,new Vector2(1,0),new Vector2(-90,205),new Vector2(130,55),null);UnityEditor.Events.UnityEventTools.AddPersistentListener(drop.onClick,session.ThrowActive);
            hud.inventoryButtons=new Button[10];hud.inventoryLabels=new Text[10];
            for(int i=0;i<10;i++){var b=Button("Slot"+i,safe,new Vector2(.5f,0),new Vector2((i%5-2)*108,75+(i/5)*32),new Vector2(102,28),null);hud.inventoryButtons[i]=b;hud.inventoryLabels[i]=b.GetComponentInChildren<Text>();hud.inventoryLabels[i].fontSize=13;}
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}

