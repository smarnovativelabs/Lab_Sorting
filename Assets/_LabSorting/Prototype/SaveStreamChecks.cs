using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using LabSorting.Sorting;
namespace LabSorting.Prototype
{
    public static class SaveStreamChecks
    {
        public static void Run(SortingSession session,Action<bool,string> check)
        {
            var sample=session.Capture();sample.cash=int.MaxValue;sample.completionAcknowledged=true;
            sample.active="quote\" slash\\ tab\t line\n\r\b\f\u0001 / اردو 🧪";sample.searchId=sample.active;sample.searchRemaining=1.2345678f;
            sample.hand.Add(sample.active);sample.rewarded.Add(sample.active);sample.milestones.Add(sample.active);sample.completedRacks.Add(sample.active);
            sample.playerPosition=new Vector3(-.00001234567f,float.Epsilon,123456.78f);sample.look=new Vector2(-89.12345f,359.99997f);
            sample.items[0].rack=sample.active;sample.items[0].slot=-1;sample.items[0].location=ItemLocation.Rack;sample.items[0].rotation=new Quaternion(.1f,-.2f,.3f,.4f);
            var culture=CultureInfo.CurrentCulture;var path=Path.Combine(Application.persistentDataPath,"stream-check-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");
                using(var text=new StringWriter())
                {
                    new SaveJsonStream().Write(text,sample);
                    var loaded=JsonUtility.FromJson<SortingSession.Snapshot>(text.ToString());
                    check(JsonUtility.ToJson(loaded)==JsonUtility.ToJson(sample),"Streaming JSON preserves every field, escaped Unicode and float precision under comma-decimal culture");
                }
                var writer=new DurableSaveWriter(path);writer.Enqueue(sample);check(writer.Flush(),"Production streaming writer commits schema-compatible snapshot");
                string original=File.ReadAllText(path);check(JsonUtility.ToJson(JsonUtility.FromJson<SortingSession.Snapshot>(original))==JsonUtility.ToJson(sample),"Production UTF8 stream round-trips full schema");
                sample=session.Capture();sample.playerPosition=new Vector3(float.NaN,0,0);writer.Enqueue(sample);check(!writer.Flush()&&writer.Pending&&writer.Error!=null&&File.ReadAllText(path)==original,"Streaming failure leaves committed cash/progress intact and remains pending");
                var valid=session.Capture();writer.Enqueue(valid);check(writer.Flush()&&writer.Error==null,"Valid newest snapshot replaces failed streaming request");
                check(File.ReadAllText(path+".bak")==original,"Streaming recovery rotates only complete primary into backup");
            }
            finally{CultureInfo.CurrentCulture=culture;foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
        }
    }
}
