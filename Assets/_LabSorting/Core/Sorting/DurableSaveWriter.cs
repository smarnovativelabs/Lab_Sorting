using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
namespace LabSorting.Sorting
{
    // Owns immutable plain-data snapshots only. Never touches scene objects on its worker.
    public sealed class DurableSaveWriter
    {
        sealed class Work {public long sequence;public SortingSession.Snapshot snapshot;}
        readonly object gate=new object();
        readonly string path;
        readonly Action<string,string,bool> write;
        Work pending,latest;
        Task task;
        bool running,protectBackup;
        long requested,completed;
        string error;
        double serializationMs,writeMs;
        public DurableSaveWriter(string path,Action<string,string,bool> writeOverride=null){this.path=path;write=writeOverride??AtomicWrite;}
        public bool Pending {get{lock(gate)return completed<requested;}}
        public int QueuedSnapshots {get{lock(gate)return pending==null?0:1;}}
        public long CompletedSequence {get{lock(gate)return completed;}}
        public string Error {get{lock(gate)return error;}}
        public double SerializationMilliseconds {get{lock(gate)return serializationMs;}}
        public double WriteMilliseconds {get{lock(gate)return writeMs;}}
        public void ProtectBackup(){lock(gate)protectBackup=true;}
        public long Enqueue(SortingSession.Snapshot snapshot)
        {
            lock(gate){latest=pending=new Work{sequence=++requested,snapshot=snapshot};StartLocked();return requested;}
        }
        void StartLocked(){if(running)return;running=true;task=Task.Run(Drain);}
        public void Retry(){lock(gate){if(running||completed>=requested)return;pending=latest;StartLocked();}}
        void Drain()
        {
            while(true)
            {
                Work work;bool preserve;
                lock(gate){work=pending;pending=null;preserve=protectBackup;if(work==null){running=false;return;}}
                try
                {
                    var clock=System.Diagnostics.Stopwatch.StartNew();string json=JsonUtility.ToJson(work.snapshot);double serialize=clock.Elapsed.TotalMilliseconds;clock.Restart();write(path,json,preserve);double disk=clock.Elapsed.TotalMilliseconds;
                    lock(gate){completed=work.sequence;error=null;protectBackup=false;serializationMs=serialize;writeMs=disk;}
                }
                catch(Exception exception){lock(gate){error=exception.Message;running=false;}return;}
            }
        }
        public bool Flush(int timeoutMilliseconds=5000)
        {
            Task current;lock(gate)current=task;
            if(current!=null&&!current.Wait(timeoutMilliseconds))return false;
            lock(gate)return completed>=requested;
        }
        public static void AtomicWrite(string path,string json,bool preserveBackup=false)
        {
            string temp=path+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,65536))
            {
                using(var writer=new StreamWriter(stream,new UTF8Encoding(false),4096,true)){writer.Write(json);writer.Flush();}
                stream.Flush(true);
            }
            if(File.Exists(path))File.Replace(temp,path,preserveBackup?null:path+".bak");else File.Move(temp,path);
        }
    }
}

