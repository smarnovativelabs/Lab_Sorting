using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using LabSorting.Sorting;
using LabSorting.UI;
using System.IO;
namespace LabSorting.Editor
{
    public static class PolishSceneUpgrade
    {
        [MenuItem("Tools/Lab Sorting/Add Polish and Progress Panel")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            var session=Object.FindFirstObjectByType<SortingSession>();var hud=Object.FindFirstObjectByType<SortingHUD>();
            if(!session||!hud) throw new System.InvalidOperationException("Sorting scene missing");
            var feedback=session.GetComponent<SortingFeedback>();if(!feedback) feedback=session.gameObject.AddComponent<SortingFeedback>();feedback.session=session;
            feedback.pick=MakeClip("Pick",0,.09f);feedback.place=MakeClip("Place",1,.13f);feedback.drop=MakeClip("Throw",2,.18f);feedback.reject=MakeClip("Reject",3,.10f);
            var source=feedback.GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=1;
            if(!hud.GetComponent<ItemsProgressPanel>()) BuildPanel(hud,session,feedback);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);EditorSceneManager.SaveScene(session.gameObject.scene);AssetDatabase.SaveAssets();Debug.Log("LAB_POLISH_UI_OK");
        }
        static AudioClip MakeClip(string name,int kind,float seconds)
        {
            const string folder="Assets/_LabSorting/Audio";Directory.CreateDirectory(folder);string path=folder+"/"+name+".wav";
            if(!File.Exists(path))
            {
                const int rate=44100;int n=Mathf.RoundToInt(rate*seconds);var random=new System.Random(71+kind);double phase=0,noise=0;
                using(var writer=new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+n*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(n*2);
                    for(int i=0;i<n;i++)
                    {
                        double t=i/(double)rate,u=i/(double)(n-1);double freq=kind==0?640+400*u:kind==1?400-150*u:kind==2?200+160*u:160;
                        phase+=2*System.Math.PI*freq/rate;noise=noise*.75+(random.NextDouble()*2-1)*.25;
                        double wave=kind==2?noise*.8+System.Math.Sin(phase)*.12:System.Math.Sin(phase)*.65+System.Math.Sin(phase*2.1)*.15;
                        double envelope=System.Math.Min(1,t/.004)*System.Math.Pow(1-u,2);writer.Write((short)(wave*envelope*.65*short.MaxValue));
                    }
                }
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static void BuildPanel(SortingHUD hud,SortingSession session,SortingFeedback feedback)
        {
            var safe=(RectTransform)hud.transform.Find("SafeArea");if(!safe) throw new System.InvalidOperationException("SafeArea missing");
            var panel=hud.gameObject.AddComponent<ItemsProgressPanel>();panel.session=session;panel.feedback=feedback;
            panel.openButton=Button("ItemProgressButton",safe,new Vector2(0,1),new Vector2(145,-205),new Vector2(240,52),"ITEM PROGRESS  [I]",panel.Toggle);
            var root=Rect("ItemProgressModal",safe,Vector2.zero,Vector2.zero,Vector2.zero);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            root.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(0,.015f,.025f,.78f);panel.root=root.gameObject;panel.group=root.gameObject.AddComponent<CanvasGroup>();
            var card=Rect("ProgressCard",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(850,560));card.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.055f,.10f,.14f,.99f);panel.card=card;
            panel.summary=Label("Summary",card,new Vector2(.5f,1),new Vector2(-20,-42),new Vector2(720,45),23);
            panel.closeButton=Button("Close",card,new Vector2(1,1),new Vector2(-38,-35),new Vector2(52,46),"X",panel.Close);
            Label("Legend",card,new Vector2(.5f,1),new Vector2(0,-85),new Vector2(780,30),16).text="Remaining includes floor items and items in your hand.";
            panel.rows=new UnityEngine.UI.Text[6];
            for(int i=0;i<6;i++)
            {
                var row=Rect("ItemRow"+i,card,new Vector2(.5f,1),new Vector2(0,-133-i*49),new Vector2(790,43));row.gameObject.AddComponent<UnityEngine.UI.Image>().color=i%2==0?new Color(.09f,.18f,.23f):new Color(.07f,.14f,.19f);
                panel.rows[i]=Label("Counts",row,new Vector2(.5f,.5f),Vector2.zero,new Vector2(758,38),19);panel.rows[i].alignment=TextAnchor.MiddleLeft;
            }
            panel.previousButton=Button("Previous",card,new Vector2(.5f,0),new Vector2(-120,114),new Vector2(120,42),"PREVIOUS",panel.Previous);
            panel.nextButton=Button("Next",card,new Vector2(.5f,0),new Vector2(120,114),new Vector2(120,42),"NEXT",panel.Next);
            panel.pageLabel=Label("Page",card,new Vector2(.5f,0),new Vector2(0,114),new Vector2(90,40),18);
            var sound=Button("SoundToggle",card,new Vector2(.5f,0),new Vector2(-140,52),new Vector2(250,48),"SOUND: ON",feedback.ToggleSound);panel.soundLabel=sound.GetComponentInChildren<UnityEngine.UI.Text>();
            var haptics=Button("HapticsToggle",card,new Vector2(.5f,0),new Vector2(140,52),new Vector2(250,48),"HAPTICS: ON",feedback.ToggleHaptics);panel.hapticsLabel=haptics.GetComponentInChildren<UnityEngine.UI.Text>();
            root.gameObject.SetActive(false);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size)
        {var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        static UnityEngine.UI.Text Label(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,int font)
        {var r=Rect(name,parent,anchor,pos,size);var t=r.gameObject.AddComponent<UnityEngine.UI.Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=font;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
        static UnityEngine.UI.Button Button(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,string caption,UnityEngine.Events.UnityAction action)
        {var r=Rect(name,parent,anchor,pos,size);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.12f,.39f,.43f);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;Label("Label",r,new Vector2(.5f,.5f),Vector2.zero,size,17).text=caption;UnityEventTools.AddPersistentListener(b.onClick,action);return b;}
    }
}
