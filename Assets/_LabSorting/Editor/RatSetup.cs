using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using LabSorting.Sorting;

namespace LabSorting.Editor
{
    public static class RatSetup
    {
        const string Folder="Assets/_LabSorting/Rats";
        [MenuItem("Lab Sorting/Setup Rats")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop play first");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var session=UnityEngine.Object.FindFirstObjectByType<SortingSession>();
            
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_LabSorting/Art/FBX/Rat.fbx");
            var importer=(ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(model));
            var clips=importer.defaultClipAnimations.Take(2).ToArray();
            clips[0].name="Rat_Run";clips[1].name="Rat_Idle";
            foreach(var c in clips){c.loopTime=true;c.loopPose=true;c.keepOriginalPositionXZ=true;c.keepOriginalOrientation=true;}
            importer.clipAnimations=clips;importer.SaveAndReimport();
            var animations=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Rat.controller")??AnimatorController.CreateAnimatorControllerAtPath(Folder+"/Rat.controller");if(controller.layers[0].stateMachine.states.Length==0){controller.AddParameter("Running",AnimatorControllerParameterType.Bool);
            var machine=controller.layers[0].stateMachine;var idle=machine.AddState("Idle");var run=machine.AddState("Run");
            idle.motion=animations.First(c=>c.name=="Rat_Idle");run.motion=animations.First(c=>c.name=="Rat_Run");machine.defaultState=idle;
            var toRun=idle.AddTransition(run);toRun.hasExitTime=false;toRun.duration=.12f;toRun.AddCondition(AnimatorConditionMode.If,0,"Running");
            var toIdle=run.AddTransition(idle);toIdle.hasExitTime=false;toIdle.duration=.12f;toIdle.AddCondition(AnimatorConditionMode.IfNot,0,"Running");}
            MakeSound("Squeak",.23f,0);MakeSound("Scurry",.12f,1);MakeSound("Release",.24f,2);
            AssetDatabase.Refresh();
            var colonyGo=GameObject.Find("Lab Rats")??new GameObject("Lab Rats");Undo.RegisterCreatedObjectUndo(colonyGo,"Setup rats");var colony=Ensure<RatColony>(colonyGo);colony.session=session;colony.feedback=UnityEngine.Object.FindFirstObjectByType<SortingFeedback>();
            colony.squeak=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Squeak.wav");colony.scurry=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Scurry.wav");colony.release=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Release.wav");colony.landing=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_LabSorting/Audio/Materials/PlasticTap.wav");
            foreach(var name in new[]{"Squeak","Scurry","Release"}){var ai=(AudioImporter)AssetImporter.GetAtPath(Folder+"/"+name+".wav");ai.forceToMono=true;var settings=ai.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;ai.defaultSampleSettings=settings;ai.SaveAndReimport();}
            var definition=AssetDatabase.LoadAssetAtPath<ItemDefinition>(Folder+"/Rat.asset")??ScriptableObject.CreateInstance<ItemDefinition>();definition.id="lab-rat";definition.displayName="Rat";definition.isRat=true;definition.placementMaterial=PlacementMaterial.Plastic;
            if(!AssetDatabase.Contains(definition))AssetDatabase.CreateAsset(definition,Folder+"/Rat.asset");
            var glass=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name=="glass_Box");
            var bounds=glass.GetComponent<Renderer>().bounds;
            var rack=Ensure<SortRack>(glass.gameObject);rack.id="rat-glass-container";rack.ratsOnly=true;rack.slots=new Transform[10];
            var target=Ensure<BoxCollider>(glass.gameObject);target.center=glass.InverseTransformPoint(bounds.center);target.size=bounds.size;
            var labelGo=glass.Find("Rat Count")?glass.Find("Rat Count").gameObject:new GameObject("Rat Count");labelGo.transform.SetParent(glass,false);labelGo.transform.position=bounds.center+Vector3.up*(bounds.extents.y+.13f);labelGo.transform.rotation=Quaternion.Euler(0,-90,0);rack.label=Ensure<TextMesh>(labelGo);rack.label.characterSize=.01f;rack.label.fontSize=64;rack.label.anchor=TextAnchor.MiddleCenter;rack.label.color=Color.white;
            var rats=new LabRat[10];var newItems=session.items.Where(i=>!i.definition.isRat).ToList();var random=new System.Random(417);
            int mask=~((1<<2)|(1<<LayerMask.NameToLayer("SortingItem")));
            for(int i=0;i<10;i++)
            {
                var ratName="Rat "+(i+1).ToString("00");var root=colonyGo.transform.Find(ratName)?colonyGo.transform.Find(ratName).gameObject:new GameObject(ratName);root.transform.position=Vector3.zero;root.transform.SetParent(colonyGo.transform);
                var visual=root.transform.childCount>0?root.transform.GetChild(0).gameObject:(GameObject)PrefabUtility.InstantiatePrefab(model);visual.transform.SetParent(root.transform,false);visual.transform.localScale=Vector3.one*.35f;visual.transform.localPosition=Vector3.zero;
                var renderer=visual.GetComponentInChildren<SkinnedMeshRenderer>();
                visual.transform.position+=Vector3.up*(-renderer.bounds.min.y+.004f);
                var b=renderer.bounds;definition.size=b.size;definition.center=root.transform.InverseTransformPoint(b.center);
                var box=Ensure<BoxCollider>(root);box.center=definition.center;box.size=definition.size;
                var item=Ensure<SortItem>(root);item.id="lab-rat-"+(i+1).ToString("00");item.definition=definition;item.hitbox=box;item.visual=renderer;item.originalScale=Vector3.one;item.heldScale=1;item.ApplyItemLayer();item.DisableShadows();
                var rat=Ensure<LabRat>(root);rat.colony=colony;rat.animator=visual.GetComponent<Animator>();if(!rat.animator)rat.animator=visual.AddComponent<Animator>();rat.animator.runtimeAnimatorController=controller;rat.animator.applyRootMotion=false;rat.animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;rats[i]=rat;
                bool found=false;
                for(int attempt=0;attempt<1000;attempt++)
                {
                    var p=new Vector3((float)random.NextDouble()*20-10,.004f,(float)random.NextDouble()*15-7.5f);
                    if(Physics.CheckSphere(p+Vector3.up*.1f,.09f,mask,QueryTriggerInteraction.Ignore))continue;
                    if(!Physics.Raycast(p+Vector3.up*.2f,Vector3.down,out var floor,.3f,mask,QueryTriggerInteraction.Ignore)||Mathf.Abs(floor.point.y)>.05f)continue;
                    root.transform.position=p;found=true;break;
                }
                if(!found)throw new InvalidOperationException("Could not find clear floor for rat "+i);
                newItems.Add(item);
                var slot=glass.Find("Rat slot "+i)?glass.Find("Rat slot "+i):new GameObject("Rat slot "+i).transform;slot.SetParent(glass,false);
                slot.position=new Vector3(bounds.center.x+(i%2==0?-.105f:.105f),bounds.min.y+.025f,bounds.min.z+.10f+(i/2)*.185f);rack.slots[i]=slot;
            }
            colony.rats=rats;session.items=newItems.ToArray();session.racks=session.racks.Where(r=>r!=rack).Concat(new[]{rack}).ToArray();rack.Initialize();
            // The designer's reference instance is retained but not duplicated in gameplay.
            var reference=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>g.name=="Rat");if(reference){Undo.RecordObject(reference,"Disable reference rat");reference.SetActive(false);}
            EditorUtility.SetDirty(definition);EditorUtility.SetDirty(session);RatSizingUpgrade.Apply();
            Debug.Log("RATS_SETUP_COMPLETE: 10 rats, container, animations, audio and save catalog installed");
        }
        // Original soft game foley; no third-party samples or runtime synthesis.
        static T Ensure<T>(GameObject go) where T:Component {var component=go.GetComponent<T>();return component?component:go.AddComponent<T>();}
        static void MakeSound(string name,float duration,int kind)
        {
            const int rate=22050;int count=(int)(rate*duration);var random=new System.Random(89+kind);double phase=0;
            using(var writer=new BinaryWriter(File.Create(Folder+"/"+name+".wav")))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                for(int i=0;i<count;i++){double t=i/(double)count,envelope=Math.Sin(Math.PI*t)*Math.Sin(Math.PI*t);phase+=2*Math.PI*(2100+900*Math.Sin(t*Math.PI))/rate;double noise=random.NextDouble()*2-1;double sample=kind==0?Math.Sin(phase)*.24:kind==1?noise*.12*Math.Pow(Math.Sin(t*Math.PI*4),8):noise*.18*(1-t);writer.Write((short)(sample*envelope*32767));}
            }
        }
    }
}






