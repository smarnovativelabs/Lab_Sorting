using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using LabSorting.UI;
using LabSorting.Sorting;
namespace LabSorting.Editor
{
    public static class AudioPresentationSetup
    {
        static AudioClip Clip(string name,float seconds,int kind)
        {
            const int rate=22050;int n=(int)(rate*seconds);const string folder="Assets/_LabSorting/Audio/Presentation";Directory.CreateDirectory(folder);string path=folder+"/"+name+".wav";
            if(!File.Exists(path))using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+n*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(n*2);
                for(int i=0;i<n;i++)
                {
                    double t=i/(double)rate,v;
                    if(kind==0)
                    {
                        // Integer-period harmonics make the ventilation bed loop without a seam.
                        v=.08*Math.Sin(2*Math.PI*60*t)+.035*Math.Sin(2*Math.PI*120*t);
                        for(int k=0;k<24;k++)v+=.003*Math.Sin(2*Math.PI*(180+k*31.125)*t+k*1.618);
                        v*=.85+.15*Math.Sin(2*Math.PI*t/8);
                    }
                    else
                    {
                        double envelope=Math.Min(1,t/.008)*Math.Pow(1-t/seconds,3);
                        double hz=kind==1?560:380;
                        v=(Math.Sin(2*Math.PI*hz*t)*.28+Math.Sin(2*Math.PI*hz*2*t)*.035)*envelope;
                    }
                    writer.Write((short)(Math.Max(-1,Math.Min(1,v))*short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static AudioSource Source(Transform parent,string name)
        {
            var t=parent.Find(name);if(!t){t=new GameObject(name).transform;t.SetParent(parent,false);}var s=t.GetComponent<AudioSource>();if(!s)s=t.gameObject.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;s.priority=180;return s;
        }
        public static void Apply()
        {
            var hud=UnityEngine.Object.FindFirstObjectByType<SortingHUD>();var sound=hud.GetComponent<LabSoundscape>();if(!sound)sound=hud.gameObject.AddComponent<LabSoundscape>();sound.feedback=hud.session.GetComponent<SortingFeedback>();sound.tour=hud.GetComponent<FirstLaunchTour>();
            sound.ambience=Source(hud.transform,"LabAmbience");sound.ambience.clip=Clip("VentilationLoop",8,0);sound.ambience.loop=true;sound.ambience.volume=0;
            sound.ui=Source(hud.transform,"InterfaceAudio");sound.ui.loop=false;sound.ui.priority=80;sound.click=Clip("SoftClick",.085f,1);sound.close=Clip("SoftClose",.12f,2);
            var actions=hud.GetComponent<ContextActionHUD>();var supply=hud.GetComponent<SupplyPanel>();
            foreach(var button in hud.GetComponentsInChildren<UnityEngine.UI.Button>(true).Concat(sound.tour.overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true)))
            {
                if(button.gameObject==actions.pick.gameObject||button.gameObject==actions.place.gameObject||button.gameObject==actions.drop.gameObject||button==supply.useButton)continue;
                var cue=button.GetComponent<UISoundFeedback>();if(!cue)cue=button.gameObject.AddComponent<UISoundFeedback>();cue.soundscape=sound;cue.closing=button.name=="Close"||button.name=="Resume"||button.name=="Skip";
            }
            sound.tour.handoffDuration=.6f;
            var report=new System.Text.StringBuilder();
            foreach(var definition in hud.session.items.Select(i=>i.definition).Distinct())
            {
                var old=definition.displayName;var clean=Regex.Replace(old,@"\s+\d+$","");clean=clean.Replace("Wakie Takie","Walkie Talkie").Replace("Micro Scope","Microscope");
                if(old==clean)continue;report.AppendLine(definition.id+": "+old+" -> "+clean);definition.displayName=clean;EditorUtility.SetDirty(definition);
            }
            var collection=UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection("LabUI");
            foreach(var table in collection.StringTables)foreach(var pair in new[]{new[]{"Wakie Takie","Walkie Talkie"},new[]{"Micro Scope","Microscope"}}){var old=table.GetEntry(pair[0]);if(old!=null)table.AddEntry(pair[1],table.LocaleIdentifier.Code=="en"?pair[1]:old.LocalizedValue);EditorUtility.SetDirty(table);}
            EditorUtility.SetDirty(collection.SharedData);
            File.WriteAllText("Logs/ItemDisplayNameChanges.txt",report.ToString());AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);if(!EditorSceneManager.SaveScene(hud.gameObject.scene))throw new IOException("Audio presentation scene save failed");
        }
    }
}
