using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using LabSorting.UI;
namespace LabSorting.Editor
{
    public static class MusicSpeedUpgrade
    {
        static void CreateMusic(string path)
        {
            if(System.IO.File.Exists(path))return;
            const int rate=22050,count=rate*24;double[][] chords={new[]{130.81,164.81,196,246.94},new[]{110.0,130.81,164.81,196},new[]{87.31,130.81,174.61,220},new[]{98.0,146.83,196,246.94}};
            using(var w=new System.IO.BinaryWriter(System.IO.File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
                for(int n=0;n<count;n++){double t=(double)n/rate,u=t%6,beat=t%1.5,env=System.Math.Min(1,System.Math.Min(u/.65,(6-u)/.9)),pad=0;var notes=chords[(int)(t/6)];foreach(double f in notes)pad+=(System.Math.Sin(2*System.Math.PI*f*t)+.15*System.Math.Sin(4*System.Math.PI*f*t))/4;double bell=System.Math.Sin(2*System.Math.PI*notes[(int)(u/1.5)%4]*2*beat)*System.Math.Exp(-beat*3)*System.Math.Min(1,beat/.025);double v=(pad*.15*env+bell*.035)*System.Math.Min(1,System.Math.Min(t/.5,(24-t)/.5));w.Write((short)(v*32767));}
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
        static void Pos(Component c,float y){var r=(RectTransform)c.transform;r.anchoredPosition=new Vector2(r.anchoredPosition.x,y);}
        public static void Apply()
        {
            const string path="Assets/_LabSorting/Audio/Presentation/LabMusic.wav";
            CreateMusic(path);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);var sample=importer.defaultSampleSettings;sample.loadType=AudioClipLoadType.CompressedInMemory;sample.compressionFormat=AudioCompressionFormat.Vorbis;sample.quality=.6f;importer.defaultSampleSettings=sample;importer.loadInBackground=true;importer.SaveAndReimport();
            var h=Object.FindFirstObjectByType<SortingHUD>();var sound=h.GetComponent<LabSoundscape>();
            if(!sound.music){var go=new GameObject("Quiet Background Music");go.transform.SetParent(sound.transform,false);sound.music=go.AddComponent<AudioSource>();}
            sound.music.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);sound.music.loop=true;sound.music.playOnAwake=false;sound.music.spatialBlend=0;sound.music.volume=0;sound.music.priority=180;sound.musicVolume=.1f;
            var settings=h.GetComponent<SettingsTutorialPanel>();var root=settings.settingsContent.transform;
            if(!settings.moveSpeedSlider){settings.moveSpeedSlider=Object.Instantiate(settings.sensitivitySlider,root);settings.moveSpeedSlider.name="MoveSpeed";settings.moveSpeedSlider.onValueChanged=new UnityEngine.UI.Slider.SliderEvent();UnityEventTools.AddPersistentListener(settings.moveSpeedSlider.onValueChanged,settings.ChangeMoveSpeed);settings.moveSpeedLabel=Object.Instantiate(settings.sensitivityLabel,root);settings.moveSpeedLabel.name="MoveSpeedLabel";}
            settings.moveSpeedSlider.minValue=.75f;settings.moveSpeedSlider.maxValue=1.2f;settings.moveSpeedSlider.SetValueWithoutNotify(1);settings.moveSpeedLabel.text="MOVE SPEED  1.00x";
            ((RectTransform)settings.title.transform.parent).sizeDelta=new Vector2(720,580);Pos(settings.title,245);var titleArt=settings.title.transform.parent.Find("DesignerTitle_"+settings.title.name);if(titleArt)Pos(titleArt,245);
            Pos(settings.sensitivityLabel,185);Pos(settings.sensitivitySlider,140);Pos(root.Find("SliderRange"),103);
            Pos(settings.moveSpeedLabel,62);Pos(settings.moveSpeedSlider,20);
            Pos(settings.soundButton,-50);Pos(settings.hapticsButton,-50);Pos(settings.replayButton,-115);Pos(settings.resetButton,-115);Pos(root.Find("HowToPlay"),-175);Pos(root.Find("Services"),-175);Pos(root.Find("Help"),-240);
            var collection=LocalizationEditorSettings.GetStringTableCollection("LabUI");foreach(var table in collection.StringTables){table.AddEntry("MOVE SPEED",table.LocaleIdentifier.Code=="es"?"VELOCIDAD":"MOVE SPEED");EditorUtility.SetDirty(table);}EditorUtility.SetDirty(collection.SharedData);EditorUtility.SetDirty(collection);LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene);EditorSceneManager.SaveScene(h.gameObject.scene);AssetDatabase.SaveAssets();
        }
    }
}
