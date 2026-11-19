using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace LabSorting.Services
{
    // Persist payloads, not SDK objects. Each destination is acknowledged independently.
    [Serializable] internal sealed class TelemetryMessage
    {
        public string kind,name,context,transaction,product,currency,receipt,payload;
        public double value;
        public bool apple,firebaseSent,tenjinSent;
    }
    [Serializable] internal sealed class TelemetryOutbox
    {
        public List<TelemetryMessage> messages=new List<TelemetryMessage>();
        public List<string> purchases=new List<string>();
        public bool Add(TelemetryMessage message)
        {
            if(message.kind=="purchase")
            {
                if(string.IsNullOrWhiteSpace(message.transaction)||purchases.Contains(message.transaction))return false;
                purchases.Add(message.transaction);
            }
            // Gameplay bursts are bounded. Financial events are retained until handed to both SDKs.
            if(message.kind=="event"&&messages.FindAll(m=>m.kind=="event").Count>=256)
                messages.RemoveAt(messages.FindIndex(m=>m.kind=="event"));
            messages.Add(message);return true;
        }
        internal bool JournalPurchase(TelemetryMessage message,string path)
        {
            if(message==null||message.kind!="purchase"||string.IsNullOrWhiteSpace(message.transaction))return false;
            Add(message);
            return Save(path);
        }
        public bool Drain(Func<TelemetryMessage,bool> firebase,Func<TelemetryMessage,bool> tenjin)
        {
            bool changed=false;int sent=0;
            foreach(var m in messages)
            {
                if(!m.firebaseSent){try{if(firebase(m)){m.firebaseSent=true;changed=true;sent++;}}catch{}}
                if(!m.tenjinSent){try{if(tenjin(m)){m.tenjinSent=true;changed=true;sent++;}}catch{}}
                if(sent>=32)break;
            }
            messages.RemoveAll(m=>m.firebaseSent&&m.tenjinSent);return changed;
        }
        public static TelemetryOutbox Load(string path)
        {
            foreach(var candidate in new[]{path,path+".bak"})try{if(File.Exists(candidate)){var data=JsonUtility.FromJson<TelemetryOutbox>(File.ReadAllText(candidate));if(data?.messages!=null&&data.purchases!=null)return data;}}catch{}
            return new TelemetryOutbox();
        }
        public bool Save(string path)
        {
            try{File.WriteAllText(path+".tmp",JsonUtility.ToJson(this));using(var stream=new FileStream(path+".tmp",FileMode.Open,FileAccess.Write,FileShare.None))stream.Flush(true);if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);return true;}catch{return false;}
        }
    }
}
