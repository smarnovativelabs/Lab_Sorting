using System;
using System.Collections.Generic;
namespace LabSorting.UI
{
    // Bounded important-message queue; routine interaction feedback is never replayed late.
    public sealed class ToastPriority
    {
        struct Entry { public string text;public int priority;public float duration; }
        readonly List<Entry> pending=new List<Entry>(4);
        public string Text {get;private set;}="";
        public int Priority {get;private set;}
        public float Until {get;private set;}
        public void Supersede(string prefix,float now){pending.RemoveAll(e=>e.text.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));if(Text.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){Text="";Priority=0;Until=now;}}
        public void Push(string text,int priority,float now,float duration)
        {
            Tick(now);if(string.IsNullOrWhiteSpace(text)||text==Text||pending.Exists(e=>e.text==text))return;
            var entry=new Entry{text=text,priority=priority,duration=duration};
            if(now>=Until||priority>Priority){Display(entry,now);return;}
            if(priority==0){if(Priority==0)Display(entry,now);return;}
            if(pending.Count<4)pending.Add(entry);
        }
        void Display(Entry entry,float now){Text=entry.text;Priority=entry.priority;Until=now+entry.duration;}
        public void Tick(float now){if(now<Until)return;if(pending.Count==0){Text="";Priority=0;return;}var entry=pending[0];pending.RemoveAt(0);Display(entry,now);}
        public static int Classify(string text,bool error=false)
        {
            if(string.IsNullOrEmpty(text))return 0;
            if(text.IndexOf("save",StringComparison.OrdinalIgnoreCase)>=0)return 3;
            if(error||text.IndexOf("purchase",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("received",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("reward",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("complete",StringComparison.OrdinalIgnoreCase)>=0)return 2;
            return 0;
        }
    }
}
